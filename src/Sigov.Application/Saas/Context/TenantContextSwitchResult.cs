namespace Sigov.Application.Saas.Context;

public sealed record TenantContextSwitchResult(bool Success, long? LogId, string Message);
