using FluentAssertions;
using Sigov.Application.Saas.Parameters;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class TenantParameterResolverTests
{
    [Fact]
    public async Task Parametro_por_usuario_sobrescreve_modulo()
    {
        var resolver = new TenantParameterResolver(new FakeParameterRepository(new[]
        {
            Value("MODULO", "{\"valor\":1}"),
            Value("USUARIO", "{\"valor\":2}", usuarioId: 5)
        }));
        var result = await resolver.ResolveAsync("tributario.aliquota", new TenantParameterResolveContext(1, null, null, 5, "tributario"), CancellationToken.None);
        result.SourceScope.Should().Be("USUARIO");
    }

    [Fact]
    public async Task Parametro_por_modulo_sobrescreve_tenant()
    {
        var resolver = new TenantParameterResolver(new FakeParameterRepository(new[] { Value("TENANT", "1"), Value("MODULO", "2") }));
        var result = await resolver.ResolveAsync("agro.feature", new TenantParameterResolveContext(1, null, null, null, "agro"), CancellationToken.None);
        result.SourceScope.Should().Be("MODULO");
    }

    [Fact]
    public async Task Parametro_sensivel_e_mascarado()
    {
        var resolver = new TenantParameterResolver(new FakeParameterRepository(new[] { Value("TENANT", "{\"segredo\":\"abc\"}") }, true));
        var result = await resolver.ResolveAsync("integracao.token", new TenantParameterResolveContext(1, null, null, null, "integracoes"), CancellationToken.None);
        result.ValorJson.Should().Be("***");
    }

    private static TenantParameterValueDto Value(string scope, string value, long? usuarioId = null) => new(1, 1, null, null, usuarioId, null, scope, value, null, null, null, true);

    private sealed class FakeParameterRepository : ITenantParameterRepository
    {
        private readonly bool _sensitive;
        private readonly IReadOnlyCollection<TenantParameterValueDto> _values;

        public FakeParameterRepository(IReadOnlyCollection<TenantParameterValueDto> values, bool sensitive = false)
        {
            _values = values;
            _sensitive = sensitive;
        }

        public Task<IReadOnlyCollection<TenantParameterDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<TenantParameterDefinitionDto>>(Array.Empty<TenantParameterDefinitionDto>());
        public Task<TenantParameterDefinitionDto?> GetDefinitionAsync(string codigo, CancellationToken cancellationToken) => Task.FromResult<TenantParameterDefinitionDto?>(new TenantParameterDefinitionDto(1, codigo, codigo, null, null, "JSON", "TENANT", "{}", false, _sensitive, true, true));
        public Task<IReadOnlyCollection<TenantParameterValueDto>> GetValuesAsync(string codigo, TenantParameterResolveContext context, CancellationToken cancellationToken) => Task.FromResult(_values);
        public Task UpsertValueAsync(string codigo, TenantParameterValueDto value, long? userId, Guid? correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
