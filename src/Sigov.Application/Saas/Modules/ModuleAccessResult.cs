namespace Sigov.Application.Saas.Modules;

public sealed record ModuleAccessResult(bool Allowed, int StatusCode, string Reason, SaasForbiddenMotivo? Motivo = null)
{
    public static ModuleAccessResult Allow(string reason = "Acesso permitido.") => new(true, 200, reason);
    public static ModuleAccessResult Forbidden(string reason, SaasForbiddenMotivo motivo = SaasForbiddenMotivo.SemPermissao) => new(false, 403, reason, motivo);
}
