using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Saas;
using Sigov.Domain.Common;

namespace Sigov.Application.Educacao;

public sealed class EducacaoService : IEscolaService, IAnoLetivoService, ICursoService, ITurmaService, IAlunoService, IMatriculaService, IProfessorService, IFrequenciaService, IAvaliacaoService, IBoletimService, IPreMatriculaService, IEducacensoService, IEducacaoDashboardService, IEducacaoExportacaoService
{
    private readonly IEducacaoRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IPermissionService _permissions;
    private readonly IModuloLicenciamentoService _modulos;
    private readonly IAuditService _audit;
    private readonly ILgpdMaskingService _lgpd;
    private readonly ILogger<EducacaoService> _logger;

    public EducacaoService(IEducacaoRepository repo, ICurrentTenant tenant, ICurrentUser user, IPermissionService permissions, IModuloLicenciamentoService modulos, IAuditService audit, ILgpdMaskingService lgpd, ILogger<EducacaoService> logger)
    { _repo = repo; _tenant = tenant; _user = user; _permissions = permissions; _modulos = modulos; _audit = audit; _lgpd = lgpd; _logger = logger; }

    private long TenantId => _tenant.TenantId ?? 0;
    private long EntidadeId => _tenant.EntidadeId ?? 1;
    private long? ExercicioId => _tenant.ExercicioId;
    private long? UsuarioId => _user.UsuarioId;
    private static Result<T> Fail<T>(string msg) => Result<T>.Failure(msg);
    private static Result Fail(string msg) => Result.Failure(msg);

    private async Task<Result> GuardAsync(string recurso, string acao, CancellationToken ct)
    {
        if (TenantId <= 0) return Fail("Tenant obrigatório para operações de Educação.");
        if (!await ModuloHabilitadoAsync(ct).ConfigureAwait(false)) return await NegarAsync(recurso, acao, "Módulo educação não contratado/habilitado para o tenant.", ct).ConfigureAwait(false);
        if (!_user.IsAuthenticated || !UsuarioId.HasValue) return await NegarAsync(recurso, acao, "Usuário autenticado obrigatório.", ct).ConfigureAwait(false);
        var ok = await _permissions.HasPermissionAsync(UsuarioId.Value, EducacaoPermissoes.Modulo, recurso, acao, ct).ConfigureAwait(false);
        return ok ? Result.Success() : await NegarAsync(recurso, acao, "Usuário sem permissão para a operação de Educação.", ct).ConfigureAwait(false);
    }

    private async Task<Result> NegarAsync(string recurso, string acao, string motivo, CancellationToken ct)
    {
        await _audit.RegistrarAsync("educacao", "ACESSO_NEGADO", "seguranca_evento", recurso, null, new { recurso, acao, motivo, usuarioId = UsuarioId, tenantId = TenantId }, ct).ConfigureAwait(false);
        return Fail(motivo);
    }

    private Task<bool> ModuloHabilitadoAsync(CancellationToken ct) => _modulos.IsModuleEnabledAsync(TenantId, EducacaoPermissoes.Modulo, ct);

    private async Task<Result<long>> CriarAsync(string recurso, string acao, object request, CancellationToken ct)
    {
        var guard = await GuardAsync(recurso, acao, ct).ConfigureAwait(false);
        if (guard.IsFailure) return Fail<long>(guard.Error ?? "Operação bloqueada.");
        try
        {
            var id = await _repo.CriarAsync(TenantId, EntidadeId, ExercicioId, recurso, request, UsuarioId, ct).ConfigureAwait(false);
            await _audit.RegistrarAsync("educacao", "CRIAR", $"sigov.{Tabela(recurso)}", id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, AuditPayload(request), ct).ConfigureAwait(false);
            return Result<long>.Success(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar recurso de Educação {Recurso} no tenant {TenantId}.", recurso, TenantId);
            return Fail<long>("Falha ao executar operação de Educação.");
        }
    }

    private async Task<Result> AtualizarAsync(string recurso, string acao, long id, object request, CancellationToken ct)
    {
        var guard = await GuardAsync(recurso, acao, ct).ConfigureAwait(false);
        if (guard.IsFailure) return guard;
        try
        {
            var anterior = await _repo.ObterAsync<object>(TenantId, EntidadeId, recurso, id, ct).ConfigureAwait(false);
            await _repo.AtualizarAsync(TenantId, EntidadeId, recurso, id, request, UsuarioId, ct).ConfigureAwait(false);
            await _audit.RegistrarAsync("educacao", acao.ToUpperInvariant(), $"sigov.{Tabela(recurso)}", id.ToString(System.Globalization.CultureInfo.InvariantCulture), anterior, AuditPayload(request), ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar recurso de Educação {Recurso} Id={Id}.", recurso, id);
            return Fail("Falha ao executar operação de Educação.");
        }
    }

    private async Task<Result<PagedResult<T>>> ListarAsync<T>(string recurso, string permissaoRecurso, object filtro, CancellationToken ct)
    {
        var guard = await GuardAsync(permissaoRecurso, "visualizar", ct).ConfigureAwait(false);
        if (guard.IsFailure) return Fail<PagedResult<T>>(guard.Error ?? "Operação bloqueada.");
        var page = await _repo.ListarAsync<T>(TenantId, EntidadeId, recurso, filtro, ct).ConfigureAwait(false);
        if (recurso is "aluno" or "professor") await RegistrarAcessoPessoalAsync(recurso, "LISTAR", ct).ConfigureAwait(false);
        return Result<PagedResult<T>>.Success(page);
    }

    private async Task<Result<T>> ObterAsync<T>(string recurso, string permissaoRecurso, long id, CancellationToken ct) where T : class
    {
        var guard = await GuardAsync(permissaoRecurso, "visualizar", ct).ConfigureAwait(false);
        if (guard.IsFailure) return Fail<T>(guard.Error ?? "Operação bloqueada.");
        var item = await _repo.ObterAsync<T>(TenantId, EntidadeId, recurso, id, ct).ConfigureAwait(false);
        if (item is null) return Fail<T>("Registro não encontrado.");
        if (recurso is "aluno" or "responsavel_aluno" or "professor") await RegistrarAcessoPessoalAsync(recurso, id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
        return Result<T>.Success(item);
    }

    private async Task<Result> ExcluirAsync(string recurso, string permissaoRecurso, long id, CancellationToken ct)
    {
        var guard = await GuardAsync(permissaoRecurso, "excluir", ct).ConfigureAwait(false);
        if (guard.IsFailure) return guard;
        await _repo.ExcluirAsync(TenantId, EntidadeId, recurso, id, UsuarioId, ct).ConfigureAwait(false);
        await _audit.RegistrarAsync("educacao", "EXCLUIR", $"sigov.{Tabela(recurso)}", id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, new { id }, ct).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task RegistrarAcessoPessoalAsync(string recurso, string chave, CancellationToken ct)
    {
        await _audit.RegistrarAsync("educacao", "ACESSO_DADO_PESSOAL", $"sigov.{Tabela(recurso)}", chave, null, new { recurso, mascarado = _lgpd.Mask("00000000000", "CPF") }, ct).ConfigureAwait(false);
    }

    private static string Tabela(string recurso) => recurso.Replace('-', '_');

    private static object? AuditPayload(object? value)
    {
        if (value is null) return null;
        if (value is DateOnly date) return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (value is DateTime dt) return dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (value is string or bool or int or long or decimal or double) return value;
        if (value is IDictionary<string, object?> dict) return dict.ToDictionary(k => k.Key, v => AuditPayload(v.Value), StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in value.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            result[prop.Name] = AuditPayload(prop.GetValue(value));
        }

        return result;
    }

    Task<Result<PagedResult<EscolaResponse>>> IEscolaService.ListarAsync(EscolaFiltro filtro, CancellationToken ct) => ListarAsync<EscolaResponse>("escola", "escola", filtro, ct);
    Task<Result<EscolaResponse>> IEscolaService.ObterAsync(long id, CancellationToken ct) => ObterAsync<EscolaResponse>("escola", "escola", id, ct);
    Task<Result<long>> IEscolaService.CriarAsync(EscolaCreateRequest request, CancellationToken ct) => CriarAsync("escola", "criar", request, ct);
    Task<Result> IEscolaService.AtualizarAsync(long id, EscolaUpdateRequest request, CancellationToken ct) => AtualizarAsync("escola", "editar", id, request, ct);
    Task<Result> IEscolaService.ExcluirAsync(long id, CancellationToken ct) => ExcluirAsync("escola", "escola", id, ct);

    Task<Result<PagedResult<AnoLetivoResponse>>> IAnoLetivoService.ListarAsync(EscolaFiltro filtro, CancellationToken ct) => ListarAsync<AnoLetivoResponse>("ano_letivo", "escola", filtro, ct);
    Task<Result<long>> IAnoLetivoService.CriarAsync(AnoLetivoCreateRequest request, CancellationToken ct) => request.DataFim < request.DataInicio ? Task.FromResult(Fail<long>("Data final do ano letivo não pode ser anterior à inicial.")) : CriarAsync("ano_letivo", "criar", request, ct);
    Task<Result> IAnoLetivoService.EncerrarAsync(long id, CancellationToken ct) => AtualizarAsync("ano_letivo", "editar", id, new { Status = "ENCERRADO" }, ct);

    Task<Result<PagedResult<CursoResponse>>> ICursoService.ListarAsync(EscolaFiltro filtro, CancellationToken ct) => ListarAsync<CursoResponse>("curso", "turma", filtro, ct);
    Task<Result<long>> ICursoService.CriarAsync(CursoCreateRequest request, CancellationToken ct) => CriarAsync("curso", "criar", request, ct);
    Task<Result<long>> ICursoService.CriarSerieAsync(long cursoId, SerieAnoCreateRequest request, CancellationToken ct) => CriarAsync("serie_ano", "criar", new { CursoId = cursoId, request.Codigo, request.Nome, request.Ordem }, ct);

    Task<Result<PagedResult<TurmaResponse>>> ITurmaService.ListarAsync(TurmaFiltro filtro, CancellationToken ct) => ListarAsync<TurmaResponse>("turma", "turma", filtro, ct);
    Task<Result<TurmaResponse>> ITurmaService.ObterAsync(long id, CancellationToken ct) => ObterAsync<TurmaResponse>("turma", "turma", id, ct);
    Task<Result<long>> ITurmaService.CriarAsync(TurmaCreateRequest request, CancellationToken ct) => request.Capacidade <= 0 ? Task.FromResult(Fail<long>("Capacidade da turma deve ser maior que zero.")) : CriarAsync("turma", "criar", request, ct);
    Task<Result> ITurmaService.AtualizarAsync(long id, TurmaUpdateRequest request, CancellationToken ct) => AtualizarAsync("turma", "editar", id, request, ct);
    Task<Result> ITurmaService.ExcluirAsync(long id, CancellationToken ct) => ExcluirAsync("turma", "turma", id, ct);

    Task<Result<PagedResult<AlunoResumoResponse>>> IAlunoService.ListarAsync(AlunoFiltro filtro, CancellationToken ct) => ListarAsync<AlunoResumoResponse>("aluno", "aluno", filtro, ct);
    Task<Result<AlunoDetalheResponse>> IAlunoService.ObterAsync(long id, CancellationToken ct) => ObterAsync<AlunoDetalheResponse>("aluno", "aluno", id, ct);
    Task<Result<long>> IAlunoService.CriarAsync(AlunoCreateRequest request, CancellationToken ct) => request.PessoaId <= 0 ? Task.FromResult(Fail<long>("Aluno deve estar vinculado a uma pessoa.")) : CriarAsync("aluno", "criar", request, ct);
    Task<Result> IAlunoService.AtualizarAsync(long id, AlunoUpdateRequest request, CancellationToken ct) => AtualizarAsync("aluno", "editar", id, request, ct);
    Task<Result> IAlunoService.ExcluirAsync(long id, CancellationToken ct) => ExcluirAsync("aluno", "aluno", id, ct);
    Task<Result<long>> IAlunoService.AdicionarResponsavelAsync(long alunoId, ResponsavelAlunoRequest request, CancellationToken ct) => CriarAsync("responsavel_aluno", "criar", new { AlunoId = alunoId, request.PessoaId, request.Parentesco, request.ResponsavelLegal, request.Financeiro, request.AutorizadoBuscar, request.ContatoEmergencia }, ct);

    Task<Result<PagedResult<MatriculaResponse>>> IMatriculaService.ListarAsync(MatriculaFiltro filtro, CancellationToken ct) => ListarAsync<MatriculaResponse>("matricula", "matricula", filtro, ct);
    Task<Result<MatriculaResponse>> IMatriculaService.ObterAsync(long id, CancellationToken ct) => ObterAsync<MatriculaResponse>("matricula", "matricula", id, ct);
    Task<Result<long>> IMatriculaService.CriarAsync(MatriculaCreateRequest request, CancellationToken ct) => request.AlunoId <= 0 || request.EscolaId <= 0 || request.AnoLetivoId <= 0 || request.TurmaId <= 0 ? Task.FromResult(Fail<long>("Matrícula exige aluno, escola, ano letivo e turma.")) : CriarAsync("matricula", "criar", request, ct);
    async Task<Result> IMatriculaService.ConfirmarAsync(long id, EducacaoConfirmarMatriculaRequest request, CancellationToken ct)
    {
        var matricula = await ObterAsync<MatriculaResponse>("matricula", "matricula", id, ct).ConfigureAwait(false);
        if (matricula.IsFailure || matricula.Value is null) return Fail(matricula.Error ?? "Matrícula não encontrada.");
        if (matricula.Value.Status.Equals("CANCELADA", StringComparison.OrdinalIgnoreCase)) return Fail("Matrícula cancelada não pode ser confirmada.");
        if (matricula.Value.Status.Equals("CONFIRMADA", StringComparison.OrdinalIgnoreCase)) return Result.Success();
        return await AtualizarAsync("matricula", "confirmar", id, new { Status = "CONFIRMADA", request.Observacao }, ct).ConfigureAwait(false);
    }
    Task<Result> IMatriculaService.CancelarAsync(long id, CancelarMatriculaRequest request, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(request.Motivo)
            ? Task.FromResult(Fail("Cancelamento de matrícula exige justificativa."))
            : AtualizarAsync("matricula", "cancelar", id, new { Status = "CANCELADA", Motivo = request.Motivo.Trim() }, ct);
    Task<Result> IMatriculaService.TransferirAsync(long id, TransferirMatriculaRequest request, CancellationToken ct) =>
        request.NovaTurmaId <= 0 || string.IsNullOrWhiteSpace(request.Motivo)
            ? Task.FromResult(Fail("Transferência exige nova turma e justificativa."))
            : AtualizarAsync("matricula", "transferir", id, new { Status = "TRANSFERIDA", request.NovaTurmaId, Motivo = request.Motivo.Trim() }, ct);

    Task<Result<PagedResult<ProfessorResponse>>> IProfessorService.ListarAsync(EscolaFiltro filtro, CancellationToken ct) => ListarAsync<ProfessorResponse>("professor", "professor", filtro, ct);
    Task<Result<long>> IProfessorService.CriarAsync(ProfessorCreateRequest request, CancellationToken ct) => CriarAsync("professor", "criar", request, ct);
    Task<Result<long>> IProfessorService.VincularTurmaAsync(long professorId, ProfessorTurmaRequest request, CancellationToken ct) => CriarAsync("professor_turma", "criar", new { ProfessorId = professorId, request.TurmaId, request.ComponenteCurricular, request.CargaHorariaSemanal }, ct);

    Task<Result<PagedResult<FrequenciaResponse>>> IFrequenciaService.ListarAsync(FrequenciaFiltro filtro, CancellationToken ct) => ListarAsync<FrequenciaResponse>("diario_frequencia", "frequencia", filtro, ct);
    Task<Result<long>> IFrequenciaService.CriarAsync(FrequenciaCreateRequest request, CancellationToken ct) =>
        request.TurmaId <= 0 || request.AlunoId <= 0
            ? Task.FromResult(Fail<long>("Frequência exige turma e aluno com matrícula ativa."))
            : CriarAsync("diario_frequencia", "criar", request, ct);

    Task<Result<PagedResult<AvaliacaoResponse>>> IAvaliacaoService.ListarAsync(TurmaFiltro filtro, CancellationToken ct) => ListarAsync<AvaliacaoResponse>("avaliacao", "avaliacao", filtro, ct);
    Task<Result<long>> IAvaliacaoService.CriarAsync(AvaliacaoCreateRequest request, CancellationToken ct) => request.ValorMaximo <= 0m ? Task.FromResult(Fail<long>("Valor máximo da avaliação deve ser positivo.")) : CriarAsync("avaliacao", "criar", request, ct);
    Task<Result<long>> IAvaliacaoService.RegistrarNotaAsync(long avaliacaoId, NotaCreateRequest request, CancellationToken ct) => request.Valor < 0m ? Task.FromResult(Fail<long>("Nota não pode ser negativa.")) : CriarAsync("nota", "criar", new { AvaliacaoId = avaliacaoId, request.AlunoId, request.Valor, request.Observacao }, ct);

    async Task<Result<BoletimResponse>> IBoletimService.ObterAsync(long alunoId, CancellationToken ct)
    {
        if (alunoId <= 0) return Fail<BoletimResponse>("Aluno inválido.");
        var guard = await GuardAsync("boletim", "visualizar", ct).ConfigureAwait(false);
        if (guard.IsFailure) return Fail<BoletimResponse>(guard.Error ?? "Operação bloqueada.");
        await RegistrarAcessoPessoalAsync("boletim", "CONSULTAR", ct).ConfigureAwait(false);
        return Result<BoletimResponse>.Success(await _repo.ObterBoletimAsync(TenantId, EntidadeId, alunoId, ct).ConfigureAwait(false));
    }

    Task<Result<PagedResult<PreMatriculaResponse>>> IPreMatriculaService.ListarAsync(PreMatriculaFiltro filtro, CancellationToken ct) => ListarAsync<PreMatriculaResponse>("pre_matricula_inscricao", "pre_matricula", filtro, ct);
    Task<Result<long>> IPreMatriculaService.CriarAsync(PreMatriculaCreateRequest request, CancellationToken ct) => CriarAsync("pre_matricula_inscricao", "criar", request, ct);
    Task<Result> IPreMatriculaService.ConverterAsync(long id, ConverterPreMatriculaRequest request, CancellationToken ct) => AtualizarAsync("pre_matricula_inscricao", "converter", id, new { Status = "CONVERTIDA_MATRICULA", request.EscolaId, request.AnoLetivoId, request.TurmaId, request.NumeroMatricula }, ct);
    Task<Result> IPreMatriculaService.IndeferirAsync(long id, CancellationToken ct) => AtualizarAsync("pre_matricula_inscricao", "editar", id, new { Status = "INDEFERIDA" }, ct);

    Task<Result<PagedResult<EducacensoRegistroResponse>>> IEducacensoService.ListarAsync(EscolaFiltro filtro, CancellationToken ct) => ListarAsync<EducacensoRegistroResponse>("educacenso_registro", "educacenso", filtro, ct);
    Task<Result<long>> IEducacensoService.CriarAsync(EducacensoRegistroRequest request, CancellationToken ct) => CriarAsync("educacenso_registro", "registrar", request, ct);
    Task<Result> IEducacensoService.ValidarDevAsync(long id, CancellationToken ct) => AtualizarAsync("educacenso_registro", "registrar", id, new { Status = "VALIDADO" }, ct);

    async Task<Result<EducacaoDashboardResponse>> IEducacaoDashboardService.ObterAsync(CancellationToken ct)
    {
        var guard = await GuardAsync("dashboard", "visualizar", ct).ConfigureAwait(false);
        if (guard.IsFailure) return Fail<EducacaoDashboardResponse>(guard.Error ?? "Operação bloqueada.");
        return Result<EducacaoDashboardResponse>.Success(await _repo.DashboardAsync(TenantId, EntidadeId, ct).ConfigureAwait(false));
    }

    async Task<Result<byte[]>> IEducacaoExportacaoService.ExportarAsync(string recurso, string formato, CancellationToken ct)
    {
        var guard = await GuardAsync("exportar", "exportar", ct).ConfigureAwait(false);
        if (guard.IsFailure) return Fail<byte[]>(guard.Error ?? "Operação bloqueada.");
        await RegistrarAcessoPessoalAsync(recurso, "EXPORTAR", ct).ConfigureAwait(false);
        return Result<byte[]>.Success(await _repo.ExportarAsync(TenantId, EntidadeId, recurso, formato, ct).ConfigureAwait(false));
    }
}
