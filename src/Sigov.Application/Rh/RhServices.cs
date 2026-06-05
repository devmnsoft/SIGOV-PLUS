using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Rh;

public sealed class RhService : IRhService
{
    private static readonly HashSet<string> Recursos = new(StringComparer.OrdinalIgnoreCase)
    {
        "servidores", "cargos", "lotacoes", "vinculos", "folhas", "folha-eventos", "folha-lancamentos",
        "pontos", "ferias", "afastamentos", "saude-ocupacional", "esocial", "portal-usuarios", "portal-acessos", "eventos"
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

    public RhService(IRhRepository repo, ICurrentTenant tenant, ICurrentUser user, IPermissionService permissions, IAuditService audit, ILogger<RhService> logger)
    {
        _repo = repo; _tenant = tenant; _user = user; _permissions = permissions; _audit = audit; _logger = logger;
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
    private static bool TryDateOnly(Dictionary<string, object?> dados, string key, out DateOnly value) => dados.TryGetValue(key, out var raw) && DateOnly.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out value);
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
        if (request.FolhaId <= 0) return Result<long>.Failure("Folha obrigatória para integração financeira.");
        if (string.IsNullOrWhiteSpace(request.Historico)) return Result<long>.Failure("Histórico obrigatório para integração financeira.");
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
        value = raw is JsonElement element ? element.ToString() : Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryInt(IReadOnlyDictionary<string, object?> dados, string campo, out int value)
    {
        value = default;
        return TryText(dados, campo, out var text) && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryDecimal(IReadOnlyDictionary<string, object?> dados, string campo, out decimal value)
    {
        value = default;
        return TryText(dados, campo, out var text) && decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryDate(IReadOnlyDictionary<string, object?> dados, string campo, out DateOnly value)
    {
        value = default;
        return TryText(dados, campo, out var text) && DateOnly.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static string OnlyDigits(string value) => new(value.Where(char.IsDigit).ToArray());
}
