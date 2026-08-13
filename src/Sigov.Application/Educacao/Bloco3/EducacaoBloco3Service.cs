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
