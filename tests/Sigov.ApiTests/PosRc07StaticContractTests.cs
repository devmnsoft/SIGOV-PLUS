using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PosRc07StaticContractTests
{
    private static readonly string Root = FindRoot();
    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    [Fact]
    public void EnvExampleAndDockerComposeUseSigovDefaults()
    {
        var env = Read(".env.example");
        env.Should().Contain("POSTGRES_DB=sigov").And.Contain("POSTGRES_USER=sigov").And.Contain("POSTGRES_PASSWORD=change_me_local_only");
        env.Should().NotContain("POSTGRES_PASSWORD=123456");

        var compose = Read("docker-compose.yml");
        compose.Should().Contain("${POSTGRES_DB:-sigov}").And.Contain("${POSTGRES_USER:-sigov}").And.Contain("${POSTGRES_PASSWORD:-change_me_local_only}");
    }

    [Fact]
    public void TenantContextExistsAndDashboardDoesNotHardcodeTenantOne()
    {
        File.Exists(Path.Combine(Root, "src/Sigov.Web/Services/ITenantContextAccessor.cs")).Should().BeTrue();
        var service = Read("src/Sigov.Web/Services/PostBuildSaasService.cs");
        service.Should().NotContain("tenantId = 1L");
        service.Should().Contain("_tenantContextAccessor.Resolve()");
    }

    [Fact]
    public void GoLiveCheckCiSmokeSeedAndPackageContractsArePresent()
    {
        Read("scripts/go-live-check.ps1").Should().Contain("docs/go-live-check-result.md").And.Contain("failedBlocking").And.Contain("releaseCandidateVersion");
        Read(".github/workflows/ci.yml").Should().Contain("go-live-check:").And.Contain("docker-compose-e2e:");
        Read("scripts/smoke-test-sigov.ps1").Should().Contain("Mask").And.NotContain("Escape($env:SIGOV_SMOKE_API_KEY)");
        Read("database/postgres/seeds/pos_rc_homologacao_demo.sql").Should().Contain("fc86ee2b04157910a83296966cd5033de0f564cbe8dc64d1f3a54238fb32063a").And.Contain("protocolos.read").And.Contain("documentos.read").And.Contain("tarefas.read");
        Read("scripts/package-release.ps1").Should().Contain(".pfx").And.Contain(".pem").And.Contain(".key");
        Read("docs/matriz-modulos-release-candidate.md").Should().Contain("Pós-RC 07");
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sigov.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }
}
