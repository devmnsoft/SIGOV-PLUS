namespace Sigov.Application.Authorization;

public enum AuthorizationDecisionReason
{
    AutorizacaoConcedida,
    RequisicaoInvalida,
    ContextoObrigatorioAusente,
    UsuarioInativoOuInexistente,
    RecursoOuAcaoInexistente,
    SemConcessaoAplicavel,
    NegativaExplicita,
    AlcadaInsuficiente,
    FalhaNaAvaliacao
}

public sealed record AuthorizationScope(long? TenantId, long? EntidadeId, long? ExercicioId, long? UnidadeId);

public sealed record AuthorizationDecision(
    bool Permitido,
    AuthorizationDecisionReason Motivo,
    string Mensagem,
    long? PerfilId,
    long? VinculoId,
    AuthorizationScope Escopo,
    decimal? LimiteAlcada,
    bool NegativaExplicita,
    DateTimeOffset DecididoEmUtc)
{
    public static AuthorizationDecision Deny(AuthorizationDecisionReason reason, AuthorizationRequest request, DateTimeOffset now, bool explicitDeny = false, decimal? limit = null) =>
        new(false, reason, Message(reason), null, null, new(request.TenantId, request.EntidadeId, request.ExercicioId, request.UnidadeId), limit, explicitDeny, now);

    public static AuthorizationDecision Allow(AuthorizationRequest request, DateTimeOffset now, long profileId, long permissionId, decimal? limit) =>
        new(true, AuthorizationDecisionReason.AutorizacaoConcedida, Message(AuthorizationDecisionReason.AutorizacaoConcedida), profileId, permissionId,
            new(request.TenantId, request.EntidadeId, request.ExercicioId, request.UnidadeId), limit, false, now);

    private static string Message(AuthorizationDecisionReason reason) => reason switch
    {
        AuthorizationDecisionReason.AutorizacaoConcedida => "Acesso autorizado.",
        AuthorizationDecisionReason.RequisicaoInvalida => "A solicitação de autorização é inválida.",
        AuthorizationDecisionReason.ContextoObrigatorioAusente => "O contexto exigido não foi informado.",
        AuthorizationDecisionReason.UsuarioInativoOuInexistente => "Usuário sem vínculo ativo para autorização.",
        AuthorizationDecisionReason.RecursoOuAcaoInexistente => "Recurso ou ação não reconhecido.",
        AuthorizationDecisionReason.NegativaExplicita => "Uma restrição explícita impede a operação.",
        AuthorizationDecisionReason.AlcadaInsuficiente => "A operação excede a alçada concedida.",
        AuthorizationDecisionReason.FalhaNaAvaliacao => "Não foi possível confirmar a autorização.",
        _ => "Não existe concessão aplicável ao contexto informado."
    };
}
