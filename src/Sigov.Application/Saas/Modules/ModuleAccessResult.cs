namespace Sigov.Application.Saas.Modules;

public sealed record ModuleAccessResult(bool Allowed, int StatusCode, string Reason)
{
    public static ModuleAccessResult Allow(string reason = "Acesso permitido.") => new(true, 200, reason);
    public static ModuleAccessResult Forbidden(string reason) => new(false, 403, reason);
}
