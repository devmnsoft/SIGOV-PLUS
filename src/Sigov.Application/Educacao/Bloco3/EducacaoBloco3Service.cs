using Sigov.Application.Abstractions;
using Sigov.Domain.Common;

namespace Sigov.Application.Educacao.Bloco3;

public sealed class EducacaoBloco3Service : IEducacaoSecretariaService, IEducacaoDocumentoEscolarService,
    IEducacaoTransferenciaService, IEducacaoSolicitacaoEscolarService, IEducacaoDiarioClasseService,
    IEducacaoDiarioFrequenciaService, IEducacaoDiarioFechamentoService, IEducacaoPortalService,
    IEducacaoPortalSolicitacaoService, IEducacaoComunicadoService
{
    private static readonly HashSet<string> Recursos = new(StringComparer.OrdinalIgnoreCase)
    {
        "documento", "documento-frequencia", "solicitacao", "pendencia", "transferencia", "ocorrencia", "diario", "aula",
        "conteudo", "frequencia", "avaliacao", "reposicao", "fechamento", "diario-pendencia",
        "portal-aluno", "portal-boletim", "portal-frequencia", "portal-ocorrencia", "portal-solicitacao",
        "portal-comunicado", "portal-mensagem", "portal-vinculo"
    };
    private readonly IEducacaoBloco3Repository _repository;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly ICorrelationIdProvider _correlation;

    public EducacaoBloco3Service(IEducacaoBloco3Repository repository, ICurrentTenant tenant, ICurrentUser user, ICorrelationIdProvider correlation)
    { _repository = repository; _tenant = tenant; _user = user; _correlation = correlation; }

    private bool Administrativo => _user.Roles.Any(x => x.Equals("admin", StringComparison.OrdinalIgnoreCase) || x.Equals("superadmin", StringComparison.OrdinalIgnoreCase))
        || _user.Permissions.Any(x => x.StartsWith("educacao.secretaria", StringComparison.OrdinalIgnoreCase));

    public async Task<Result<IReadOnlyCollection<T>>> ListarAsync<T>(string recurso, EducacaoBloco3Filtro filtro, CancellationToken ct)
    {
        var guard = Guard(recurso);
        if (guard is not null) return Result<IReadOnlyCollection<T>>.Failure(guard);
        if (recurso == "portal-vinculo" && !Administrativo) return Result<IReadOnlyCollection<T>>.Failure("Permissão administrativa é obrigatória para consultar vínculos.");
        var itens = await _repository.ListarAsync<T>(_tenant.TenantId!.Value, recurso, filtro, _user.UsuarioId, Administrativo, ct).ConfigureAwait(false);
        return Result<IReadOnlyCollection<T>>.Success(itens);
    }

    public async Task<Result<long>> CriarAsync(string recurso, object request, CancellationToken ct)
    {
        var guard = Guard(recurso);
        if (guard is not null) return Result<long>.Failure(guard);
        if ((recurso == "portal-vinculo" || recurso == "portal-comunicado") && !Administrativo) return Result<long>.Failure("Permissão administrativa é obrigatória para esta operação.");
        if (new[] { "diario", "aula", "conteudo", "frequencia", "avaliacao", "reposicao" }.Contains(recurso, StringComparer.OrdinalIgnoreCase) && !Pode("educacao.diario.editar") && !Pode("educacao.diario.lancar")) return Result<long>.Failure("Permissão para lançar ou editar o diário é obrigatória.");
        var erro = Validar(recurso, request);
        if (erro is not null) return Result<long>.Failure(erro);
        var alunoId = LerLong(request, "AlunoId");
        var matriculaId = LerLong(request, "MatriculaId");
        if ((recurso == "documento" || recurso == "documento-frequencia" || recurso == "transferencia") && alunoId.HasValue && matriculaId.HasValue
            && !await _repository.MatriculaValidaAsync(_tenant.TenantId!.Value, alunoId.Value, matriculaId.Value, ct).ConfigureAwait(false))
            return Result<long>.Failure("A matrícula deve pertencer ao aluno e estar ativa ou concluída.");
        if (!Administrativo && alunoId.HasValue && !await _repository.UsuarioVinculadoAsync(_tenant.TenantId!.Value, _user.UsuarioId!.Value, alunoId.Value, ct).ConfigureAwait(false))
            return Result<long>.Failure("Acesso negado: o aluno não está vinculado ao usuário autenticado.");
        var id = await _repository.CriarAsync(_tenant.TenantId!.Value, _tenant.EntidadeId ?? 1, _tenant.ExercicioId, recurso, request, _user.UsuarioId!.Value, _correlation.CorrelationId.ToString(), ct).ConfigureAwait(false);
        return Result<long>.Success(id);
    }

    public async Task<Result> DecidirAsync(string recurso, long id, string status, string justificativa, CancellationToken ct)
    {
        var guard = Guard(recurso);
        if (guard is not null) return Result.Failure(guard);
        if (!Administrativo) return Result.Failure("Permissão administrativa da Secretaria Escolar é obrigatória.");
        if (string.IsNullOrWhiteSpace(justificativa)) return Result.Failure("Justificativa é obrigatória para decisão, fechamento ou reabertura.");
        var destino = status.ToUpperInvariant();
        var atual = await _repository.ObterStatusAsync(_tenant.TenantId!.Value, recurso, id, ct).ConfigureAwait(false);
        if (atual is null) return Result.Failure("Registro não encontrado para o tenant informado.");
        if (!TransicaoPermitida(recurso, atual, destino)) return Result.Failure($"Transição de {atual} para {destino} não permitida.");
        if (recurso == "diario" && destino == "FECHADO"
            && !await _repository.DiarioProntoParaFechamentoAsync(_tenant.TenantId.Value, id, ct).ConfigureAwait(false))
            return Result.Failure("O diário somente pode ser fechado quando todas as aulas possuem conteúdo e frequência lançados.");
        await _repository.AlterarStatusAsync(_tenant.TenantId!.Value, recurso, id, destino, justificativa.Trim(), _user.UsuarioId!.Value, _correlation.CorrelationId.ToString(), ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<EducacaoDiarioConferenciaDto>> ConferirDiarioAsync(long diarioId, CancellationToken ct)
    {
        var guard = Guard("diario");
        if (guard is not null) return Result<EducacaoDiarioConferenciaDto>.Failure(guard);
        if (!Pode("educacao.diario.conferir")) return Result<EducacaoDiarioConferenciaDto>.Failure("Permissão para conferir o período é obrigatória.");
        var conferencia = await _repository.ConferirDiarioAsync(_tenant.TenantId!.Value, diarioId, ct).ConfigureAwait(false);
        return conferencia is null
            ? Result<EducacaoDiarioConferenciaDto>.Failure("Diário não encontrado no contexto autorizado.")
            : Result<EducacaoDiarioConferenciaDto>.Success(conferencia);
    }

    public async Task<Result<long>> FecharDiarioAsync(long diarioId, EducacaoDiarioFechamentoRequest request, CancellationToken ct)
    {
        var guard = Guard("diario");
        if (guard is not null) return Result<long>.Failure(guard);
        if (!Pode("educacao.diario.fechar")) return Result<long>.Failure("Permissão específica para fechar o período é obrigatória.");
        if (string.IsNullOrWhiteSpace(request.Observacao)) return Result<long>.Failure("A observação do fechamento é obrigatória.");
        if (string.IsNullOrWhiteSpace(request.TokenConferencia)) return Result<long>.Failure("Faça uma nova conferência antes de fechar.");
        try
        {
            var id = await _repository.FecharDiarioAsync(_tenant.TenantId!.Value, diarioId, request.TokenConferencia, request.Observacao.Trim(), _user.UsuarioId!.Value, _correlation.CorrelationId.ToString(), ct).ConfigureAwait(false);
            return Result<long>.Success(id);
        }
        catch (InvalidOperationException ex) { return Result<long>.Failure(ex.Message); }
    }

    public async Task<Result> ReabrirDiarioAsync(long diarioId, EducacaoDiarioReaberturaRequest request, CancellationToken ct)
    {
        var guard = Guard("diario");
        if (guard is not null) return Result.Failure(guard);
        if (!Pode("educacao.diario.reabrir")) return Result.Failure("Permissão específica para reabrir o período é obrigatória.");
        if (string.IsNullOrWhiteSpace(request.Justificativa)) return Result.Failure("A justificativa da reabertura é obrigatória.");
        try { await _repository.ReabrirDiarioAsync(_tenant.TenantId!.Value, diarioId, request.Justificativa.Trim(), _user.UsuarioId!.Value, _correlation.CorrelationId.ToString(), ct).ConfigureAwait(false); return Result.Success(); }
        catch (InvalidOperationException ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result<IReadOnlyCollection<EducacaoDiarioFechamentoDto>>> HistoricoFechamentoAsync(long diarioId, CancellationToken ct)
    {
        var guard = Guard("diario");
        if (guard is not null) return Result<IReadOnlyCollection<EducacaoDiarioFechamentoDto>>.Failure(guard);
        if (!Pode("educacao.diario.conferir")) return Result<IReadOnlyCollection<EducacaoDiarioFechamentoDto>>.Failure("Permissão para consultar o histórico é obrigatória.");
        return Result<IReadOnlyCollection<EducacaoDiarioFechamentoDto>>.Success(await _repository.HistoricoFechamentoAsync(_tenant.TenantId!.Value, diarioId, ct).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyCollection<EducacaoPortalAlunoDto>>> ListarAlunosAsync(CancellationToken ct)
    {
        var guard = Guard("portal-aluno");
        if (guard is not null) return Result<IReadOnlyCollection<EducacaoPortalAlunoDto>>.Failure(guard);
        return Result<IReadOnlyCollection<EducacaoPortalAlunoDto>>.Success(
            await _repository.ListarAlunosAutorizadosAsync(_tenant.TenantId!.Value, _user.UsuarioId!.Value, ct).ConfigureAwait(false));
    }

    public async Task<Result<EducacaoPortalVidaEscolarDto>> ObterVidaEscolarAsync(long alunoId, CancellationToken ct)
    {
        var guard = Guard("portal-aluno");
        if (guard is not null) return Result<EducacaoPortalVidaEscolarDto>.Failure(guard);
        var item = await _repository.ObterVidaEscolarAsync(_tenant.TenantId!.Value, _user.UsuarioId!.Value, alunoId, ct).ConfigureAwait(false);
        return item is null ? Result<EducacaoPortalVidaEscolarDto>.Failure("Aluno não encontrado no vínculo ativo do usuário autenticado.") : Result<EducacaoPortalVidaEscolarDto>.Success(item);
    }

    public async Task<Result<EducacaoPortalBoletimDto>> ObterBoletimAsync(long alunoId, CancellationToken ct)
    {
        var guard = Guard("portal-boletim");
        if (guard is not null) return Result<EducacaoPortalBoletimDto>.Failure(guard);
        var item = await _repository.ObterBoletimPortalAsync(_tenant.TenantId!.Value, _user.UsuarioId!.Value, alunoId, ct).ConfigureAwait(false);
        return item is null ? Result<EducacaoPortalBoletimDto>.Failure("Boletim não publicado ou aluno fora do vínculo ativo.") : Result<EducacaoPortalBoletimDto>.Success(item);
    }

    public async Task<Result<EducacaoPortalCienciaDto>> ConfirmarCienciaAsync(long comunicadoId, long alunoId, CancellationToken ct)
    {
        var guard = Guard("portal-comunicado");
        if (guard is not null) return Result<EducacaoPortalCienciaDto>.Failure(guard);
        try { return Result<EducacaoPortalCienciaDto>.Success(await _repository.RegistrarCienciaAsync(_tenant.TenantId!.Value, _user.UsuarioId!.Value, comunicadoId, alunoId, ct).ConfigureAwait(false)); }
        catch (InvalidOperationException ex) { return Result<EducacaoPortalCienciaDto>.Failure(ex.Message); }
    }

    public async Task<Result> RegistrarLeituraAsync(long comunicadoId, long alunoId, CancellationToken ct)
    {
        var guard = Guard("portal-comunicado"); if (guard is not null) return Result.Failure(guard);
        try { await _repository.RegistrarLeituraAsync(_tenant.TenantId!.Value,_user.UsuarioId!.Value,comunicadoId,alunoId,ct).ConfigureAwait(false); return Result.Success(); }
        catch(InvalidOperationException ex){ return Result.Failure(ex.Message); }
    }

    public async Task<Result> AlterarVinculoAsync(long vinculoId, bool ativar, string justificativa, CancellationToken ct)
    {
        var guard = Guard("portal-vinculo");
        if (guard is not null) return Result.Failure(guard);
        if (!Administrativo) return Result.Failure("Permissão administrativa da Secretaria Escolar é obrigatória.");
        if (string.IsNullOrWhiteSpace(justificativa)) return Result.Failure("A justificativa é obrigatória para ativar ou revogar acesso.");
        try { await _repository.AlterarVinculoAsync(_tenant.TenantId!.Value, vinculoId, ativar ? "ATIVO" : "REVOGADO", justificativa.Trim(), _user.UsuarioId!.Value, _correlation.CorrelationId.ToString(), ct).ConfigureAwait(false); return Result.Success(); }
        catch (InvalidOperationException ex) { return Result.Failure(ex.Message); }
    }

    private bool Pode(string permissao) => Administrativo || _user.Permissions.Any(x => x.Equals(permissao, StringComparison.OrdinalIgnoreCase));

    private string? Guard(string recurso)
    {
        if (!Recursos.Contains(recurso)) return "Recurso educacional inválido.";
        if (!_tenant.TenantId.HasValue || _tenant.TenantId.Value <= 0) return "Tenant obrigatório.";
        if (!_user.IsAuthenticated || !_user.UsuarioId.HasValue) return "Usuário autenticado obrigatório.";
        return null;
    }

    private static string? Validar(string recurso, object request)
    {
        if ((recurso == "documento" || recurso == "documento-frequencia" || recurso == "solicitacao" || recurso == "pendencia" || recurso == "transferencia" || recurso == "ocorrencia" || recurso == "portal-solicitacao")
            && (!LerLong(request, "AlunoId").HasValue || LerLong(request, "AlunoId") <= 0)) return "Aluno é obrigatório.";
        if (recurso == "portal-vinculo" && (!LerLong(request, "UsuarioVinculadoId").HasValue || !LerLong(request, "AlunoId").HasValue || !LerLong(request, "ResponsavelId").HasValue))
            return "Usuário, aluno e vínculo cadastral do responsável são obrigatórios para a liberação.";
        if (recurso == "documento-frequencia" && LerValor(request, "Inicio") is DateOnly inicio && LerValor(request, "Fim") is DateOnly fim && inicio > fim)
            return "O início do período de frequência não pode ser posterior ao fim.";
        if ((recurso == "solicitacao" || recurso == "pendencia" || recurso == "ocorrencia" || recurso == "portal-solicitacao")
            && string.IsNullOrWhiteSpace(LerString(request, "Tipo"))) return "Tipo é obrigatório.";
        if ((recurso.Contains("solicitacao", StringComparison.OrdinalIgnoreCase) || recurso == "ocorrencia") && string.IsNullOrWhiteSpace(LerString(request, "Descricao"))) return "Descrição é obrigatória.";
        if (recurso == "transferencia" && !LerLong(request, "EscolaDestinoId").HasValue && !LerLong(request, "TurmaDestinoId").HasValue && string.IsNullOrWhiteSpace(LerString(request, "JustificativaExterna"))) return "Informe escola/turma de destino ou justificativa externa.";
        if (recurso == "aula" && LerValor(request, "DataAula") is null) return "Data da aula é obrigatória.";
        if (recurso == "conteudo" && string.IsNullOrWhiteSpace(LerString(request, "Conteudo"))) return "Conteúdo ministrado é obrigatório.";
        if (recurso == "frequencia")
        {
            var itens = LerValor(request, "Alunos") as System.Collections.IEnumerable;
            if (itens is null) return "A chamada deve conter alunos.";
            var permitidos = new HashSet<string>(new[] { "PRESENTE", "FALTA", "JUSTIFICADA", "ABONADA" }, StringComparer.OrdinalIgnoreCase);
            foreach (var item in itens)
                if (item is null || !permitidos.Contains(LerString(item, "Status") ?? string.Empty)) return "Status de frequência inválido.";
        }
        return null;
    }
    private static bool TransicaoPermitida(string recurso, string atual, string destino)
    {
        var chave = atual.ToUpperInvariant() + ":" + destino;
        return recurso switch
        {
            "solicitacao" => new[] { "ABERTA:DEFERIDA", "ABERTA:INDEFERIDA", "EM_ANALISE:DEFERIDA", "EM_ANALISE:INDEFERIDA", "DEFERIDA:CONCLUIDA" }.Contains(chave),
            "transferencia" => new[] { "SOLICITADA:APROVADA", "SOLICITADA:REPROVADA", "EM_ANALISE:APROVADA", "EM_ANALISE:REPROVADA", "APROVADA:CONCLUIDA" }.Contains(chave),
            "pendencia" => atual.Equals("PENDENTE", StringComparison.OrdinalIgnoreCase) && destino == "RESOLVIDA",
            "diario" => (destino == "FECHADO" && new[] { "ABERTO", "PENDENTE", "REABERTO" }.Contains(atual.ToUpperInvariant())) || (destino == "REABERTO" && atual.Equals("FECHADO", StringComparison.OrdinalIgnoreCase)),
            "portal-solicitacao" => new[] { "ABERTA:EM_ANALISE", "ABERTA:RESPONDIDA", "EM_ANALISE:RESPONDIDA", "RESPONDIDA:CONCLUIDA" }.Contains(chave),
            _ => false
        };
    }
    private static object? LerValor(object value, string nome) => value.GetType().GetProperty(nome)?.GetValue(value);
    private static string? LerString(object value, string nome) => Convert.ToString(LerValor(value, nome), System.Globalization.CultureInfo.InvariantCulture);
    private static long? LerLong(object value, string nome) => LerValor(value, nome) is null ? null : Convert.ToInt64(LerValor(value, nome), System.Globalization.CultureInfo.InvariantCulture);
}
