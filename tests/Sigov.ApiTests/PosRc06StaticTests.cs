using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PosRc06StaticTests
{
    private static string Read(string path) => File.ReadAllText(TestRepoPath.Get(path));

    [Fact]
    public void Tracked_Artifacts_Gate_Deve_Falhar_Fora_De_Worktree_E_Sem_Ferramentas()
    {
        var script = Read("scripts/check-tracked-artifacts.sh");
        Assert.Contains("git is not available", script);
        Assert.Contains("rg is not available", script);
        Assert.Contains("not inside a git worktree", script);
        Assert.Contains("git ls-files failed", script);
        Assert.Contains("tracked_files=\"$(git ls-files)\"", script);
        Assert.Contains("cache\\.json", script);
        var passIndex = script.LastIndexOf("Tracked artifact gate: PASS", StringComparison.Ordinal);
        var failIndex = script.IndexOf("Generated artifacts are tracked by Git", StringComparison.Ordinal);
        Assert.True(failIndex > 0 && passIndex > failIndex);
        var ci = Read(".github/workflows/ci.yml");
        Assert.Contains("Install ripgrep", ci);
        Assert.Contains("needs: [tracked-artifacts, workflow-integrity]", ci);
    }

    [Fact]
    public void Appsettings_Versionados_Nao_Devem_Conter_Senha_Literal()
    {
        foreach (var path in new[]
        {
            "src/Sigov.Api/appsettings.json",
            "src/Sigov.Api/appsettings.Development.json",
            "src/Sigov.Api/appsettings.Homologation.json",
            "src/Sigov.Web/appsettings.json",
            "src/Sigov.Web/appsettings.Development.json",
            "src/Sigov.Worker/appsettings.json"
        })
        {
            var content = Read(path);
            Assert.DoesNotContain("Password=123456", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"MigrationsPath\": \"database/postgres/migrations\"", content);
        }
        Assert.Contains("ConnectionStrings__DefaultConnection", Read(".env.example"));
    }

    [Fact]
    public void Ci_Deve_Conter_Jobs_E_Validar_OutboxEvento()
    {
        var ci = Read(".github/workflows/ci.yml");
        foreach (var job in new[] { "tracked-artifacts", "build-test", "migration-governance-static", "migrations-manifest", "script-completop-idempotency", "schema-equivalence", "standalone-postgres-runtime", "docker-build", "release-package-check" })
            Assert.Contains($"  {job}:", ci);
        Assert.Contains("postgres:16", ci);
        Assert.Contains("sigov.outbox_evento", Read("database/postgres/seeds/pos_rc_homologacao_demo.sql"));
        Assert.Contains("sigov.outbox_evento", Read("database/postgres/script_completo.sql"));
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
        Assert.Contains("sigov.api_key_escopo", seed);
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
