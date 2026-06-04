using FluentAssertions;
using Sigov.Infrastructure.Storage;
using Xunit;

namespace Sigov.UnitTests;

public sealed class StorageKeyGeneratorTests
{
    [Fact]
    public void Generate_DeveIncluirTenantIdENaoExporPathFisico()
    {
        var key = new StorageKeyGenerator().Generate(42, "documento.pdf");

        key.Should().StartWith("tenants/42/");
        key.Should().EndWith(".pdf");
        key.Should().NotContain("\\");
    }
}
