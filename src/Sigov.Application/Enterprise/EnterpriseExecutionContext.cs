namespace Sigov.Application.Enterprise;

public sealed record EnterpriseExecutionContext(Guid TenantId, string UsuarioId, string Login, string? Ip, string? UserAgent, string CorrelationId, IReadOnlyCollection<string> Permissoes)
{
    public string Actor => string.IsNullOrWhiteSpace(Login) ? UsuarioId : Login;
}

public static class EnterpriseExecutionContextAccessor
{
    private static readonly AsyncLocal<EnterpriseExecutionContext?> CurrentContext = new();
    public static EnterpriseExecutionContext? Current { get => CurrentContext.Value; set => CurrentContext.Value = value; }
}
