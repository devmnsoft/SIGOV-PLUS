using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.ApiTests;

public sealed class ApiV1ApiKeySecurityStaticTests
{
    private static readonly string MiddlewarePath = Path.Combine(TestRepoPath.Root, "src", "Sigov.Api", "Middlewares", "ApiKeyV1Middleware.cs");

    [Fact]
    public void Middleware_Deve_Exigir_Key_Tenant_E_Escopos_Sem_Logar_Token()
    {
        var code = File.ReadAllText(MiddlewarePath);

        code.Should().Contain("X-Api-Key");
        code.Should().Contain("X-Tenant-Id");
        code.Should().Contain("StatusCodes.Status401Unauthorized");
        code.Should().Contain("StatusCodes.Status403Forbidden");
        code.Should().Contain("protocolos.write");
        code.Should().Contain("documentos.read");
        code.Should().Contain("api_requisicao_log");
        code.Should().NotContain("api_key_texto_claro");
    }
}
