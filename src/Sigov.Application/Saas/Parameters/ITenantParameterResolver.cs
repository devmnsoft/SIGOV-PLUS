namespace Sigov.Application.Saas.Parameters;

public interface ITenantParameterResolver
{
    Task<TenantParameterResolvedValue> ResolveAsync(string codigo, TenantParameterResolveContext context, CancellationToken cancellationToken);
}

public sealed record TenantParameterResolveContext(long TenantId, long? EntidadeId, long? ExercicioId, long? UsuarioId, string? ModuloCodigo, DateOnly? DataReferencia = null);
public sealed record TenantParameterResolvedValue(string Codigo, string? ValorJson, bool Found, bool Sensivel, string SourceScope);
