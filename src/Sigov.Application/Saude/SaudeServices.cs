using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Saas;
using Sigov.Domain.Common;
using Sigov.Domain.Saude;

namespace Sigov.Application.Saude;

public sealed class SaudeService : IUnidadeSaudeService, IProfissionalSaudeService, IPacienteService, IProntuarioService, IAtendimentoSaudeService, IAgendaSaudeService, IFarmaciaService, IVacinacaoService, ILaboratorioService, IRegulacaoService, IAcsService, IAcsSyncService, ISaudeDashboardService, ISaudeExportacaoService
{
    private readonly ISaudeCrudRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IPermissionService _permissions;
    private readonly IModuloLicenciamentoService _modulos;
    private readonly IFeatureFlagService _features;
    private readonly IAuditService _audit;
    private readonly ILgpdMaskingService _lgpd;
    private readonly ILogger<SaudeService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SaudeService(ISaudeCrudRepository repo, ICurrentTenant tenant, ICurrentUser user, IPermissionService permissions, IModuloLicenciamentoService modulos, IFeatureFlagService features, IAuditService audit, ILgpdMaskingService lgpd, ILogger<SaudeService> logger)
    { _repo = repo; _tenant = tenant; _user = user; _permissions = permissions; _modulos = modulos; _features = features; _audit = audit; _lgpd = lgpd; _logger = logger; }

    private long TenantId => _tenant.TenantId ?? 0;
    private long EntidadeId => _tenant.EntidadeId ?? 1;
    private long? ExercicioId => _tenant.ExercicioId;
    private long? UsuarioId => _user.UsuarioId;
    private static Result<T> Fail<T>(string msg) => Result<T>.Failure(msg);
    private static Result Fail(string msg) => Result.Failure(msg);

    private async Task<Result> GuardAsync(string recurso, string acao, CancellationToken ct, bool offline = false)
    {
        if (TenantId <= 0) return Fail("Tenant obrigatório para operações de Saúde.");
        if (!await _modulos.IsModuleEnabledAsync(TenantId, SaudePermissoes.Modulo, ct).ConfigureAwait(false)) return await NegarAsync(recurso, acao, "Módulo saúde não contratado/habilitado para o tenant.", ct).ConfigureAwait(false);
        if (offline && !await _features.IsEnabledAsync(TenantId, SaudePermissoes.AcsOfflineFeature, ct).ConfigureAwait(false)) return await NegarAsync(recurso, acao, "Feature ACS offline desabilitada para o tenant.", ct).ConfigureAwait(false);
        if (!_user.IsAuthenticated || !UsuarioId.HasValue) return await NegarAsync(recurso, acao, "Usuário autenticado obrigatório.", ct).ConfigureAwait(false);
        var ok = await _permissions.HasPermissionAsync(UsuarioId.Value, SaudePermissoes.Modulo, recurso, acao, ct).ConfigureAwait(false);
        return ok ? Result.Success() : await NegarAsync(recurso, acao, "Usuário sem permissão para a operação de Saúde.", ct).ConfigureAwait(false);
    }

    private async Task<Result> NegarAsync(string recurso, string acao, string motivo, CancellationToken ct)
    {
        await _audit.RegistrarAsync("saude", "ACESSO_NEGADO", "seguranca_evento", recurso, null, new { recurso, acao, motivo, usuarioId = UsuarioId, tenantId = TenantId }, ct).ConfigureAwait(false);
        return Fail(motivo);
    }

    private async Task<Result<long>> CriarAsync(string recurso, string acao, object request, CancellationToken ct, bool offline = false)
    {
        var guard = await GuardAsync(recurso, acao, ct, offline).ConfigureAwait(false);
        if (guard.IsFailure) return Fail<long>(guard.Error ?? "Operação bloqueada.");
        try
        {
            Validate(recurso, request);
            if (recurso == "farmacia_produto" && request is FarmaciaProdutoCreateRequest fp) _ = new FarmaciaProduto(fp.Codigo, fp.Nome);
            if (recurso == "acs_microarea" && request is AcsMicroareaCreateRequest am) _ = new AcsMicroarea(am.Codigo, am.Nome);
            if (recurso == "acs_dispositivo" && request is AcsDispositivoCreateRequest ad) _ = new AcsDispositivo(ad.ProfissionalAcsId, ad.Identificador);
            var id = await _repo.CriarAsync(TenantId, EntidadeId, ExercicioId, recurso, request, UsuarioId, ct).ConfigureAwait(false);
            await AuditarAsync("CRIAR", recurso, id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, MaskPayload(request), ct).ConfigureAwait(false);
            return Result<long>.Success(id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return Fail<long>(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar recurso Saúde {Recurso} no tenant {TenantId}.", recurso, TenantId);
            return Fail<long>("Falha ao executar operação de Saúde.");
        }
    }

    private async Task<Result> AtualizarAsync(string recurso, string acao, long id, object request, CancellationToken ct)
    {
        var guard = await GuardAsync(recurso, acao, ct).ConfigureAwait(false);
        if (guard.IsFailure) return guard;
        try
        {
            var anterior = await _repo.ObterAsync<object>(TenantId, EntidadeId, ExercicioId, recurso, id, ct).ConfigureAwait(false);
            await _repo.AtualizarAsync(TenantId, EntidadeId, ExercicioId, recurso, id, request, UsuarioId, ct).ConfigureAwait(false);
            await AuditarAsync(acao.ToUpperInvariant(), recurso, id.ToString(System.Globalization.CultureInfo.InvariantCulture), anterior, MaskPayload(request), ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar recurso Saúde {Recurso} Id={Id}.", recurso, id);
            return Fail("Falha ao executar operação de Saúde.");
        }
    }

    private async Task<Result<PagedResult<T>>> ListarAsync<T>(string recurso, string permissao, object filtro, CancellationToken ct)
    {
        var guard = await GuardAsync(permissao, "visualizar", ct).ConfigureAwait(false);
        if (guard.IsFailure) return Fail<PagedResult<T>>(guard.Error ?? "Operação bloqueada.");
        var page = await _repo.ListarAsync<T>(TenantId, EntidadeId, ExercicioId, recurso, filtro, ct).ConfigureAwait(false);
        if (IsSensivel(recurso)) await RegistrarAcessoPessoalAsync(recurso, "LISTAR", ct).ConfigureAwait(false);
        return Result<PagedResult<T>>.Success(page);
    }

    private async Task<Result<T>> ObterAsync<T>(string recurso, string permissao, long id, CancellationToken ct) where T : class
    {
        var guard = await GuardAsync(permissao, "visualizar", ct).ConfigureAwait(false);
        if (guard.IsFailure) return Fail<T>(guard.Error ?? "Operação bloqueada.");
        var item = await _repo.ObterAsync<T>(TenantId, EntidadeId, ExercicioId, recurso, id, ct).ConfigureAwait(false);
        if (item is null) return Fail<T>("Registro não encontrado.");
        if (IsSensivel(recurso)) await RegistrarAcessoPessoalAsync(recurso, id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
        return Result<T>.Success(item);
    }

    private async Task<Result> ExcluirAsync(string recurso, string permissao, long id, CancellationToken ct)
    {
        var guard = await GuardAsync(permissao, "excluir", ct).ConfigureAwait(false);
        if (guard.IsFailure) return guard;
        await _repo.ExcluirAsync(TenantId, EntidadeId, recurso, id, UsuarioId, ct).ConfigureAwait(false);
        await AuditarAsync("EXCLUIR", recurso, id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, new { softDelete = true }, ct).ConfigureAwait(false);
        return Result.Success();
    }

    Task<Result<PagedResult<UnidadeSaudeResponse>>> IUnidadeSaudeService.ListarAsync(UnidadeSaudeFiltro f, CancellationToken ct) => ListarAsync<UnidadeSaudeResponse>("unidade", "unidade", f, ct);
    Task<Result<UnidadeSaudeResponse>> IUnidadeSaudeService.ObterAsync(long id, CancellationToken ct) => ObterAsync<UnidadeSaudeResponse>("unidade", "unidade", id, ct);
    Task<Result<long>> IUnidadeSaudeService.CriarAsync(UnidadeSaudeCreateRequest r, CancellationToken ct) => CriarAsync("unidade", "criar", r, ct);
    Task<Result> IUnidadeSaudeService.AtualizarAsync(long id, UnidadeSaudeUpdateRequest r, CancellationToken ct) => AtualizarAsync("unidade", "editar", id, r, ct);
    Task<Result> IUnidadeSaudeService.ExcluirAsync(long id, CancellationToken ct) => ExcluirAsync("unidade", "unidade", id, ct);
    Task<Result<PagedResult<ProfissionalSaudeResponse>>> IProfissionalSaudeService.ListarAsync(ProfissionalSaudeFiltro f, CancellationToken ct) => ListarAsync<ProfissionalSaudeResponse>("profissional", "profissional", f, ct);
    Task<Result<ProfissionalSaudeResponse>> IProfissionalSaudeService.ObterAsync(long id, CancellationToken ct) => ObterAsync<ProfissionalSaudeResponse>("profissional", "profissional", id, ct);
    Task<Result<long>> IProfissionalSaudeService.CriarAsync(ProfissionalSaudeCreateRequest r, CancellationToken ct) => CriarAsync("profissional", "criar", r, ct);
    Task<Result> IProfissionalSaudeService.AtualizarAsync(long id, ProfissionalSaudeUpdateRequest r, CancellationToken ct) => AtualizarAsync("profissional", "editar", id, r, ct);
    Task<Result> IProfissionalSaudeService.ExcluirAsync(long id, CancellationToken ct) => ExcluirAsync("profissional", "profissional", id, ct);
    Task<Result<PagedResult<PacienteResumoResponse>>> IPacienteService.ListarAsync(PacienteFiltro f, CancellationToken ct) => ListarAsync<PacienteResumoResponse>("paciente", "paciente", f, ct);
    Task<Result<PacienteDetalheResponse>> IPacienteService.ObterAsync(long id, CancellationToken ct) => ObterAsync<PacienteDetalheResponse>("paciente", "paciente", id, ct);
    Task<Result<long>> IPacienteService.CriarAsync(PacienteCreateRequest r, CancellationToken ct) => CriarAsync("paciente", "criar", r, ct);
    Task<Result> IPacienteService.AtualizarAsync(long id, PacienteUpdateRequest r, CancellationToken ct) => AtualizarAsync("paciente", "editar", id, r, ct);
    Task<Result> IPacienteService.ExcluirAsync(long id, CancellationToken ct) => ExcluirAsync("paciente", "paciente", id, ct);
    Task<Result<ProntuarioResponse>> IProntuarioService.ObterPorPacienteAsync(long pacienteId, CancellationToken ct) => ObterAsync<ProntuarioResponse>("prontuario_paciente", "prontuario", pacienteId, ct);
    Task<Result> IProntuarioService.AtualizarAsync(long pacienteId, ProntuarioUpdateRequest r, CancellationToken ct) => AtualizarAsync("prontuario_paciente", "editar", pacienteId, r, ct);
    Task<Result<PagedResult<AtendimentoSaudeResponse>>> IAtendimentoSaudeService.ListarAsync(AtendimentoSaudeFiltro f, CancellationToken ct) => ListarAsync<AtendimentoSaudeResponse>("atendimento", "atendimento", f, ct);
    Task<Result<AtendimentoSaudeResponse>> IAtendimentoSaudeService.ObterAsync(long id, CancellationToken ct) => ObterAsync<AtendimentoSaudeResponse>("atendimento", "atendimento", id, ct);
    Task<Result<long>> IAtendimentoSaudeService.CriarAsync(AtendimentoSaudeCreateRequest r, CancellationToken ct) => CriarAsync("atendimento", "criar", r, ct);
    Task<Result> IAtendimentoSaudeService.AtualizarAsync(long id, AtendimentoSaudeUpdateRequest r, CancellationToken ct) => AtualizarAsync("atendimento", "editar", id, r, ct);
    Task<Result> IAtendimentoSaudeService.RegistrarCondutaAsync(long id, RegistrarCondutaRequest r, CancellationToken ct) => AtualizarAsync("atendimento_conduta", "editar", id, r, ct);
    Task<Result> IAtendimentoSaudeService.CancelarAsync(long id, CancellationToken ct) => AtualizarAsync("atendimento_cancelar", "cancelar", id, new { Status = "CANCELADO" }, ct);
    Task<Result<PagedResult<AgendaSaudeResponse>>> IAgendaSaudeService.ListarAsync(AgendaSaudeFiltro f, CancellationToken ct) => ListarAsync<AgendaSaudeResponse>("agenda", "agenda", f, ct);
    Task<Result<long>> IAgendaSaudeService.CriarAsync(AgendaSaudeCreateRequest r, CancellationToken ct) => CriarAsync("agenda", "criar", r, ct);
    Task<Result> IAgendaSaudeService.CancelarAsync(long id, CancellationToken ct) => AtualizarAsync("agenda_cancelar", "cancelar", id, new { Status = "CANCELADA" }, ct);
    Task<Result<PagedResult<FarmaciaProdutoResponse>>> IFarmaciaService.ListarProdutosAsync(UnidadeSaudeFiltro f, CancellationToken ct) => ListarAsync<FarmaciaProdutoResponse>("farmacia_produto", "farmacia", f, ct);
    Task<Result<long>> IFarmaciaService.CriarProdutoAsync(FarmaciaProdutoCreateRequest r, CancellationToken ct) => CriarAsync("farmacia_produto", "produto.criar", r, ct);
    Task<Result<PagedResult<FarmaciaEstoqueResponse>>> IFarmaciaService.ListarEstoqueAsync(UnidadeSaudeFiltro f, CancellationToken ct) => ListarAsync<FarmaciaEstoqueResponse>("farmacia_estoque", "farmacia", f, ct);
    Task<Result<long>> IFarmaciaService.DispensarAsync(FarmaciaDispensacaoCreateRequest r, CancellationToken ct) => r.Quantidade <= 0 || r.PacienteId <= 0 ? Task.FromResult(Fail<long>("Dispensação exige paciente e quantidade positiva.")) : CriarAsync("farmacia_dispensacao", "dispensar", r, ct);
    Task<Result<PagedResult<VacinacaoResponse>>> IVacinacaoService.ListarAsync(PacienteFiltro f, CancellationToken ct) => ListarAsync<VacinacaoResponse>("vacinacao", "vacinacao", f, ct);
    Task<Result<long>> IVacinacaoService.CriarAsync(VacinacaoCreateRequest r, CancellationToken ct) => r.PacienteId <= 0 || r.ProfissionalSaudeId.GetValueOrDefault() <= 0 ? Task.FromResult(Fail<long>("Aplicação de vacina exige paciente e profissional.")) : CriarAsync("vacinacao", "aplicar", r, ct);
    Task<Result<PagedResult<LaboratorioExameResponse>>> ILaboratorioService.ListarAsync(PacienteFiltro f, CancellationToken ct) => ListarAsync<LaboratorioExameResponse>("laboratorio", "laboratorio", f, ct);
    Task<Result<long>> ILaboratorioService.CriarAsync(LaboratorioExameCreateRequest r, CancellationToken ct) => CriarAsync("laboratorio", "criar", r, ct);
    Task<Result> ILaboratorioService.RegistrarResultadoAsync(long id, LaboratorioResultadoRequest r, CancellationToken ct) => AtualizarAsync("laboratorio_resultado", "resultado", id, r, ct);
    Task<Result<PagedResult<RegulacaoSolicitacaoResponse>>> IRegulacaoService.ListarAsync(PacienteFiltro f, CancellationToken ct) => ListarAsync<RegulacaoSolicitacaoResponse>("regulacao", "regulacao", f, ct);
    Task<Result<long>> IRegulacaoService.CriarAsync(RegulacaoSolicitacaoCreateRequest r, CancellationToken ct) => r.PacienteId <= 0 || string.IsNullOrWhiteSpace(r.TipoSolicitacao) || string.IsNullOrWhiteSpace(r.Justificativa) ? Task.FromResult(Fail<long>("Regulação exige paciente, tipo e justificativa.")) : CriarAsync("regulacao", "criar", r, ct);
    Task<Result> IRegulacaoService.AlterarStatusAsync(long id, AlterarStatusRequest r, CancellationToken ct) => string.IsNullOrWhiteSpace(r.Status) ? Task.FromResult(Fail("Movimentação da regulação exige status.")) : AtualizarAsync("regulacao_status", "movimentar", id, r, ct);
    Task<Result<PagedResult<AcsMicroareaResponse>>> IAcsService.ListarMicroareasAsync(UnidadeSaudeFiltro f, CancellationToken ct) => ListarAsync<AcsMicroareaResponse>("acs_microarea", "acs", f, ct);
    Task<Result<long>> IAcsService.CriarMicroareaAsync(AcsMicroareaCreateRequest r, CancellationToken ct) => CriarAsync("acs_microarea", "cadastrar", r, ct);
    Task<Result<long>> IAcsService.CriarDispositivoAsync(AcsDispositivoCreateRequest r, CancellationToken ct) => CriarAsync("acs_dispositivo", "cadastrar", r, ct);
    Task<Result<PagedResult<AcsCadastroDomiciliarResponse>>> IAcsService.ListarDomiciliosAsync(UnidadeSaudeFiltro f, CancellationToken ct) => ListarAsync<AcsCadastroDomiciliarResponse>("acs_domicilio", "acs", f, ct);
    Task<Result<long>> IAcsService.CriarDomicilioAsync(AcsCadastroDomiciliarCreateRequest r, CancellationToken ct) => CriarAsync("acs_domicilio", "cadastrar", r, ct);
    Task<Result<PagedResult<AcsCadastroIndividualResponse>>> IAcsService.ListarIndividuosAsync(PacienteFiltro f, CancellationToken ct) => ListarAsync<AcsCadastroIndividualResponse>("acs_individuo", "acs", f, ct);
    Task<Result<long>> IAcsService.CriarIndividuoAsync(AcsCadastroIndividualCreateRequest r, CancellationToken ct) => CriarAsync("acs_individuo", "cadastrar", r, ct);
    Task<Result<PagedResult<AcsVisitaResponse>>> IAcsService.ListarVisitasAsync(PacienteFiltro f, CancellationToken ct) => ListarAsync<AcsVisitaResponse>("acs_visita", "acs", f, ct);
    Task<Result<long>> IAcsService.RegistrarVisitaAsync(AcsVisitaCreateRequest r, CancellationToken ct) => r.ProfissionalAcsId <= 0 || string.IsNullOrWhiteSpace(r.Desfecho) || (!r.AcsCadastroDomiciliarId.HasValue && !r.PacienteId.HasValue && !r.AcsCadastroIndividualId.HasValue) ? Task.FromResult(Fail<long>("Visita exige ACS, domicílio ou paciente e desfecho.")) : CriarAsync("acs_visita", "registrar", r, ct);

    async Task<Result<AcsSyncLoteResponse>> IAcsSyncService.ProcessarAsync(AcsSyncLoteRequest r, CancellationToken ct)
    {
        var guard = await GuardAsync("acs", "sync", ct, true).ConfigureAwait(false); if (guard.IsFailure) return Fail<AcsSyncLoteResponse>(guard.Error ?? "Operação bloqueada.");
        if (string.IsNullOrWhiteSpace(r.LoteId)) return Fail<AcsSyncLoteResponse>("loteId obrigatório.");
        var existente = await _repo.ObterSyncAsync(TenantId, EntidadeId, r.LoteId, ct).ConfigureAwait(false); if (existente is not null) return Result<AcsSyncLoteResponse>.Success(existente);
        var itens = r.Itens.Select(i => string.IsNullOrWhiteSpace(i.OfflineId) ? new AcsSyncItemResponse(i.TipoItem, i.OfflineId, "ERRO", "offlineId obrigatório") : new AcsSyncItemResponse(i.TipoItem, i.OfflineId, "PROCESSADO")).ToArray();
        var response = new AcsSyncLoteResponse(r.LoteId, itens.Any(i => i.Status == "ERRO") ? "PROCESSADO_COM_ERROS" : "PROCESSADO", itens.Length, itens.Count(i => i.Status == "PROCESSADO"), itens.Count(i => i.Status == "ERRO"), itens);
        await _repo.CriarAsync(TenantId, EntidadeId, ExercicioId, "acs_sync", new { r.LoteId, r.DispositivoId, r.ProfissionalAcsId, Itens = itens, Payload = JsonSerializer.Serialize(r, JsonOptions) }, UsuarioId, ct).ConfigureAwait(false);
        await AuditarAsync("SYNC", "acs_sync", r.LoteId, null, response, ct).ConfigureAwait(false);
        return Result<AcsSyncLoteResponse>.Success(response);
    }
    async Task<Result<AcsSyncLoteResponse>> IAcsSyncService.ObterAsync(string loteId, CancellationToken ct) { var guard = await GuardAsync("acs", "sync", ct).ConfigureAwait(false); if (guard.IsFailure) return Fail<AcsSyncLoteResponse>(guard.Error ?? "Operação bloqueada."); var item = await _repo.ObterSyncAsync(TenantId, EntidadeId, loteId, ct).ConfigureAwait(false); return item is null ? Fail<AcsSyncLoteResponse>("Lote não encontrado.") : Result<AcsSyncLoteResponse>.Success(item); }
    async Task<Result<SaudeDashboardResponse>> ISaudeDashboardService.ObterAsync(CancellationToken ct) { var guard = await GuardAsync("dashboard", "visualizar", ct).ConfigureAwait(false); if (guard.IsFailure) return Fail<SaudeDashboardResponse>(guard.Error ?? "Operação bloqueada."); return Result<SaudeDashboardResponse>.Success(await _repo.DashboardAsync(TenantId, EntidadeId, ct).ConfigureAwait(false)); }
    async Task<Result<byte[]>> ISaudeExportacaoService.ExportarAsync(string recurso, string formato, CancellationToken ct) { var guard = await GuardAsync("exportar", "exportar", ct).ConfigureAwait(false); if (guard.IsFailure) return Fail<byte[]>(guard.Error ?? "Operação bloqueada."); await RegistrarAcessoPessoalAsync(recurso, "EXPORTAR", ct).ConfigureAwait(false); return Result<byte[]>.Success(await _repo.ExportarAsync(TenantId, EntidadeId, recurso, formato, ct).ConfigureAwait(false)); }

    private static void Validate(string recurso, object request)
    {
        switch (request)
        {
            case UnidadeSaudeCreateRequest r: _ = new UnidadeSaude(r.Codigo, r.Nome); break;
            case ProfissionalSaudeCreateRequest r: _ = new ProfissionalSaude(r.PessoaId, r.CodigoProfissional); break;
            case PacienteCreateRequest r: _ = new Paciente(r.PessoaId, r.CodigoPaciente); break;
            case AtendimentoSaudeCreateRequest r: _ = new AtendimentoSaude(r.UnidadeSaudeId, r.PacienteId, "ATD"); break;
            case AgendaSaudeCreateRequest r: _ = new AgendaSaude(r.UnidadeSaudeId, r.DataInicio, r.DataFim); break;
            case FarmaciaDispensacaoCreateRequest r: _ = new FarmaciaDispensacao(r.PacienteId, r.FarmaciaProdutoId, r.Quantidade); break;
            case VacinacaoCreateRequest r: _ = new Vacinacao(r.PacienteId, r.Vacina, r.Dose, r.DataAplicacao); break;
            case LaboratorioExameCreateRequest r: _ = new LaboratorioExame(r.PacienteId, r.TipoExame); break;
            case RegulacaoSolicitacaoCreateRequest r: _ = new RegulacaoSolicitacao(r.PacienteId, r.Justificativa); break;
            case AcsCadastroDomiciliarCreateRequest r: _ = new AcsCadastroDomiciliar(JsonSerializer.Serialize(r.Endereco, JsonOptions), r.Latitude, r.Longitude); break;
            case AcsCadastroIndividualCreateRequest r: _ = new AcsCadastroIndividual(r.PessoaId); break;
            case AcsVisitaCreateRequest r: _ = new AcsVisita(r.ProfissionalAcsId, r.AcsCadastroDomiciliarId, r.AcsCadastroIndividualId, r.PacienteId, r.Latitude, r.Longitude); break;
        }
        if (recurso == "laboratorio_resultado" && request is LaboratorioResultadoRequest lr && (lr.Resultado.Count == 0)) throw new InvalidOperationException("Exame concluído deve ter resultado.");
    }

    private object MaskPayload(object request) => request switch
    {
        PacienteCreateRequest p => p with { CartaoSus = _lgpd.Mask(p.CartaoSus, "CARTAO_SUS"), Alergias = "***", DadosSensiveis = new Dictionary<string, object?> { ["masked"] = true } },
        PacienteUpdateRequest p => p with { CartaoSus = _lgpd.Mask(p.CartaoSus, "CARTAO_SUS"), Alergias = "***", DadosSensiveis = new Dictionary<string, object?> { ["masked"] = true } },
        ProntuarioUpdateRequest => new { masked = true },
        LaboratorioResultadoRequest => new { masked = true },
        AcsCadastroIndividualCreateRequest => new { masked = true },
        _ => request
    };
    private static bool IsSensivel(string recurso) => recurso.Contains("paciente", StringComparison.OrdinalIgnoreCase) || recurso.Contains("prontuario", StringComparison.OrdinalIgnoreCase) || recurso.Contains("atendimento", StringComparison.OrdinalIgnoreCase) || recurso.Contains("acs_individuo", StringComparison.OrdinalIgnoreCase) || recurso.Contains("vacinacao", StringComparison.OrdinalIgnoreCase) || recurso.Contains("laboratorio", StringComparison.OrdinalIgnoreCase);
    private Task AuditarAsync(string acao, string recurso, string chave, object? anterior, object? novo, CancellationToken ct) => _audit.RegistrarAsync("saude", acao, $"sigov.{Tabela(recurso)}", chave, anterior, novo, ct);
    private Task RegistrarAcessoPessoalAsync(string recurso, string chave, CancellationToken ct) => _audit.RegistrarAsync("saude", "ACESSO_DADO_PESSOAL", $"sigov.{Tabela(recurso)}", chave, null, new { recurso, mascarado = true }, ct);
    private static string Tabela(string recurso) => recurso switch { "unidade" => "unidade_saude", "profissional" => "profissional_saude", "prontuario_paciente" => "prontuario", "atendimento_conduta" or "atendimento_cancelar" => "atendimento_saude", "agenda_cancelar" => "agenda_saude", "laboratorio" or "laboratorio_resultado" => "laboratorio_exame", "regulacao" or "regulacao_status" => "regulacao_solicitacao", "acs_domicilio" => "acs_cadastro_domiciliar", "acs_individuo" => "acs_cadastro_individual", _ => recurso };
}
