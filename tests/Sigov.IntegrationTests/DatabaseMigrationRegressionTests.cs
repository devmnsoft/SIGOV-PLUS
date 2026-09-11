using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class DatabaseMigrationRegressionTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string MigrationsPath = Path.Combine(Root, "database", "postgres", "migrations");
    private static readonly string[] ForbiddenSchemas =
    {
        "core", "sec", "audit", "lgpd", "fin", "trib", "compras", "rh", "educacao", "saude", "saneamento", "social", "suporte", "operacao", "integracao", "bi", "transparencia"
    };

    [Fact]
    public void Migrations_Devem_Usar_Apenas_Schema_Sigov_E_Metadata_Em_Sigov()
    {
        var allSql = ReadAllMigrations();

        allSql.Should().Contain("create schema if not exists sigov;");
        allSql.Should().Contain("sigov.schema_migrations");
        foreach (var schema in ForbiddenSchemas)
        {
            Regex.IsMatch(allSql, $@"\bcreate\s+schema\s+(if\s+not\s+exists\s+)?{Regex.Escape(schema)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Should().BeFalse($"o schema físico proibido {schema} não deve ser criado");
        }
    }

    [Theory]
    [InlineData("nvarchar")]
    [InlineData("datetime2")]
    [InlineData("bit")]
    [InlineData("uniqueidentifier")]
    [InlineData("rowversion")]
    public void Migrations_Nao_Devem_Usar_Tipos_Do_SQL_Server(string forbiddenType)
    {
        ReadAllMigrations().Should().NotMatchRegex($@"\b{Regex.Escape(forbiddenType)}\b");
    }

    [Fact]
    public void Migrations_Operacionais_Devem_Declarar_TenantId_E_Qualificar_Tabelas_Com_Sigov()
    {
        var sql = ReadBaselineMigrations();

        sql.Should().Contain("tenant_id");
        var tableNames = Regex.Matches(sql, @"^\s*create\s+table\s+(?:if\s+not\s+exists\s+)?(?<name>[a-zA-Z0-9_.%]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)
            .Select(match => match.Groups["name"].Value)
            .ToArray();

        tableNames.Should().OnlyContain(table => table.StartsWith("sigov.", StringComparison.OrdinalIgnoreCase), "toda tabela criada pelas migrations deve ser qualificada como sigov.<tabela>");
    }

    [Fact]
    public void Manifest_Deve_Estar_Ordenado_Com_Checksums_Normalizados_E_Historico_LicitaPro()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(MigrationsPath, "manifest.json")));
        var entries = document.RootElement.GetProperty("migrations").EnumerateArray().ToArray();
        entries.Select(entry => entry.GetProperty("version").GetString()).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();

        foreach (var entry in entries.Where(entry => entry.GetProperty("applyAutomatically").GetBoolean()))
        {
            var contents = File.ReadAllText(Path.Combine(MigrationsPath, entry.GetProperty("file").GetString()!))
                .TrimStart('\uFEFF').Replace("\r\n", "\n").Replace("\r", "\n");
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contents))).ToLowerInvariant()
                .Should().Be(entry.GetProperty("checksum").GetString());
        }

        var historical = entries.Single(entry => entry.GetProperty("version").GetString() == "20260903130000");
        historical.GetProperty("knownChecksums").EnumerateArray().Select(value => value.GetString())
            .Should().Contain("2ee4b77413f755230ad1bdaef456893c1f5f045866ea436e78d388a0b4f18364");

        entries.Single(entry => entry.GetProperty("version").GetString() == "20260902010000")
            .GetProperty("applyAutomatically").GetBoolean().Should().BeFalse();
        historical.GetProperty("applyAutomatically").GetBoolean().Should().BeFalse();
        entries.Single(entry => entry.GetProperty("version").GetString() == "20260909120000")
            .GetProperty("applyAutomatically").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Correcao_LicitaPro_Deve_Ser_Aditiva_Idempotente_E_Validar_Catalogos_Fortes()
    {
        var sql = File.ReadAllText(Path.Combine(MigrationsPath, "20260903173000_corr_licitapro_schema_history.sql"));
        sql.Should().Contain("conrelid=to_regclass('sigov.compras_licitapro_fonte')");
        sql.Should().Contain("create index ix_clp_alerta_tenant_status_vencimento");
        sql.Should().NotContain("create index sigov.ix_clp_alerta_tenant_status_vencimento");
        sql.Should().Contain("compras_licitapro_alerta (tenant_id, entidade_id, status, vencimento_at)");
        sql.Should().Contain("pg_constraint").And.Contain("pg_attribute");
        sql.Should().NotContain("concurrently");
    }

    [Fact]
    public void Runner_Deve_Validar_Antes_Do_Ddl_E_Avaliar_PostConditions_Somente_Ao_Final()
    {
        var runner = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Persistence", "Migrations", "MigrationRunner.cs"));
        runner.IndexOf("ThrowIfInvalid(validation);", StringComparison.Ordinal).Should()
            .BeLessThan(runner.IndexOf("EnsureMigrationHistoryAsync(connection", StringComparison.Ordinal));
        runner.IndexOf("// Fase 2:", StringComparison.Ordinal).Should()
            .BeLessThan(runner.IndexOf("// Fase 3:", StringComparison.Ordinal));
        runner.Should().Contain("if (!validateOnly)");
        runner.Should().Contain("history = await ReadMigrationHistoryAsync");
        runner.Should().Contain("manifest.DeclaredMigrations.ToDictionary");
        runner.Should().Contain("manifest.AutomaticMigrations.Where");
        runner.Should().Contain("validation.Excluded.Add");
        runner.Should().Contain("POSTCONDITION_MISSING: migration histórica presente");
        runner.Should().Contain("pendentes=0; checksum=0; falhas=0");
    }

    [Fact]
    public void PosCondicoes_Corrigidas_Devem_Distinguir_Contratos_E_Configuracao_Comercial()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(MigrationsPath, "manifest.json")));
        var entries = document.RootElement.GetProperty("migrations").EnumerateArray().ToArray();

        var compras = entries.Single(entry => entry.GetProperty("version").GetString() == "20260802210000");
        var comprasProbes = compras.GetProperty("postConditionProbes").ToString();
        compras.GetProperty("postConditionSql").GetString().Should().Contain("compras_empresarial_fatura").And.NotContain("compras_fatura') is not null and");
        comprasProbes.Should().Contain("coexistem").And.Contain("atttypid='uuid'::regtype").And.Contain("compras_empresarial_pedido");

        var saude = entries.Single(entry => entry.GetProperty("version").GetString() == "20260819120000");
        saude.GetProperty("postConditionProbes").ToString().Should().Contain("permissão canônica ativa ausente")
            .And.Contain("atribuição sem tenant_id bigint");

        var saas = entries.Single(entry => entry.GetProperty("version").GetString() == "20260908120000");
        var saasProbes = saas.GetProperty("postConditionProbes").EnumerateArray().ToArray();
        saasProbes.Should().HaveCount(9);
        saasProbes.Select(probe => probe.GetProperty("name").GetString()).Should().Contain(new[]
        {
            "tabela de histórico", "trigger de auditoria correto e habilitado", "trigger de compatibilidade correto e habilitado",
            "cadastro industria_producao", "nome administrável", "disponibilidade de contratação administrável"
        });
        saas.GetProperty("postConditionSql").GetString().Should().NotContain("nome='Indústria 360'").And.NotContain("disponivel_contratacao)");

        var correction = entries.Single(entry => entry.GetProperty("version").GetString() == "20260910120000");
        var correctionProbes = correction.GetProperty("postConditionProbes").EnumerateArray().ToArray();
        correctionProbes.Should().HaveCount(5);
        correctionProbes.Select(probe => probe.GetProperty("name").GetString()).Should().Contain(new[]
        {
            "permissão saude.visita.registrar ativa", "perfil_acesso.tenant_id bigint anulável",
            "tabela de histórico SaaS canônica", "trigger SaaS de auditoria corrigido",
            "trigger SaaS de compatibilidade corrigido"
        });
        correction.GetProperty("postConditionSql").GetString().Should().Contain("is_nullable='YES'")
            .And.Contain("ativo and not is_deleted");
    }

    [Fact]
    public void Correcao_Deve_Ser_ForwardOnly_Idempotente_E_Preservar_Suspensao_Comercial()
    {
        var sql = File.ReadAllText(Path.Combine(MigrationsPath, "20260910120000_corr_postconditions_rc37b_rc5060_saas.sql"));
        sql.Should().Contain("where not exists (select 1 from sigov.permissao where chave='saude.visita.registrar')");
        sql.Should().Contain("alter column tenant_id drop not null");
        sql.Should().Contain("drop trigger if exists trg_tenant_modulo_contrato_auditar");
        sql.Should().Contain("drop trigger if exists trg_tenant_modulo_compatibilizar");
        sql.Should().NotContain("update sigov.modulo_saas set");
        sql.Should().NotContain("disponivel_contratacao=true");
        sql.Should().NotContain("create table if not exists sigov.compras_fatura");
    }

    [Fact]
    public void Aplicador_PowerShell_Deve_Executar_Probes_Nomeados_Antes_De_Registrar_Ledger()
    {
        var script = File.ReadAllText(Path.Combine(Root, "scripts", "apply-migrations-manifest.ps1"));
        var probes = script.IndexOf("foreach ($probe in (Get-OptionalArray $entry 'postConditionProbes'))", StringComparison.Ordinal);
        var registration = script.IndexOf("$psqlArgs += @('-f', $registrationFile)", StringComparison.Ordinal);

        probes.Should().BeGreaterThan(0);
        probes.Should().BeLessThan(registration, "probes reprovados devem abortar a transação antes do registro no ledger");
        script.Should().Contain("nullif(btrim(failure_message), '') is not null");
        script.Should().Contain("postConditionProbe reprovada");
        script.Should().Contain("postConditionSql final reprovada");
        script.Should().Contain("postConditionProbe final reprovada");
        script.Should().Contain("DATABASE_HISTORY_INCONSISTENT: versão");
        script.Should().Contain("DATABASE_HISTORY_INCONSISTENT: checksum desconhecido");
        script.Should().Contain("knownChecksums");
        script.Should().Contain("if ($appliedAny)");
        script.IndexOf("$canExecute =", StringComparison.Ordinal).Should()
            .BeLessThan(script.IndexOf("Get-Command $PsqlPath", StringComparison.Ordinal),
                "ValidateOnly não deve depender da instalação do cliente PostgreSQL");
    }

    private static string ReadAllMigrations() => string.Join('\n', Directory.GetFiles(MigrationsPath, "*.sql", SearchOption.TopDirectoryOnly).OrderBy(static file => file, StringComparer.OrdinalIgnoreCase).Select(File.ReadAllText));

    private static string ReadBaselineMigrations()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(MigrationsPath, "manifest.json")));
        var files = document.RootElement.GetProperty("migrations")
            .EnumerateArray()
            .Where(entry => entry.GetProperty("includeInBaseline").GetBoolean())
            .Select(entry => entry.GetProperty("file").GetString()!)
            .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase);

        return string.Join('\n', files.Select(file => File.ReadAllText(Path.Combine(MigrationsPath, file))));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório sigov não encontrada.");
    }
}
