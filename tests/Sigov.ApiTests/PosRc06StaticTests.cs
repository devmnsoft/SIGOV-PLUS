using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PosRc06StaticTests
{
    private static string Read(string path) => File.ReadAllText(TestRepoPath.Get(path));

    [Fact]
    public void Ci_Deve_Conter_Jobs_E_Validar_OutboxEvento()
    {
        var ci = Read(".github/workflows/ci.yml");
        foreach (var job in new[] { "build-test", "docker-build", "sql-validate", "smoke-static", "docker-compose-e2e", "release-package-check" })
            Assert.Contains($"  {job}:", ci);
        Assert.Contains("sigov.outbox_evento", ci);
        Assert.DoesNotContain("to_regclass('sigov.outbox')", ci);
    }

    [Fact]
    public void SchemaReport_Smoke_Seed_E_Package_Devem_Cobrir_Correcoes_PosRc06()
    {
        var schema = Read("scripts/schema-report.ps1");
        Assert.Contains("'Docker','Psql'", schema);
        var smoke = Read("scripts/smoke-test-sigov.ps1");
        Assert.DoesNotContain("Escape($env:SIGOV_SMOKE_API_KEY)", smoke);
        Assert.Contains("sigov_demo_****rotate", smoke);
        Assert.Contains("failedNonBlocking", smoke);
        var seed = Read("database/postgres/seeds/pos_rc_homologacao_demo.sql");
        Assert.Contains("fc86ee2b04157910a83296966cd5033de0f564cbe8dc64d1f3a54238fb32063a", seed);
        foreach (var scope in new[] { "protocolos.read", "protocolos.write", "documentos.read", "tarefas.read", "webhooks.manage", "bi.read" })
            Assert.Contains(scope, seed);
        Assert.DoesNotContain("sigov_demo_local_only_2026_please_rotate", seed);
        var package = Read("scripts/package-release.ps1");
        Assert.Contains("Sanitize-EnvExample", package);
        Assert.Contains("POSTGRES_PASSWORD=change_me_local_only", package);
        Assert.Contains(".pfx$|\\.pem$|\\.key$", package);
    }

    [Fact]
    public void Hash_Da_Chave_Demo_Deve_Ser_Compativel_Com_Middleware()
    {
        var token = "sigov_demo_local_only_2026_please_rotate";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        Assert.Equal("fc86ee2b04157910a83296966cd5033de0f564cbe8dc64d1f3a54238fb32063a", hash);
    }
}
