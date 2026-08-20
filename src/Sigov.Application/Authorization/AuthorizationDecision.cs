namespace Sigov.Application.Authorization;

public sealed record AuthorizationDecision(bool Permitido, string Motivo)
{
    public static AuthorizationDecision Deny(string reason) => new(false, reason);
    public static AuthorizationDecision Allow() => new(true, "AUTORIZACAO_CONCEDIDA");
}
