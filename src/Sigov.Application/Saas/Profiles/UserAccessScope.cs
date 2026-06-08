namespace Sigov.Application.Saas.Profiles;

public sealed record UserAccessScope(long TenantId, long? EntidadeId, long? ExercicioId, string? ModuloCodigo, string Escopo);
