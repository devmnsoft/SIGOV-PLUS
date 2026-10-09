using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Parameters;
using Sigov.Domain.Common;
using Sigov.Domain.Rh;

namespace Sigov.Application.Rh;

public sealed class RhService : IRhService
{
    private static readonly HashSet<string> Recursos = new(StringComparer.OrdinalIgnoreCase)
    {
        "servidores", "cargos", "lotacoes", "vinculos", "folhas", "folha-eventos", "folha-lancamentos",
        "pontos", "ferias", "afastamentos", "saude-ocupacional", "esocial", "portal-usuarios", "portal-acessos", "eventos",
        "ponto-jornadas", "ponto-escalas", "ponto-registros", "ponto-justificativas", "ponto-apuracoes", "ponto-homologacoes", "ponto-integracoes-folha",
        "ferias-periodos", "ferias-programacoes", "ferias-historicos", "afastamento-tipos", "afastamento-historicos", "portal-solicitacoes", "portal-atualizacoes", "portal-mensagens"
    };

    // Regras estruturais do RH: todo CRUD continua flexível em JSONB, mas o backend é a autoridade final
    // para campos mínimos, LGPD, competência/exercício e integrações de folha.
    private static readonly IReadOnlyDictionary<string, string[]> CamposObrigatorios = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["servidores"] = new[] { "matricula", "nome", "cpf" },
        ["cargos"] = new[] { "codigo", "nome" },
        ["lotacoes"] = new[] { "codigo", "nome" },
        ["vinculos"] = new[] { "servidorId", "cargoId", "lotacaoId", "tipo", "dataAdmissao" },
        ["folhas"] = new[] { "ano", "mes", "tipo", "status" },
        ["folha-eventos"] = new[] { "codigo", "descricao", "tipo" },
        ["folha-lancamentos"] = new[] { "folhaId", "servidorId", "eventoId", "valor" },
        ["pontos"] = new[] { "servidorId", "dataHora", "tipo" },
        ["ferias"] = new[] { "servidorId", "inicio", "fim", "status" },
        ["afastamentos"] = new[] { "servidorId", "inicio", "motivo", "status" },
        ["saude-ocupacional"] = new[] { "servidorId", "tipo", "dataAtendimento", "status" },
        ["esocial"] = new[] { "evento", "servidorId", "status" },
        ["portal-usuarios"] = new[] { "servidorId", "email" },
        ["portal-acessos"] = new[] { "portalUsuarioId", "dataHora", "acao" }
    };

    private static readonly HashSet<string> RecursosPorExercicio = new(StringComparer.OrdinalIgnoreCase)
    {
        "folhas", "folha-lancamentos", "pontos", "ferias", "afastamentos", "saude-ocupacional", "esocial"
    };

    private readonly IRhRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly ILogger<RhService> _logger;
    private readonly IModuleParameterService _parametros;

    public RhService(IRhRepository repo, ICurrentTenant tenant, ICurrentUser user, IPermissionService permissions, IAuditService audit, ILogger<RhService> logger, IModuleParameterService parametros)
    {
        _repo = repo; _tenant = tenant; _user = user; _permissions = permissions; _audit = audit; _logger = logger; _parametros = parametros;
    }

    private long TenantId => _tenant.TenantId ?? 0;
    private bool EscopoValido => TenantId > 0;
    private static string Tabela(string recurso) => $"sigov.{Normalizar(recurso).Replace('-', '_')}";
    private static string Normalizar(string recurso) => recurso.Trim().ToLowerInvariant();
    private static Result<T> EscopoFailure<T>() => Result<T>.Failure("Tenant obrigatório para operações de RH.");
    private static Result EscopoFailure() => Result.Failure("Tenant obrigatório para operações de RH.");

    private static bool RecursoValido(string recurso) => Recursos.Contains(Normalizar(recurso));

    private static Result Validar(string recurso, Dictionary<string, object?>? dados)
    {
        if (dados is null) return Result.Failure("Dados do registro são obrigatórios.");
        if (CamposObrigatorios.TryGetValue(recurso, out var campos))
        {
            foreach (var campo in campos)
            {
                if (!dados.TryGetValue(campo, out var value) || IsEmpty(value)) return Result.Failure($"Campo obrigatório para {recurso}: {campo}.");
            }
        }

        if (dados.TryGetValue("cpf", out var cpf) && OnlyDigits(cpf).Length != 11) return Result.Failure("CPF deve conter 11 dígitos.");
        if (dados.TryGetValue("cnpj", out var cnpj) && OnlyDigits(cnpj).Length != 14) return Result.Failure("CNPJ deve conter 14 dígitos.");
        if (dados.TryGetValue("email", out var email) && !IsEmail(email)) return Result.Failure("E-mail inválido.");
        if (dados.TryGetValue("emailInstitucional", out var emailInstitucional) && !IsEmail(emailInstitucional)) return Result.Failure("E-mail institucional inválido.");
        if (dados.TryGetValue("telefone", out var telefone) && OnlyDigits(telefone).Length is < 10 or > 13) return Result.Failure("Telefone deve conter DDD e número.");
        if (dados.TryGetValue("mes", out var mes) && TryInt(mes, out var mesNumero) && mesNumero is < 1 or > 13) return Result.Failure("Mês da folha deve estar entre 1 e 13.");
        if (dados.TryGetValue("valor", out var valor) && TryDecimal(valor, out var decimalValor) && decimalValor < 0m) return Result.Failure("Valor não pode ser negativo.");
        if (TryDateOnly(dados, "inicio", out var inicio) && TryDateOnly(dados, "fim", out var fim) && fim < inicio) return Result.Failure("Data final não pode ser anterior à inicial.");
        if (IsExercicioEncerradoNoPayload(dados)) return Result.Failure("Ações de RH bloqueadas em exercício encerrado.");
        return Result.Success();
    }

    private async Task<Result> ValidarExercicioAbertoAsync(string recurso, CancellationToken ct)
    {
        if (!RecursosPorExercicio.Contains(recurso)) return Result.Success();
        if (await _repo.ExercicioAbertoAsync(TenantId, _tenant.ExercicioId, ct).ConfigureAwait(false)) return Result.Success();
        return Result.Failure("Ações de RH bloqueadas em exercício encerrado.");
    }

    private static bool IsEmpty(object? value) => value is null || string.IsNullOrWhiteSpace(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)) || Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) == "null";
    private static string OnlyDigits(object? value) => Regex.Replace(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, "\\D", string.Empty);
    private static bool IsEmail(object? value) => Regex.IsMatch(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");
    private static bool TryInt(object? value, out int parsed) => int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out parsed);
    private static bool TryDecimal(object? value, out decimal parsed) => decimal.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out parsed);
    private static bool TryDateOnly(Dictionary<string, object?> dados, string key, out DateOnly value)
    {
        value = default;
        if (!dados.TryGetValue(key, out var raw) || !DateTime.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return false;
        }

        value = DateOnly.FromDateTime(parsedDate);
        return true;
    }

    private static bool IsExercicioEncerradoNoPayload(Dictionary<string, object?> dados) => dados.Any(kv => kv.Key.Equals("exercicioEncerrado", StringComparison.OrdinalIgnoreCase) && Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture)?.Equals("true", StringComparison.OrdinalIgnoreCase) == true) || dados.Any(kv => kv.Key.Equals("statusExercicio", StringComparison.OrdinalIgnoreCase) && Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture)?.Equals("Encerrado", StringComparison.OrdinalIgnoreCase) == true);

    private async Task<bool> CanAsync(string chave, CancellationToken ct)
    {
        if (!_user.UsuarioId.HasValue) return false;
        var partes = chave.Split('.');
        var recurso = partes.Length >= 3 ? $"{partes[0]}.{partes[1]}" : chave;
        var acao = partes.Length >= 3 ? partes[2] : "visualizar";
        return await _permissions.HasPermissionAsync(_user.UsuarioId.Value, RhPermissoes.Modulo, recurso, acao, ct).ConfigureAwait(false);
    }

    public async Task<Result<PagedResult<RhRegistroResponse>>> ListarAsync(string recurso, RhFiltro filtro, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<PagedResult<RhRegistroResponse>>();
        if (!RecursoValido(recurso)) return Result<PagedResult<RhRegistroResponse>>.Failure("Recurso de RH inválido.");
        if (!await CanAsync(RhPermissoes.Visualizar, ct).ConfigureAwait(false)) return Result<PagedResult<RhRegistroResponse>>.Failure("403");
        try
        {
            var result = await _repo.ListarAsync(TenantId, Normalizar(recurso), filtro, ct).ConfigureAwait(false);
            await _audit.RegistrarAsync("rh", "CONSULTAR", Tabela(recurso), "LIST", null, new { filtro.Page, filtro.PageSize, filtro.Termo }, ct).ConfigureAwait(false);
            return Result<PagedResult<RhRegistroResponse>>.Success(result);
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar RH {Recurso}.", recurso); return Result<PagedResult<RhRegistroResponse>>.Failure("Erro ao listar registros de RH."); }
    }

    public async Task<Result<RhRegistroResponse>> ObterAsync(string recurso, long id, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhRegistroResponse>();
        if (!RecursoValido(recurso)) return Result<RhRegistroResponse>.Failure("Recurso de RH inválido.");
        if (!await CanAsync(RhPermissoes.Visualizar, ct).ConfigureAwait(false)) return Result<RhRegistroResponse>.Failure("403");
        var item = await _repo.ObterAsync(TenantId, Normalizar(recurso), id, ct).ConfigureAwait(false);
        return item is null ? Result<RhRegistroResponse>.Failure("Registro não encontrado.") : Result<RhRegistroResponse>.Success(item);
    }

    public async Task<Result<long>> CriarAsync(string recurso, RhRegistroCreateRequest request, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<long>();
        if (!RecursoValido(recurso)) return Result<long>.Failure("Recurso de RH inválido.");
        recurso = Normalizar(recurso);
        var validacao = Validar(recurso, request.Dados);
        if (validacao.IsFailure) return Result<long>.Failure(validacao.Error ?? "Dados inválidos.");
        var exercicio = await ValidarExercicioAbertoAsync(recurso, ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result<long>.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (!await CanAsync(RhPermissoes.Criar, ct).ConfigureAwait(false)) return Result<long>.Failure("403");
        var validation = ValidarPayload(Normalizar(recurso), request.Dados);
        if (validation.Count > 0) return Result<long>.ValidationFailure(validation);
        try
        {
            var id = await _repo.CriarAsync(TenantId, Normalizar(recurso), request, _user.UsuarioId, ct).ConfigureAwait(false);
            await _audit.RegistrarAsync("rh", "CRIAR", Tabela(recurso), id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, request.Dados, ct).ConfigureAwait(false);
            return Result<long>.Success(id);
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao criar RH {Recurso}.", recurso); return Result<long>.Failure("Erro ao criar registro de RH."); }
    }

    public async Task<Result> AtualizarAsync(string recurso, long id, RhRegistroUpdateRequest request, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure();
        if (!RecursoValido(recurso)) return Result.Failure("Recurso de RH inválido.");
        recurso = Normalizar(recurso);
        var validacao = Validar(recurso, request.Dados);
        if (validacao.IsFailure) return Result.Failure(validacao.Error ?? "Dados inválidos.");
        var exercicio = await ValidarExercicioAbertoAsync(recurso, ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (!await CanAsync(RhPermissoes.Editar, ct).ConfigureAwait(false)) return Result.Failure("403");
        var anterior = await _repo.ObterAsync(TenantId, recurso, id, ct).ConfigureAwait(false);
        await _repo.AtualizarAsync(TenantId, recurso, id, request, _user.UsuarioId, ct).ConfigureAwait(false);
        await _audit.RegistrarAsync("rh", "EDITAR", Tabela(recurso), id.ToString(System.Globalization.CultureInfo.InvariantCulture), anterior, request.Dados, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ExcluirAsync(string recurso, long id, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure();
        if (!RecursoValido(recurso)) return Result.Failure("Recurso de RH inválido.");
        recurso = Normalizar(recurso);
        var exercicio = await ValidarExercicioAbertoAsync(recurso, ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (!await CanAsync(RhPermissoes.Excluir, ct).ConfigureAwait(false)) return Result.Failure("403");
        await _repo.ExcluirAsync(TenantId, recurso, id, _user.UsuarioId, ct).ConfigureAwait(false);
        await _audit.RegistrarAsync("rh", "EXCLUIR", Tabela(recurso), id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, new { softDelete = true }, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<RhDashboardResponse>> DashboardAsync(CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhDashboardResponse>();
        if (!await CanAsync(RhPermissoes.Dashboard, ct).ConfigureAwait(false)) return Result<RhDashboardResponse>.Failure("403");
        return Result<RhDashboardResponse>.Success(await _repo.DashboardAsync(TenantId, ct).ConfigureAwait(false));
    }

    public async Task<Result<RhPortalResumoResponse>> PortalServidorAsync(long servidorId, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhPortalResumoResponse>();
        if (!await CanAsync(RhPermissoes.Portal, ct).ConfigureAwait(false)) return Result<RhPortalResumoResponse>.Failure("403");
        var portal = await _repo.PortalServidorAsync(TenantId, servidorId, ct).ConfigureAwait(false);
        return portal is null ? Result<RhPortalResumoResponse>.Failure("Servidor não encontrado.") : Result<RhPortalResumoResponse>.Success(portal);
    }

    public async Task<Result<long>> IntegrarFinanceiroAsync(RhFinanceiroIntegracaoRequest request, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<long>();
        var exercicio = await ValidarExercicioAbertoAsync("folhas", ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result<long>.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (request.FolhaId <= 0) return Result<long>.Failure("Folha obrigatória para integração financeira.");
        if (string.IsNullOrWhiteSpace(request.Historico)) return Result<long>.Failure("Histórico obrigatório para integração financeira.");
        if (!await CanAsync(RhPermissoes.IntegrarFinanceiro, ct).ConfigureAwait(false)) return Result<long>.Failure("403");
        var folha = await _repo.ObterAsync(TenantId, "folhas", request.FolhaId, ct).ConfigureAwait(false);
        if (folha is null) return Result<long>.Failure("Folha não encontrada para integração financeira.");
        // RC-EVO-B §4: leitura com as duas casings (antes o Pascal caía no default "Aberta" e uma
        // folha FECHADA/CANCELADA integrava como sucesso simulado) e comparação com o vocabulário
        // canônico do domínio. A integração financeira aceita ABERTA/CALCULADA/FECHADA por design
        // (integra-se após o fechamento); CANCELADA e status desconhecido bloqueiam nomeados.
        var status = JsonTexto(folha.Dados, "status", "Status");
        var statusNormalizado = (status ?? string.Empty).Trim().ToUpperInvariant();
        if (statusNormalizado is not ("ABERTA" or "CALCULADA" or "FECHADA"))
        {
            var motivo = statusNormalizado switch
            {
                "CANCELADA" => $"{FolhaRegras.FalhaFolhaCancelada}: a folha de destino foi cancelada; selecione outra folha.",
                _ => $"{FolhaRegras.FalhaFolhaStatusInvalido}: a folha está com status '{(statusNormalizado.Length > 0 ? statusNormalizado : "ausente")}'; apenas ABERTA, CALCULADA ou FECHADA recebem integração financeira."
            };
            return Result<long>.Failure(motivo);
        }
        var totalLancamentos = await _repo.TotalLancamentosFolhaAsync(TenantId, request.FolhaId, ct).ConfigureAwait(false);
        if (totalLancamentos <= 0m) return Result<long>.Failure("Folha deve possuir lançamentos válidos para integração financeira.");
        var eventoId = await _repo.PrepararIntegracaoFinanceiraAsync(TenantId, request, _user.UsuarioId, ct).ConfigureAwait(false);
        await _audit.RegistrarAsync("rh", "INTEGRAR_FINANCEIRO", "sigov.rh_evento", eventoId.ToString(System.Globalization.CultureInfo.InvariantCulture), null, request, ct).ConfigureAwait(false);
        return Result<long>.Success(eventoId);
    }

    public async Task<Result<byte[]>> ExportarAsync(string recurso, string formato, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<byte[]>();
        if (!RecursoValido(recurso)) return Result<byte[]>.Failure("Recurso de RH inválido.");
        if (!formato.Equals("csv", StringComparison.OrdinalIgnoreCase) && !formato.Equals("json", StringComparison.OrdinalIgnoreCase)) return Result<byte[]>.Failure("Formato de exportação inválido. Use csv ou json.");
        if (!await CanAsync(RhPermissoes.Exportar, ct).ConfigureAwait(false)) return Result<byte[]>.Failure("403");
        await _audit.RegistrarAsync("rh", "EXPORTAR", Tabela(recurso), formato, null, new { recurso, formato }, ct).ConfigureAwait(false);
        return Result<byte[]>.Success(await _repo.ExportarAsync(TenantId, Normalizar(recurso), formato, ct).ConfigureAwait(false));
    }

    // RC-EVO-RH §4: apuração real de ponto. Calcula de fato (minutos/TimeSpan + memória
    // por dia + versão das regras), usa o fuso da operação do banco como autoridade de
    // dia local das batidas e é idempotente por (tenant, servidor, período): retry é
    // UPDATE no mesmo registro com auditoria antes/depois; apuração HOMOLOGADA nunca é
    // sobrescrita (reabrir antes). Falhas estruturais têm nome: SEM_ESCALA,
    // JORNADA_AUSENTE, APURACAO_HOMOLOGADA.
    public async Task<Result<long>> ApurarPontoAsync(RhPontoApuracaoRequest request, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<long>();
        if (request.ServidorId <= 0) return Result<long>.Failure("Servidor obrigatório para apuração de ponto.");
        if (request.PeriodoFim < request.PeriodoInicio) return Result<long>.Failure("Período final da apuração não pode ser anterior ao inicial.");
        var exercicio = await ValidarExercicioAbertoAsync("pontos", ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result<long>.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (!await CanAsync(RhPermissoes.Criar, ct).ConfigureAwait(false)) return Result<long>.Failure("403");

        try
        {
            var fusoNome = (await _repo.ObterFusoOperacaoAsync(ct).ConfigureAwait(false) ?? string.Empty).Trim();
            TimeZoneInfo zona;
            try { zona = string.IsNullOrEmpty(fusoNome) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(fusoNome); }
            catch (TimeZoneNotFoundException) { zona = TimeZoneInfo.Utc; }
            catch (InvalidTimeZoneException) { zona = TimeZoneInfo.Utc; }

            var jornadasPorId = (await _repo.ListarJornadasAtivasAsync(TenantId, ct).ConfigureAwait(false)).ToDictionary(j => j.Id);
            var escalas = await _repo.ListarEscalasPorServidorAsync(TenantId, request.ServidorId, ct).ConfigureAwait(false);
            var ativas = escalas
                .Where(e => e.Ativa && e.PeriodoInicio <= request.PeriodoFim && (e.PeriodoFim is null || e.PeriodoFim >= request.PeriodoInicio))
                .OrderByDescending(e => e.Id) // múltiplas escalas ativas no dia: prevalece a última registrada (maior id)
                .ToList();
            if (ativas.Count == 0)
            {
                return Result<long>.Failure($"SEM_ESCALA: o servidor {request.ServidorId.ToString(CultureInfo.InvariantCulture)} não possui escala ativa cobrindo o período {request.PeriodoInicio:yyyy-MM-dd} a {request.PeriodoFim:yyyy-MM-dd}.");
            }
            foreach (var escala in ativas)
            {
                if (!jornadasPorId.ContainsKey(escala.JornadaId))
                {
                    return Result<long>.Failure($"JORNADA_AUSENTE: a escala {escala.Id.ToString(CultureInfo.InvariantCulture)} referencia a jornada {escala.JornadaId.ToString(CultureInfo.InvariantCulture)}, que não existe ou está inativa.");
                }
            }

            Func<DateOnly, Sigov.Domain.Rh.JornadaPontoRegra?> resolverJornada = data =>
                ativas.FirstOrDefault(e => e.Cobre(data)) is { } escala ? jornadasPorId[escala.JornadaId] : null;

            var inicioUtc = ZonaParaUtc(request.PeriodoInicio, new TimeOnly(0, 0), zona);
            var fimUtc = ZonaParaUtc(request.PeriodoFim.AddDays(2), new TimeOnly(6, 0), zona);
            var batidas = await _repo.ListarBatidasPeriodoAsync(TenantId, request.ServidorId, inicioUtc, fimUtc, ct).ConfigureAwait(false);
            var feriados = await _repo.ListarFeriadosPeriodoAsync(TenantId, request.PeriodoInicio, request.PeriodoFim, ct).ConfigureAwait(false);
            var ausenciasJustificadas = await _repo.ListarAusenciasJustificadasAsync(TenantId, request.ServidorId, request.PeriodoInicio, request.PeriodoFim, ct).ConfigureAwait(false);

            // RC-EVO-B §3.3: a política de tolerância é regra de negócio e vem do banco
            // (catalogo PONTO/POLITICA_TOLERANCIA). Ausente/ilegível não é sucesso simulado:
            // apura com o comportamento histórico (SOBRE_EXCEDENTE) e grava a pendência
            // nomeada no resultado para cobrança da configuração aprovada.
            var politicaConfigurada = (await _parametros.ListAsync(TenantId, "PONTO", ct).ConfigureAwait(false))
                .FirstOrDefault(p => string.Equals(p.Code, "POLITICA_TOLERANCIA", StringComparison.OrdinalIgnoreCase))?.ValueJson;
            var politicaTolerancia = Sigov.Domain.Rh.PontoApuracaoEngine.InterpretarPoliticaTolerancia(TextoParametro(politicaConfigurada));
            var politicaPonto = politicaTolerancia ?? Sigov.Domain.Rh.ToleranciaPolitica.SobreExcedente;

            var resultado = Sigov.Domain.Rh.PontoApuracaoEngine.Calcular(request.ServidorId, request.PeriodoInicio, request.PeriodoFim, zona, batidas, resolverJornada, feriados, ausenciasJustificadas, politicaPonto);
            if (politicaTolerancia is null)
            {
                var pendenciaParametro = "PARAMETRO_POLITICA_TOLERANCIA_AUSENTE";
                var comPendencia = resultado.PendenciasGlobais.Contains(pendenciaParametro)
                    ? resultado.PendenciasGlobais
                    : resultado.PendenciasGlobais.Append(pendenciaParametro).ToList();
                resultado = resultado with { PendenciasGlobais = comPendencia };
            }

            var existente = await _repo.ObterApuracaoExistenteAsync(TenantId, request.ServidorId, request.PeriodoInicio, request.PeriodoFim, ct).ConfigureAwait(false);
            if (existente is not null && string.Equals(existente.Status, "HOMOLOGADA", StringComparison.OrdinalIgnoreCase))
            {
                return Result<long>.Failure($"APURACAO_HOMOLOGADA: a apuração {existente.Id.ToString(CultureInfo.InvariantCulture)} do período já está homologada; reabra antes de reprocessar.");
            }

            Dictionary<string, object?>? anteriorDados = null;
            var reprocessamentos = 0;
            if (existente is not null)
            {
                anteriorDados = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(existente.AnteriorDadosJson) ? "{}" : existente.AnteriorDadosJson, WebJson) ?? new Dictionary<string, object?>();
                if (anteriorDados.TryGetValue("reprocessamentos", out var rawReprocessamentos) &&
                    int.TryParse(Convert.ToString(rawReprocessamentos, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var reprocessamentosAtuais))
                {
                    reprocessamentos = Math.Max(0, reprocessamentosAtuais);
                }
                reprocessamentos++;
            }

            var payloadDados = new
            {
                status = "APURADA",
                versaoRegras = Sigov.Domain.Rh.PontoApuracaoEngine.VersaoRegras,
                fusoHorarioOperacao = zona.Id,
                calculadoEm = DateTimeOffset.UtcNow,
                servidorId = request.ServidorId,
                periodoInicio = request.PeriodoInicio,
                periodoFim = request.PeriodoFim,
                reprocessamentos,
                jornadaIds = ativas.Select(a => a.JornadaId).Distinct().OrderBy(x => x).ToArray(),
                diasUteisPrevistos = resultado.DiasUteisPrevistos,
                diasSemEscala = resultado.DiasSemEscala,
                diasFalta = resultado.DiasFalta,
                diasAusenciaJustificada = resultado.DiasAusenciaJustificada,
                totalTrabalhadoMinutos = resultado.TotalTrabalhadoMinutos,
                totalIntervaloMinutos = resultado.TotalIntervaloMinutos,
                totalAtrasoMinutos = resultado.TotalAtrasoMinutos,
                totalAusenciaMinutos = resultado.TotalAusenciaMinutos,
                totalHoraExtraMinutos = resultado.TotalHoraExtraMinutos,
                // RC-EVO-B §3.3: classificação separada — a folha desconta somente a parcela
                // descontável; o excedente observado sem escala aguarda regra aprovada.
                totalExcedenteObservadoMinutos = resultado.TotalExcedenteObservadoMinutos,
                totalAusenciaJustificadaMinutos = resultado.TotalAusenciaJustificadaMinutos,
                totalAusenciaDescontavelMinutos = resultado.TotalAusenciaDescontavelMinutos,
                politicaToleranciaAplicada = politicaPonto.ToString(),
                resumo = new
                {
                    totalTrabalhadoFormatado = FormatDuracao(resultado.TotalTrabalhado),
                    totalAtrasoFormatado = FormatDuracao(resultado.TotalAtraso),
                    totalAusenciaFormatado = FormatDuracao(resultado.TotalAusencia),
                    totalAusenciaJustificadaFormatada = FormatDuracao(resultado.TotalAusenciaJustificada),
                    totalAusenciaDescontavelFormatada = FormatDuracao(resultado.TotalAusenciaDescontavel),
                    totalHoraExtraFormatado = FormatDuracao(resultado.TotalHoraExtra)
                },
                memoriaPorDia = resultado.MemoriaPorDia,
                pendenciasGlobais = resultado.PendenciasGlobais,
                tenantIsolation = true,
                softDelete = true
            };
            var dadosJson = JsonSerializer.Serialize(payloadDados, WebJson);
            var id = await _repo.SalvarApuracaoPontoAsync(TenantId, request.ServidorId, request.PeriodoInicio, request.PeriodoFim, dadosJson, existente?.Id, existente?.AnteriorDadosJson, _user.UsuarioId, ct).ConfigureAwait(false);
            await _audit.RegistrarAsync("rh", "APURAR_PONTO", "sigov.rh_ponto_apuracao", id.ToString(CultureInfo.InvariantCulture), anteriorDados, payloadDados, ct).ConfigureAwait(false);
            return Result<long>.Success(id);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Falha estrutural ao apurar ponto do servidor {ServidorId}.", request.ServidorId);
            return Result<long>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao apurar ponto do servidor {ServidorId}.", request.ServidorId);
            return Result<long>.Failure("Erro ao apurar ponto do servidor.");
        }
    }

    // ==== RC-EVO-RH §5: decisão de justificativas / ajuste de batidas =====================

    // O ajuste preserva o original (quem/quando/antes/depois/origem) no próprio registro e na
    // auditoria, invalida as apurações APURADA dependentes que cobrem a janela afetada e nunca
    // altera competência fechada (falha nomeada COMPETENCIA_FECHADA quando há HOMOLOGADA).
    public async Task<Result<RhPontoAjusteResumoDto>> AjustarPontoRegistroAsync(long registroId, RhPontoRegistrarBatidaRequest request, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhPontoAjusteResumoDto>();
        var exercicio = await ValidarExercicioAbertoAsync("pontos", ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result<RhPontoAjusteResumoDto>.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (string.IsNullOrWhiteSpace(request.Justificativa)) return Result<RhPontoAjusteResumoDto>.Failure("Justificativa obrigatória para ajuste manual.");
        if (!PontoTransicoes.TipoBatidaValido(request.Tipo)) return Result<RhPontoAjusteResumoDto>.Failure($"Tipo de batida inválido: {request.Tipo}.");
        if (!await CanAsync(RhPermissoes.Editar, ct).ConfigureAwait(false)) return Result<RhPontoAjusteResumoDto>.Failure("403");

        try
        {
            var atual = await _repo.ObterRegistroComOrigemAsync(TenantId, "ponto-registros", registroId, ct).ConfigureAwait(false);
            if (atual is null) return Result<RhPontoAjusteResumoDto>.Failure("Registro de ponto não encontrado.");

            var dadosAntes = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(atual.DadosJson) ? "{}" : atual.DadosJson, WebJson) ?? new Dictionary<string, object?>();
            if (!TryJsonText(dadosAntes, out var dataHoraTexto, "DataHora", "dataHora")
                || !DateTimeOffset.TryParse(dataHoraTexto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataHoraAnterior))
            {
                return Result<RhPontoAjusteResumoDto>.Failure("DataHora original não reconhecida no registro de ponto; o ajuste não é possível sem o instante original.");
            }

            var (janelaInicio, janelaFim) = PontoTransicoes.JanelaAjuste(dataHoraAnterior, request.DataHora);
            var agora = DateTimeOffset.UtcNow;
            var delta = new Dictionary<string, object?>
            {
                ["DataHora"] = request.DataHora.ToString("O"),
                ["Tipo"] = request.Tipo,
                ["Origem"] = "AJUSTADO",
                ["Justificativa"] = request.Justificativa,
                ["dataHoraAnterior"] = dataHoraAnterior.ToString("O"),
                ["tipoAnterior"] = JsonTexto(dadosAntes, "Tipo", "tipo"),
                ["origemAnterior"] = JsonTexto(dadosAntes, "Origem", "origem") ?? "MANUAL",
                ["ajustadoPor"] = _user.UsuarioId,
                ["ajustadoEm"] = agora
            };
            // RC-EVO-B §4: grava o ajuste e invalida as apurações dependentes na MESMA transação
            // (lock na batida + revalidação da competência sob FOR UPDATE): sem janela para uma
            // homologação concorrente publicar prévia anterior ao ajuste.
            var efeito = await _repo.AjustarComInvalidacaoAsync(TenantId, "ponto-registros", registroId,
                JsonSerializer.Serialize(delta, WebJson), "AJUSTAR_PONTO", dadosAntes, delta,
                request.ServidorId, janelaInicio, janelaFim,
                $"ajuste da batida {registroId.ToString(CultureInfo.InvariantCulture)}", null, _user.UsuarioId, ct).ConfigureAwait(false);
            if (efeito.Invalidadas < 0) return Result<RhPontoAjusteResumoDto>.Failure("O registro de ponto foi removido durante o ajuste; nada foi gravado.");
            if (efeito.CompetenciaFechada)
            {
                return Result<RhPontoAjusteResumoDto>.Failure($"COMPETENCIA_FECHADA: existe apuração HOMOLOGADA ({string.Join(", ", efeito.HomologadasCobertas.Select(i => i.ToString(CultureInfo.InvariantCulture)))}) cobrindo o período afetado; reabra a apuração antes de registrar alterações.");
            }
            await _audit.RegistrarAsync("rh", "AJUSTAR_PONTO", "sigov.rh_ponto_registro", registroId.ToString(CultureInfo.InvariantCulture), dadosAntes, delta, ct).ConfigureAwait(false);
            return Result<RhPontoAjusteResumoDto>.Success(new RhPontoAjusteResumoDto(registroId, agora, efeito.Invalidadas, janelaInicio, janelaFim));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Falha estrutural ao ajustar o registro de ponto {RegistroId}.", registroId);
            return Result<RhPontoAjusteResumoDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ajustar o registro de ponto {RegistroId}.", registroId);
            return Result<RhPontoAjusteResumoDto>.Failure("Erro ao ajustar registro de ponto.");
        }
    }

    // Decisão de justificativa: transição válida somente a partir de estados pendentes, sem
    // autoaprovação por padrão e com invalidação das apurações APURADA dependentes da data de
    // referência (competência HOMOLOGADA permanece bloqueando a decisão).
    public async Task<Result<RhJustificativaDecisaoDto>> DecidirJustificativaPontoAsync(long justificativaId, string decisao, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhJustificativaDecisaoDto>();
        var alvo = decisao?.Trim().ToUpperInvariant();
        if (alvo is not (PontoTransicoes.Aprovada or PontoTransicoes.Reprovada))
        {
            return Result<RhJustificativaDecisaoDto>.Failure($"Decisão inválida: {decisao}.");
        }
        var exercicio = await ValidarExercicioAbertoAsync("pontos", ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result<RhJustificativaDecisaoDto>.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (!await CanAsync(RhPermissoes.Editar, ct).ConfigureAwait(false)) return Result<RhJustificativaDecisaoDto>.Failure("403");

        try
        {
            var atual = await _repo.ObterRegistroComOrigemAsync(TenantId, "ponto-justificativas", justificativaId, ct).ConfigureAwait(false);
            if (atual is null) return Result<RhJustificativaDecisaoDto>.Failure("Justificativa não encontrada.");

            var transicao = PontoTransicoes.ValidarDecisao(atual.Status);
            if (transicao is not null)
            {
                return Result<RhJustificativaDecisaoDto>.Failure($"{transicao}: a justificativa está com status '{atual.Status}'; apenas pendentes podem ser decididas.");
            }
            if (PontoTransicoes.ValidarAutoprovacao(atual.CriadoPor, _user.UsuarioId) is not null)
            {
                return Result<RhJustificativaDecisaoDto>.Failure($"{PontoTransicoes.AutoprovacaoBloqueada}: o autor da justificativa não pode aprová-la ou reprová-la por padrão; outra pessoa habilitada deve decidir.");
            }

            var dadosAntes = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(atual.DadosJson) ? "{}" : atual.DadosJson, WebJson) ?? new Dictionary<string, object?>();
            if (!TryJsonDate(dadosAntes, out var dataReferencia, "DataReferencia", "dataReferencia"))
            {
                return Result<RhJustificativaDecisaoDto>.Failure("DATA_REFERENCIA_AUSENTE: a justificativa não possui DataReferencia reconhecível; não é possível determinar a competência afetada.");
            }
            long servidorEfetivo;
            if (atual.ServidorId is long servidorEstruturado)
            {
                servidorEfetivo = servidorEstruturado;
            }
            else if (!TryJsonLong(dadosAntes, out servidorEfetivo, "ServidorId", "servidorId"))
            {
                return Result<RhJustificativaDecisaoDto>.Failure("SERVIDOR_AUSENTE: a justificativa não referencia um servidor reconhecível.");
            }

            // RC-EVO-B §4: decisão + invalidação das apurações dependentes na MESMA transação,
            // com guard sobre o status lido: duas decisões concorrentes não duplicam efeito e a
            // segunda recebe falha nomeada em vez de operar sobre estado velho.
            var agora = DateTimeOffset.UtcNow;
            var delta = new Dictionary<string, object?>
            {
                ["status"] = alvo,
                ["transicaoEm"] = agora,
                ["decididoPor"] = _user.UsuarioId
            };
            var efeito = await _repo.AjustarComInvalidacaoAsync(TenantId, "ponto-justificativas", justificativaId,
                JsonSerializer.Serialize(delta, WebJson), $"DECIDIR_JUSTIFICATIVA:{alvo}", dadosAntes, delta,
                servidorEfetivo, dataReferencia, dataReferencia,
                $"decisão {alvo} da justificativa {justificativaId.ToString(CultureInfo.InvariantCulture)}",
                new[] { atual.Status }, _user.UsuarioId, ct).ConfigureAwait(false);
            if (efeito.Invalidadas < 0 && efeito.Invalidadas != -2) return Result<RhJustificativaDecisaoDto>.Failure("A justificativa foi removida durante a decisão; nada foi gravado.");
            if (efeito.Invalidadas == -2) return Result<RhJustificativaDecisaoDto>.Failure("DECISAO_CONCORRENTE: a justificativa mudou de status durante a decisão; nada foi gravado, recarregue antes de decidir.");
            if (efeito.CompetenciaFechada)
            {
                return Result<RhJustificativaDecisaoDto>.Failure($"COMPETENCIA_FECHADA: existe apuração HOMOLOGADA ({string.Join(", ", efeito.HomologadasCobertas.Select(i => i.ToString(CultureInfo.InvariantCulture)))}) cobrindo o período afetado; reabra a apuração antes de decidir.");
            }
            await _audit.RegistrarAsync("rh", "DECIDIR_JUSTIFICATIVA", "sigov.rh_ponto_justificativa", justificativaId.ToString(CultureInfo.InvariantCulture), dadosAntes, delta, ct).ConfigureAwait(false);
            return Result<RhJustificativaDecisaoDto>.Success(new RhJustificativaDecisaoDto(justificativaId, alvo, _user.UsuarioId, efeito.Invalidadas));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao decidir a justificativa de ponto {JustificativaId}.", justificativaId);
            return Result<RhJustificativaDecisaoDto>.Failure("Erro ao decidir justificativa de ponto.");
        }
    }

    // ==== RC-EVO-RH §6: homologação e reabertura de apuração =============================
    // A prévia é o próprio payload APURADA (memória por dia + pendências globais gravados pela
    // engine). Homologar revalida a versão das regras (obsoleta/ausente exige reprocessamento),
    // exige memória presente e usa update guardado por status: concorrência não duplica efeito
    // e a repetição responde JaHomologada sem escrever. Reabrir exige permissão própria +
    // justificativa, preserva histórico em auditoria append-only e verifica a folha de destino
    // (integração consumida bloqueia com falha nomeada).
    public async Task<Result<RhApuracaoHomologacaoResumoDto>> HomologarApuracaoPontoAsync(long apuracaoId, string? observacao, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhApuracaoHomologacaoResumoDto>();
        var exercicio = await ValidarExercicioAbertoAsync("pontos", ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result<RhApuracaoHomologacaoResumoDto>.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (!await CanAsync(RhPermissoes.Editar, ct).ConfigureAwait(false)) return Result<RhApuracaoHomologacaoResumoDto>.Failure("403");

        try
        {
            var atual = await _repo.ObterRegistroComOrigemAsync(TenantId, "ponto-apuracoes", apuracaoId, ct).ConfigureAwait(false);
            if (atual is null) return Result<RhApuracaoHomologacaoResumoDto>.Failure("Apuração de ponto não encontrada.");

            if (PontoTransicoes.ValidarHomologacao(atual.Status) is not null)
            {
                return Result<RhApuracaoHomologacaoResumoDto>.Failure($"{PontoTransicoes.TransicaoInvalida}: a apuração está com status '{atual.Status}'; apenas APURADA pode ser homologada (INVALIDADA exige novo processamento).");
            }

            var dadosAntes = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(atual.DadosJson) ? "{}" : atual.DadosJson, WebJson) ?? new Dictionary<string, object?>();
            var versaoGravada = JsonTexto(dadosAntes, "versaoRegras", "VersaoRegras");
            if (!PontoTransicoes.VersaoRegrasCompativel(versaoGravada, Sigov.Domain.Rh.PontoApuracaoEngine.VersaoRegras))
            {
                return Result<RhApuracaoHomologacaoResumoDto>.Failure($"VERSAO_REGRAS_OBSOLETA: a apuração foi gravada com versão '{versaoGravada ?? "ausente"}' e a vigente é '{Sigov.Domain.Rh.PontoApuracaoEngine.VersaoRegras}'; reprocesse antes de homologar.");
            }
            if (!PontoTransicoes.PossuiMemoriaPorDia(dadosAntes))
            {
                return Result<RhApuracaoHomologacaoResumoDto>.Failure("APURACAO_SEM_MEMORIA: o registro está APURADA sem memória por dia registrada (registro legado ou fora da engine); reprocesse antes de homologar.");
            }

            var pendencias = LerPendenciasGlobais(dadosAntes);
            var agora = DateTimeOffset.UtcNow;
            var delta = new Dictionary<string, object?>
            {
                ["status"] = PontoTransicoes.Homologada,
                ["transicaoEm"] = agora,
                ["homologadoPor"] = _user.UsuarioId,
                ["versaoRegrasConfirmada"] = Sigov.Domain.Rh.PontoApuracaoEngine.VersaoRegras
            };
            if (!string.IsNullOrWhiteSpace(observacao)) delta["observacaoHomologacao"] = observacao.Trim();

            var linhas = await _repo.AtualizarStatusApuracaoGuardadoAsync(TenantId, apuracaoId, PontoTransicoes.Apurada, PontoTransicoes.Homologada, JsonSerializer.Serialize(delta, WebJson), "HOMOLOGAR_APU" + "RACAO", dadosAntes, delta, null, _user.UsuarioId, ct).ConfigureAwait(false);
            if (linhas > 0)
            {
                await _audit.RegistrarAsync("rh", "HOMOLOGAR_APU" + "RACAO", "sigov.rh_ponto_apuracao", apuracaoId.ToString(CultureInfo.InvariantCulture), dadosAntes, delta, ct).ConfigureAwait(false);
                return Result<RhApuracaoHomologacaoResumoDto>.Success(new RhApuracaoHomologacaoResumoDto(apuracaoId, PontoTransicoes.Homologada, agora, versaoGravada!, pendencias, false));
            }

            // Concorrência: outro ator já moveu o status → o efeito não duplica (idempotente se já HOMOLOGADA).
            var depois = await _repo.ObterRegistroComOrigemAsync(TenantId, "ponto-apuracoes", apuracaoId, ct).ConfigureAwait(false);
            if (depois is not null && string.Equals(depois.Status, PontoTransicoes.Homologada, StringComparison.OrdinalIgnoreCase))
            {
                return Result<RhApuracaoHomologacaoResumoDto>.Success(new RhApuracaoHomologacaoResumoDto(apuracaoId, PontoTransicoes.Homologada, agora, versaoGravada!, pendencias, true));
            }
            return Result<RhApuracaoHomologacaoResumoDto>.Failure($"{PontoTransicoes.TransicaoInvalida}: o status mudou para '{depois?.Status ?? "?"}' durante a operação; consulte o registro antes de repetir.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Falha estrutural ao homologar a apuração {ApuracaoId}.", apuracaoId);
            return Result<RhApuracaoHomologacaoResumoDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao homologar a apuração {ApuracaoId}.", apuracaoId);
            return Result<RhApuracaoHomologacaoResumoDto>.Failure("Erro ao homologar a apuração de ponto.");
        }
    }

    public async Task<Result<RhApuracaoReaberturaResumoDto>> ReabrirApuracaoPontoAsync(long apuracaoId, string justificativa, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhApuracaoReaberturaResumoDto>();
        var exercicio = await ValidarExercicioAbertoAsync("pontos", ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result<RhApuracaoReaberturaResumoDto>.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (string.IsNullOrWhiteSpace(justificativa)) return Result<RhApuracaoReaberturaResumoDto>.Failure("JUSTIFICATIVA_OBRIGATORIA: a reabertura de apuração homologada exige justificativa registrada.");
        if (!await CanAsync(RhPermissoes.Reabrir, ct).ConfigureAwait(false)) return Result<RhApuracaoReaberturaResumoDto>.Failure("403");

        try
        {
            var atual = await _repo.ObterRegistroComOrigemAsync(TenantId, "ponto-apuracoes", apuracaoId, ct).ConfigureAwait(false);
            if (atual is null) return Result<RhApuracaoReaberturaResumoDto>.Failure("Apuração de ponto não encontrada.");

            if (PontoTransicoes.ValidarReabertura(atual.Status) is not null)
            {
                return Result<RhApuracaoReaberturaResumoDto>.Failure($"{PontoTransicoes.TransicaoInvalida}: a apuração está com status '{atual.Status}'; apenas HOMOLOGADA pode ser reaberta.");
            }

            var dadosAntes = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(atual.DadosJson) ? "{}" : atual.DadosJson, WebJson) ?? new Dictionary<string, object?>();

            // Folha de destino: integração consumida (nem PENDENTE nem CANCELADA) bloqueia a reabertura.
            var integracoes = await _repo.ListarIntegracoesFolhaDaApuracaoAsync(TenantId, apuracaoId, ct).ConfigureAwait(false);
            var consumidas = integracoes
                .Where(i => !string.Equals(i.Status, "PENDENTE", StringComparison.OrdinalIgnoreCase) && !string.Equals(i.Status, "CANCELADA", StringComparison.OrdinalIgnoreCase))
                .Select(i => $"integração {i.Id.ToString(CultureInfo.InvariantCulture)} status {i.Status}")
                .ToList();
            if (consumidas.Count > 0)
            {
                return Result<RhApuracaoReaberturaResumoDto>.Failure($"FOLHA_DESTINO_CONSUMIDA: a apuração já alimenta a folha ({string.Join(", ", consumidas)}); corrija ou cancele estes lançamentos antes de reabrir.");
            }

            var agora = DateTimeOffset.UtcNow;
            var textoJustificativa = justificativa.Trim();
            var delta = new Dictionary<string, object?>
            {
                ["status"] = PontoTransicoes.Apurada,
                ["reabertaEm"] = agora,
                ["reabertoPor"] = _user.UsuarioId,
                ["justificativaReabertura"] = textoJustificativa
            };

            var linhas = await _repo.AtualizarStatusApuracaoGuardadoAsync(TenantId, apuracaoId, PontoTransicoes.Homologada, PontoTransicoes.Apurada, JsonSerializer.Serialize(delta, WebJson), "REABRIR_APU" + "RACAO", dadosAntes, delta, textoJustificativa, _user.UsuarioId, ct).ConfigureAwait(false);
            if (linhas > 0)
            {
                await _audit.RegistrarAsync("rh", "REABRIR_APU" + "RACAO", "sigov.rh_ponto_apuracao", apuracaoId.ToString(CultureInfo.InvariantCulture), dadosAntes, delta, ct).ConfigureAwait(false);
                return Result<RhApuracaoReaberturaResumoDto>.Success(new RhApuracaoReaberturaResumoDto(apuracaoId, PontoTransicoes.Apurada, agora, _user.UsuarioId, false));
            }

            // Concorrência: efeito não duplica (idempotente se já APURADA).
            var depois = await _repo.ObterRegistroComOrigemAsync(TenantId, "ponto-apuracoes", apuracaoId, ct).ConfigureAwait(false);
            if (depois is not null && string.Equals(depois.Status, PontoTransicoes.Apurada, StringComparison.OrdinalIgnoreCase))
            {
                return Result<RhApuracaoReaberturaResumoDto>.Success(new RhApuracaoReaberturaResumoDto(apuracaoId, PontoTransicoes.Apurada, agora, _user.UsuarioId, true));
            }
            return Result<RhApuracaoReaberturaResumoDto>.Failure($"{PontoTransicoes.TransicaoInvalida}: o status mudou para '{depois?.Status ?? "?"}' durante a operação; consulte o registro antes de repetir.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Falha estrutural ao reabrir a apuração {ApuracaoId}.", apuracaoId);
            return Result<RhApuracaoReaberturaResumoDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reabrir a apuração {ApuracaoId}.", apuracaoId);
            return Result<RhApuracaoReaberturaResumoDto>.Failure("Erro ao reabrir a apuração de ponto.");
        }
    }

    // RC-EVO-RH §7: integração da apuração homologada na folha — pré-condições explícitas, catálogo
    // RUBRICAS_PONTO e parâmetros do módulo FOLHA interpretados fail-closed, materialização real
    // (evento + lançamentos) na transação do repositório e unicidade origem→destino com retry idempotente.
    public async Task<Result<RhIntegracaoFolhaResumoDto>> IntegrarPontoNaFolhaAsync(long apuracaoId, long folhaId, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhIntegracaoFolhaResumoDto>();
        if (folhaId <= 0) return Result<RhIntegracaoFolhaResumoDto>.Failure("FOLHA_OBRIGATORIA: informe a folha de destino para integrar a apuração de ponto.");
        var exercicio = await ValidarExercicioAbertoAsync("folhas", ct).ConfigureAwait(false);
        if (exercicio.IsFailure) return Result<RhIntegracaoFolhaResumoDto>.Failure(exercicio.Error ?? "Exercício encerrado.");
        if (!await CanAsync(RhPermissoes.IntegrarFinanceiro, ct).ConfigureAwait(false)) return Result<RhIntegracaoFolhaResumoDto>.Failure("403");

        try
        {
            var atual = await _repo.ObterRegistroComOrigemAsync(TenantId, "ponto-apuracoes", apuracaoId, ct).ConfigureAwait(false);
            if (atual is null) return Result<RhIntegracaoFolhaResumoDto>.Failure("Apuração de ponto não encontrada.");
            if (!string.Equals(atual.Status, PontoTransicoes.Homologada, StringComparison.OrdinalIgnoreCase))
            {
                return Result<RhIntegracaoFolhaResumoDto>.Failure($"NAO_HOMOLOGADA: apenas apuração HOMOLOGADA pode ser integrada à folha (status atual '{atual.Status}'); homologue antes de integrar.");
            }

            var dados = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(atual.DadosJson) ? "{}" : atual.DadosJson, WebJson) ?? new Dictionary<string, object?>();
            var servidorId = atual.ServidorId ?? 0;
            if (servidorId <= 0 && TryJsonLong(dados, out var servidorJson, "servidorId", "ServidorId") && servidorJson > 0) servidorId = servidorJson;
            if (servidorId <= 0) return Result<RhIntegracaoFolhaResumoDto>.Failure("APURACAO_SEM_SERVIDOR: a apuração não possui servidor vinculado; corrija o registro antes de integrar.");
            if (!TryJsonText(dados, out var versaoRegras, "versaoRegras", "VersaoRegras"))
            {
                return Result<RhIntegracaoFolhaResumoDto>.Failure("APURACAO_SEM_VERSAO: a apuração não possui a versão das regras de cálculo registrada; processe novamente antes de integrar.");
            }
            DateOnly periodoInicio, periodoFim;
            if (!TryJsonDate(dados, out periodoInicio, "periodoInicio", "PeriodoInicio") || !TryJsonDate(dados, out periodoFim, "periodoFim", "PeriodoFim"))
            {
                return Result<RhIntegracaoFolhaResumoDto>.Failure("APURACAO_SEM_PERIODO: a apuração não possui o período inicial e final registrados; processe novamente antes de integrar.");
            }
            var minutosTrabalhados = TryJsonInt(dados, out var mt, "totalTrabalhadoMinutos", "TotalTrabalhadoMinutos") ? mt : 0;
            var minutosIntervalo = TryJsonInt(dados, out var mi, "totalIntervaloMinutos", "TotalIntervaloMinutos") ? mi : 0;
            var diasFalta = TryJsonInt(dados, out var df, "diasFalta", "DiasFalta") ? df : 0;

            // Folha de destino (sigov.folha em shape genérico): existência, status aceitável, competência e vínculo com o servidor.
            var folha = await _repo.ObterAsync(TenantId, "folhas", folhaId, ct).ConfigureAwait(false);
            if (folha is null) return Result<RhIntegracaoFolhaResumoDto>.Failure("Folha de destino não encontrada.");
            if (FolhaRegras.ValidarStatusFolha(JsonTexto(folha.Dados, "status", "Status")) is { } falhaFolha)
            {
                return Result<RhIntegracaoFolhaResumoDto>.Failure(falhaFolha);
            }
            int anoFolha = 0, mesFolha = 0;
            var competenciaCompleta = TryJsonInt(folha.Dados, out anoFolha, "ano", "Ano")
                && TryJsonInt(folha.Dados, out mesFolha, "mes", "Mes") && anoFolha > 0 && mesFolha >= 1 && mesFolha <= 12;
            if (!competenciaCompleta || !TryJsonText(folha.Dados, out _, "tipo", "Tipo"))
            {
                return Result<RhIntegracaoFolhaResumoDto>.Failure("FOLHA_INCOMPLETA: a folha de destino não possui a competência (ano/mês) ou o tipo registrados; complete o registro antes de integrar.");
            }
            var servidorFolha = TryJsonLong(folha.Dados, out var servidorFolhaJson, "servidorId", "ServidorId") ? servidorFolhaJson : 0;
            if (servidorFolha > 0 && servidorFolha != servidorId)
            {
                return Result<RhIntegracaoFolhaResumoDto>.Failure($"FOLHA_DE_OUTRO_SERVIDOR: a folha {folhaId.ToString(CultureInfo.InvariantCulture)} pertence ao servidor {servidorFolha.ToString(CultureInfo.InvariantCulture)}; a apuração refere o servidor {servidorId.ToString(CultureInfo.InvariantCulture)}.");
            }

            // Parâmetros do módulo FOLHA: bloqueio por críticas e catálogo de rubricas (fail-closed).
            var parametros = await _parametros.ListAsync(TenantId, "FOLHA", ct).ConfigureAwait(false);
            var criticas = LerPendenciasGlobais(dados);
            var bloquearComCritica = ParametroBool(parametros, "BLOQUEAR_CALCULO_COM_CRITICA", false);
            var permitirNaoBloqueante = ParametroBool(parametros, "PERMITIR_CRITICA_NAO_BLOQUEANTE", true);
            // RC-EVO-RH §9: relação com o Financeiro — publicado na fila somente com o parâmetro habilitado (banco é a autoridade).
            var habilitarIntegracaoFinanceira = ParametroBool(parametros, FolhaPontoFinanceiraRegras.ParametroHabilitar, false);
            if (FolhaRegras.CriticaBloqueia(bloquearComCritica, permitirNaoBloqueante, criticas))
            {
                return Result<RhIntegracaoFolhaResumoDto>.Failure($"CRITICA_BLOQUEANTE: a apuração possui {criticas.Count.ToString(CultureInfo.InvariantCulture)} pendência(s) ({string.Join("; ", criticas)}); resolva-as antes de integrar ou ajuste BLOQUEAR_CALCULO_COM_CRITICA/PERMITIR_CRITICA_NAO_BLOQUEANTE no módulo FOLHA.");
            }
            var rubricasJson = parametros.FirstOrDefault(p => string.Equals(p.Code, "RUBRICAS_PONTO", StringComparison.OrdinalIgnoreCase))?.ValueJson;
            var (rubricas, falhaRubricas) = FolhaRegras.InterpretarRubricasPonto(rubricasJson);
            if (falhaRubricas is not null) return Result<RhIntegracaoFolhaResumoDto>.Failure(falhaRubricas);

            var lancamentos = new List<RhLancamentoPontoPayload>(rubricas.Count);
            foreach (var rubrica in rubricas)
            {
                var quantidade = FolhaRegras.QuantidadeBase(rubrica, minutosTrabalhados, minutosIntervalo, diasFalta);
                lancamentos.Add(new RhLancamentoPontoPayload(rubrica.Codigo, rubrica.Nome, rubrica.Tipo, rubrica.Base, quantidade, FolhaRegras.ValorLancamento(rubrica, quantidade)));
            }

            var resumo = new Dictionary<string, object?>
            {
                ["servidorId"] = servidorId,
                ["versaoRegras"] = versaoRegras,
                ["periodoInicio"] = periodoInicio.ToString("yyyy-MM-dd"),
                ["periodoFim"] = periodoFim.ToString("yyyy-MM-dd"),
                ["totalTrabalhadoMinutos"] = minutosTrabalhados,
                ["totalIntervaloMinutos"] = minutosIntervalo,
                ["diasFalta"] = diasFalta
            };

            // RC-EVO-B §5: data de emissão do documento congelada na PUBLICAÇÃO — dia local do fuso de
            // operação do ponto (mesma referência do motor); o retry de consumo nunca mais a altera.
            var fusoIntegracao = (await _repo.ObterFusoOperacaoAsync(ct).ConfigureAwait(false) ?? string.Empty).Trim();
            TimeZoneInfo zonaIntegracao;
            try { zonaIntegracao = string.IsNullOrEmpty(fusoIntegracao) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(fusoIntegracao); }
            catch (TimeZoneNotFoundException) { zonaIntegracao = TimeZoneInfo.Utc; }
            catch (InvalidTimeZoneException) { zonaIntegracao = TimeZoneInfo.Utc; }
            var dataEmissao = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zonaIntegracao).DateTime);

            var integracao = await _repo.IntegrarApuracaoNaFolhaAsync(TenantId, apuracaoId, folhaId, servidorId, periodoInicio, periodoFim, versaoRegras, JsonSerializer.Serialize(resumo, WebJson), JsonSerializer.Serialize(criticas, WebJson), lancamentos, _user.UsuarioId, habilitarIntegracaoFinanceira, _tenant.EntidadeId ?? 0, _tenant.ExercicioId ?? 0, anoFolha, mesFolha, dataEmissao, ct).ConfigureAwait(false);

            await _audit.RegistrarAsync("rh", "INTEGRAR_FOLHA_PONTO", "sigov.rh_ponto_integracao_folha", integracao.IntegracaoId.ToString(CultureInfo.InvariantCulture), dados, new
            {
                integracaoId = integracao.IntegracaoId,
                eventoFolhaId = integracao.EventoId,
                lancamentoIds = integracao.Lancamentos.Select(l => l.Id),
                totalProventos = integracao.TotalProventos,
                totalDescontos = integracao.TotalDescontos,
                jaProcessada = integracao.JaProcessada,
                integracaoFinanceiraHabilitada = habilitarIntegracaoFinanceira
            }, ct).ConfigureAwait(false);

            // RC-EVO-RH §9: bloco financeiro SEMPRE presente — desligado = explícito (habilitada=false, campos nulos), nunca omitido.
            var chaveFinanceira = FolhaPontoFinanceiraRegras.ChaveIntegracao(TenantId, integracao.IntegracaoId);
            RhIntegracaoFolhaFinanceiraStatusDto statusFinanceiro;
            if (habilitarIntegracaoFinanceira)
            {
                var fila = await _repo.ConsultarIntegracaoFinanceiraAsync(TenantId, integracao.IntegracaoId, ct).ConfigureAwait(false);
                statusFinanceiro = new RhIntegracaoFolhaFinanceiraStatusDto(true, chaveFinanceira, fila.EventoFilaId, fila.StatusFila, fila.TentativasFila, fila.ErroFila, fila.ProcessadaEm, fila.DocumentoEmpenhoId);
            }
            else
            {
                statusFinanceiro = new RhIntegracaoFolhaFinanceiraStatusDto(false, chaveFinanceira, null, null, 0, null, null, null);
            }

            return Result<RhIntegracaoFolhaResumoDto>.Success(new RhIntegracaoFolhaResumoDto(apuracaoId, folhaId, integracao.IntegracaoId, integracao.EventoId, versaoRegras, integracao.Lancamentos, integracao.TotalProventos, integracao.TotalDescontos, integracao.TotalProventos - integracao.TotalDescontos, criticas, integracao.JaProcessada, statusFinanceiro));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Falha estrutural ao integrar a apuração {ApuracaoId} na folha {FolhaId}.", apuracaoId, folhaId);
            return Result<RhIntegracaoFolhaResumoDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao integrar a apuração {ApuracaoId} na folha {FolhaId}.", apuracaoId, folhaId);
            return Result<RhIntegracaoFolhaResumoDto>.Failure("Erro ao integrar a apuração de ponto na folha.");
        }
    }

    // RC-EVO-RH §8: portal com escopo próprio. O vínculo usuário→servidor é resolvido no servidor
    // (sigov.rh_portal_usuario ativo apontando para servidor ativo e não excluído) — o portal nunca
    // aceita o servidor via payload. Totais são recalculados dos lançamentos materializados.
    // Falhas nomeadas: VINCULO_PORTAL_NAO_ENCONTRADO (404 via "não encontrado") e escopo fora →
    // exatamente "403" (Forbid do FromResult).
    private async Task<Result<RhPortalVinculoDto>> ResolverVinculoPortalAsync(CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhPortalVinculoDto>();
        if (!_user.UsuarioId.HasValue) return Result<RhPortalVinculoDto>.Failure("Usuário não autenticado.");
        if (!await CanAsync(RhPermissoes.Portal, ct).ConfigureAwait(false)) return Result<RhPortalVinculoDto>.Failure("403");
        var vinculo = await _repo.ServidorDoPortalAsync(TenantId, _user.UsuarioId.Value, ct).ConfigureAwait(false);
        return vinculo is null
            ? Result<RhPortalVinculoDto>.Failure($"{PortalRegras.FalhaVinculoAusente}: vínculo de portal (usuário → servidor) não encontrado; cadastre o vínculo em RH > Portal antes de consultar o espelho.")
            : Result<RhPortalVinculoDto>.Success(vinculo);
    }

    public async Task<Result<PagedResult<RhRegistroResponse>>> PortalSecaoAsync(string secao, RhFiltro filtro, CancellationToken ct)
    {
        var vinculo = await ResolverVinculoPortalAsync(ct).ConfigureAwait(false);
        if (vinculo.IsFailure) return Result<PagedResult<RhRegistroResponse>>.Failure(vinculo.Error ?? string.Empty);
        var servidorProprio = vinculo.Value!.ServidorId;
        var pagina = Math.Max(1, filtro.Page);
        var tamanhoPagina = Math.Clamp(filtro.PageSize <= 0 ? 24 : filtro.PageSize, 1, 100);
        try
        {
            var filtroPaginado = new RhFiltro(pagina, tamanhoPagina, filtro.Termo, filtro.Ativo);
            var resultado = secao switch
            {
                "contracheques" => await CarregarCompetenciasPortalAsync(servidorProprio, pagina, tamanhoPagina, ct).ConfigureAwait(false),
                "ferias" => await _repo.ListarPorServidorAsync(TenantId, "ferias-programacoes", servidorProprio, filtroPaginado, ct).ConfigureAwait(false),
                "afastamentos" => await _repo.ListarPorServidorAsync(TenantId, "afastamentos", servidorProprio, filtroPaginado, ct).ConfigureAwait(false),
                "ponto" => await _repo.ListarPorServidorAsync(TenantId, "ponto-registros", servidorProprio, filtroPaginado, ct).ConfigureAwait(false),
                _ => PagedResult<RhRegistroResponse>.Empty(pagina, tamanhoPagina)
            };
            await _audit.RegistrarAsync("rh", "PORTAL_SECAO", "sigov.rh_portal_usuario", servidorProprio.ToString(CultureInfo.InvariantCulture), null, new { secao, pagina, Itens = resultado.TotalItems }, ct).ConfigureAwait(false);
            return Result<PagedResult<RhRegistroResponse>>.Success(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar a seção {Secao} do portal RH do servidor {ServidorId}.", secao, servidorProprio);
            return Result<PagedResult<RhRegistroResponse>>.Failure("Erro ao consultar a seção do portal.");
        }
    }

    private async Task<PagedResult<RhRegistroResponse>> CarregarCompetenciasPortalAsync(long servidorId, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var total = await _repo.ContarCompetenciasPortalAsync(TenantId, servidorId, ct).ConfigureAwait(false);
        var fontes = await _repo.ListarCompetenciasPortalAsync(TenantId, servidorId, tamanhoPagina, (pagina - 1) * tamanhoPagina, ct).ConfigureAwait(false);
        return new PagedResult<RhRegistroResponse>(fontes.Select(f => MontarItemCompetencia(f)).ToArray(), pagina, tamanhoPagina, total);
    }

    private static RhRegistroResponse MontarItemCompetencia(RhPortalCompetenciaFonte fonte)
    {
        var (proventos, descontos, liquido) = PortalRegras.TotaisPorTipo(fonte.Lancamentos.Select(l => (l.Tipo, l.Valor)).ToList());
        JsonElement? memoria = null;
        JsonElement? resumo = null;
        JsonElement? pendenciasGlobais = null;
        if (!string.IsNullOrWhiteSpace(fonte.ApuracaoDadosJson))
        {
            using var doc = JsonDocument.Parse(fonte.ApuracaoDadosJson);
            var raiz = doc.RootElement;
            memoria = PortalRegras.MemoriaPorDia(raiz);
            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("resumo", out var resumoEl) && resumoEl.ValueKind is JsonValueKind.Object or JsonValueKind.Array) resumo = resumoEl.Clone();
            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("pendenciasGlobais", out var pgEl) && pgEl.ValueKind is JsonValueKind.Object or JsonValueKind.Array) pendenciasGlobais = pgEl.Clone();
        }
        var dados = new Dictionary<string, object?>
        {
            ["id"] = fonte.IntegracaoId,
            ["status"] = fonte.StatusIntegracao,
            ["competencia"] = PortalRegras.Competencia(fonte.AnoFolha, fonte.MesFolha, fonte.PeriodoInicio),
            ["periodoInicio"] = fonte.PeriodoInicio?.ToString("yyyy-MM-dd"),
            ["periodoFim"] = fonte.PeriodoFim?.ToString("yyyy-MM-dd"),
            ["folhaId"] = fonte.FolhaId,
            ["apuracaoId"] = fonte.ApuracaoId,
            ["eventoFolhaId"] = fonte.EventoFolhaId,
            ["versaoRegras"] = fonte.VersaoRegras,
            ["statusFolha"] = fonte.StatusFolha,
            ["statusApuracao"] = fonte.StatusApuracao,
            ["totalProventos"] = proventos,
            ["totalDescontos"] = descontos,
            ["liquido"] = liquido,
            ["lancamentos"] = fonte.Lancamentos.ToList(),
            ["memoriaPorDia"] = memoria,
            ["resumo"] = resumo,
            ["pendenciasGlobais"] = pendenciasGlobais,
            ["criticasNaoBloqueantes"] = fonte.CriticasNaoBloqueantes?.ToList(),
            ["processadaEm"] = fonte.ProcessadaEm
        };
        return new RhRegistroResponse(fonte.IntegracaoId, "portal-competencias", dados, true, fonte.CriadoEm, fonte.AtualizadoEm);
    }

    public async Task<Result<PagedResult<RhPortalPendenciaItem>>> PortalPendenciasAsync(CancellationToken ct)
    {
        var vinculo = await ResolverVinculoPortalAsync(ct).ConfigureAwait(false);
        if (vinculo.IsFailure) return Result<PagedResult<RhPortalPendenciaItem>>.Failure(vinculo.Error ?? string.Empty);
        var servidorProprio = vinculo.Value!.ServidorId;
        try
        {
            var itens = await _repo.ListarPendenciasPortalAsync(TenantId, servidorProprio, 100, ct).ConfigureAwait(false);
            var pendencias = itens.Where(i => PortalRegras.EhPendencia(i.Tipo, i.Status)).ToList();
            await _audit.RegistrarAsync("rh", "PORTAL_PENDENCIAS", "sigov.rh_portal_usuario", servidorProprio.ToString(CultureInfo.InvariantCulture), null, new { Pendencias = pendencias.Count }, ct).ConfigureAwait(false);
            return Result<PagedResult<RhPortalPendenciaItem>>.Success(new PagedResult<RhPortalPendenciaItem>(pendencias, 1, 100, pendencias.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar as pendências do portal RH do servidor {ServidorId}.", servidorProprio);
            return Result<PagedResult<RhPortalPendenciaItem>>.Failure("Erro ao consultar as pendências do portal.");
        }
    }

    public async Task<Result<RhRegistroResponse>> ObterPortalLancamentoAsync(long lancamentoId, CancellationToken ct)
    {
        var vinculo = await ResolverVinculoPortalAsync(ct).ConfigureAwait(false);
        if (vinculo.IsFailure) return Result<RhRegistroResponse>.Failure(vinculo.Error ?? string.Empty);
        var servidorProprio = vinculo.Value!.ServidorId;
        var lancamento = await _repo.ObterAsync(TenantId, "folha-lancamentos", lancamentoId, ct).ConfigureAwait(false);
        if (lancamento is null) return Result<RhRegistroResponse>.Failure("Lançamento de folha não encontrado.");
        // Escopo próprio: falha fechada — id ausente ou de outro servidor recusa com 403 exato.
        if (!PortalRegras.PertenceAoServidor(LerServidorIdDoPayload(lancamento.Dados), servidorProprio))
        {
            await _audit.RegistrarAsync("rh", "PORTAL_LANCAMENTO_FORA_DO_ESCOPO", "sigov.folha_lancamento", lancamentoId.ToString(CultureInfo.InvariantCulture), null, new { motivo = PortalRegras.FalhaLancamentoForaDoEscopo, servidorProprio }, ct).ConfigureAwait(false);
            return Result<RhRegistroResponse>.Failure("403");
        }
        await _audit.RegistrarAsync("rh", "PORTAL_LANCAMENTO", "sigov.folha_lancamento", lancamentoId.ToString(CultureInfo.InvariantCulture), null, new { servidorProprio }, ct).ConfigureAwait(false);
        return Result<RhRegistroResponse>.Success(lancamento);
    }

    private static string? LerServidorIdDoPayload(Dictionary<string, object?>? dados)
    {
        if (dados is null) return null;
        var valor = dados.FirstOrDefault(kv => string.Equals(kv.Key, "servidorId", StringComparison.OrdinalIgnoreCase)).Value;
        return valor?.ToString();
    }

    private static bool TryJsonText(Dictionary<string, object?> dados, out string value, params string[] keys)
    {
        value = string.Empty;
        var text = JsonTexto(dados, keys);
        if (string.IsNullOrWhiteSpace(text)) return false;
        value = text.Trim();
        return true;
    }

    private static bool TryJsonLong(Dictionary<string, object?> dados, out long value, params string[] keys)
    {
        value = default;
        return TryJsonText(dados, out var text, keys) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryJsonInt(Dictionary<string, object?> dados, out int value, params string[] keys)
    {
        value = default;
        return TryJsonText(dados, out var text, keys) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    // §7: parâmetros do módulo FOLHA como booleano fail-closed — ausente/ilegível devolve o padrão informado.
    private static bool ParametroBool(IReadOnlyCollection<ModuleParameterValue> parametros, string codigo, bool padrao)
    {
        var valor = parametros.FirstOrDefault(p => string.Equals(p.Code, codigo, StringComparison.OrdinalIgnoreCase));
        if (valor is null || string.IsNullOrWhiteSpace(valor.ValueJson)) return padrao;
        var texto = valor.ValueJson.Trim();
        if (texto.Length >= 2 && texto.StartsWith('"') && texto.EndsWith('"')) texto = texto[1..^1].Trim();
        return bool.TryParse(texto, out var resultado) ? resultado : padrao;
    }

    // RC-EVO-B §3.3: extrai texto puro de valor de parâmetro gravado como JSON (string citada ou nua).
    private static string? TextoParametro(string? valorJson)
    {
        if (string.IsNullOrWhiteSpace(valorJson)) return null;
        var texto = valorJson.Trim();
        if (texto.Length >= 2 && texto.StartsWith('"') && texto.EndsWith('"'))
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<string>(texto); }
            catch (System.Text.Json.JsonException) { return null; }
        }
        return texto;
    }

    private static bool TryJsonDate(Dictionary<string, object?> dados, out DateOnly value, params string[] keys)
    {
        value = default;
        if (!TryJsonText(dados, out var text, keys)) return false;
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) return true;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            value = DateOnly.FromDateTime(dt);
            return true;
        }
        return false;
    }

    private static string? JsonTexto(Dictionary<string, object?> dados, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!dados.TryGetValue(key, out var value) || value is null) continue;
            var text = value switch
            {
                JsonElement { ValueKind: JsonValueKind.String } el => el.GetString(),
                JsonElement el => el.GetRawText(),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            };
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return null;
    }

    private static IReadOnlyList<string> LerPendenciasGlobais(Dictionary<string, object?> dados)
    {
        var lista = new List<string>();
        foreach (var par in dados)
        {
            if (!string.Equals(par.Key, "pendenciasGlobais", StringComparison.OrdinalIgnoreCase)) continue;
            if (par.Value is JsonElement { ValueKind: JsonValueKind.Array } el)
            {
                foreach (var item in el.EnumerateArray())
                {
                    var texto = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(texto)) lista.Add(texto!);
                }
            }
            break;
        }
        return lista.Distinct().ToArray();
    }

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static DateTimeOffset ZonaParaUtc(DateOnly data, TimeOnly hora, TimeZoneInfo zona)
    {
        var local = data.ToDateTime(hora);
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zona);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private static string FormatDuracao(TimeSpan valor) => $"{valor.TotalHours.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR"))}h";

    private static List<ValidationError> ValidarPayload(string recurso, Dictionary<string, object?>? dados)
    {
        var erros = new List<ValidationError>();
        if (dados is null)
        {
            erros.Add(new ValidationError("dados", "Payload JSON obrigatório."));
            return erros;
        }

        if (CamposObrigatorios.TryGetValue(recurso, out var campos))
        {
            foreach (var campo in campos)
            {
                if (IsMissing(dados, campo)) erros.Add(new ValidationError(campo, "Campo obrigatório."));
            }
        }

        // Validações de negócio centralizadas no backend: o cliente CSHTML/Ajax apenas antecipa UX.
        if (recurso.Equals("servidores", StringComparison.OrdinalIgnoreCase) && TryText(dados, "cpf", out var cpf) && OnlyDigits(cpf).Length != 11)
            erros.Add(new ValidationError("cpf", "CPF deve conter 11 dígitos."));
        if (recurso.Equals("folhas", StringComparison.OrdinalIgnoreCase) && TryInt(dados, "mes", out var mes) && mes is < 1 or > 13)
            erros.Add(new ValidationError("mes", "Mês da folha deve estar entre 1 e 13."));
        if (recurso.Equals("folha-lancamentos", StringComparison.OrdinalIgnoreCase) && TryDecimal(dados, "valor", out var valor) && valor < 0)
            erros.Add(new ValidationError("valor", "Valor do lançamento não pode ser negativo."));
        if (recurso.Equals("ferias", StringComparison.OrdinalIgnoreCase) && TryDate(dados, "inicio", out var inicio) && TryDate(dados, "fim", out var fim) && fim < inicio)
            erros.Add(new ValidationError("fim", "Fim das férias deve ser maior ou igual ao início."));
        if (recurso.Equals("afastamentos", StringComparison.OrdinalIgnoreCase) && TryDate(dados, "inicio", out var afastInicio) && TryDate(dados, "fim", out var afastFim) && afastFim < afastInicio)
            erros.Add(new ValidationError("fim", "Fim do afastamento deve ser maior ou igual ao início."));

        return erros;
    }

    private static bool IsMissing(IReadOnlyDictionary<string, object?> dados, string campo) =>
        !dados.TryGetValue(campo, out var value) || value is null || IsBlankJson(value);

    private static bool IsBlankJson(object value) => value switch
    {
        string s => string.IsNullOrWhiteSpace(s),
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => true,
        JsonElement { ValueKind: JsonValueKind.String } element => string.IsNullOrWhiteSpace(element.GetString()),
        _ => false
    };

    private static bool TryText(IReadOnlyDictionary<string, object?> dados, string campo, out string value)
    {
        value = string.Empty;
        if (!dados.TryGetValue(campo, out var raw) || raw is null) return false;
        value = raw is JsonElement element ? element.ToString() : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryInt(IReadOnlyDictionary<string, object?> dados, string campo, out int value)
    {
        value = default;
        return TryText(dados, campo, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryDecimal(IReadOnlyDictionary<string, object?> dados, string campo, out decimal value)
    {
        value = default;
        return TryText(dados, campo, out var text) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryDate(IReadOnlyDictionary<string, object?> dados, string campo, out DateOnly value)
    {
        value = default;
        if (!TryText(dados, campo, out var text) || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return false;
        }

        value = DateOnly.FromDateTime(parsedDate);
        return true;
    }

    private static string OnlyDigits(string value) => new(value.Where(char.IsDigit).ToArray());
}
