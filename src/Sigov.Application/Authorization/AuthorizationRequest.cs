namespace Sigov.Application.Authorization;

public sealed record AuthorizationRequest(long UsuarioId, string Recurso, string Acao,
    long? TenantId, long? EntidadeId = null, long? ExercicioId = null,
    long? UnidadeId = null, decimal? Valor = null);
