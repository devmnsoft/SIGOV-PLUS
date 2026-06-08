namespace Sigov.Application.Saas.Context;

public sealed record TenantContextSwitchRequest(long UsuarioGlobalId, long? TenantDestinoId, long? EntidadeDestinoId, string Motivo, string? Ip, string? UserAgent, Guid? CorrelationId);
