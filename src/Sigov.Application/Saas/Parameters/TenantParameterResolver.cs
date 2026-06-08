namespace Sigov.Application.Saas.Parameters;

public sealed class TenantParameterResolver : ITenantParameterResolver
{
    private static readonly IReadOnlyList<string> Priority = new[] { "USUARIO", "MODULO", "EXERCICIO", "ENTIDADE", "TENANT", "GLOBAL" };
    private readonly ITenantParameterRepository _repository;

    public TenantParameterResolver(ITenantParameterRepository repository) => _repository = repository;

    public async Task<TenantParameterResolvedValue> ResolveAsync(string codigo, TenantParameterResolveContext context, CancellationToken cancellationToken)
    {
        var definition = await _repository.GetDefinitionAsync(codigo, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return new TenantParameterResolvedValue(codigo, null, false, false, "NAO_ENCONTRADO");
        }

        var values = await _repository.GetValuesAsync(codigo, context, cancellationToken).ConfigureAwait(false);
        var reference = context.DataReferencia ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var activeValues = values.Where(value => value.Ativo
            && (value.VigenteInicio is null || value.VigenteInicio <= reference)
            && (value.VigenteFim is null || value.VigenteFim >= reference));

        foreach (var scope in Priority)
        {
            var match = activeValues.FirstOrDefault(value => string.Equals(value.Escopo, scope, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return new TenantParameterResolvedValue(codigo, definition.Sensivel ? Mask(match.ValorJson, match.ValorMascarado) : match.ValorJson, true, definition.Sensivel, scope);
            }
        }

        return new TenantParameterResolvedValue(codigo, definition.Sensivel ? Mask(definition.ValorPadraoJson, null) : definition.ValorPadraoJson, definition.ValorPadraoJson is not null, definition.Sensivel, "VALOR_PADRAO");
    }

    private static string? Mask(string? value, string? explicitMask) => !string.IsNullOrWhiteSpace(explicitMask) ? explicitMask : value is null ? null : "***";
}
