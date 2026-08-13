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
        "documento", "solicitacao", "pendencia", "transferencia", "ocorrencia", "diario", "aula",
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
        if ((recurso == "documento" || recurso == "transferencia") && alunoId.HasValue && matriculaId.HasValue
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
        await _repository.AlterarStatusAsync(_tenant.TenantId!.Value, recurso, id, status.ToUpperInvariant(), justificativa.Trim(), _user.UsuarioId!.Value, _correlation.CorrelationId.ToString(), ct).ConfigureAwait(false);
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
    private static object? LerValor(object value, string nome) => value.GetType().GetProperty(nome)?.GetValue(value);
    private static string? LerString(object value, string nome) => Convert.ToString(LerValor(value, nome), System.Globalization.CultureInfo.InvariantCulture);
    private static long? LerLong(object value, string nome) => LerValor(value, nome) is null ? null : Convert.ToInt64(LerValor(value, nome), System.Globalization.CultureInfo.InvariantCulture);
}
