using FluentAssertions;
using Sigov.Infrastructure.Persistence.Migrations;
using Sigov.Testing;

namespace Sigov.UnitTests;

public sealed class MigrationSqlPolicyTests
{
    [Fact]
    public void Migration_path_resolution_rejects_two_manifest_candidates_deterministically()
    {
        using var checkoutA = MigrationTree.CreateRepository();
        using var checkoutB = MigrationTree.CreateRepository();

        var action = () => MigrationRunner.ResolveMigrationsPath(null, checkoutB.Root, checkoutB.Root, checkoutA.BaseDirectory);

        var exception = action.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain(checkoutA.Migrations);
        exception.Message.Should().Contain(checkoutB.Migrations);
    }

    [Fact]
    public void Migration_path_resolution_from_repository_root_finds_canonical_manifest()
    {
        using var tree = MigrationTree.CreateRepository();

        var resolved = MigrationRunner.ResolveMigrationsPath(null, tree.Root, tree.Root, tree.BaseDirectory);

        resolved.Should().Be(tree.Migrations);
    }

    [Fact]
    public void Migration_path_resolution_from_visual_studio_api_directory_finds_repository_manifest()
    {
        using var tree = MigrationTree.CreateRepository();
        var apiDirectory = Path.Combine(tree.Root, "src", "Sigov.Api");
        Directory.CreateDirectory(apiDirectory);

        var resolved = MigrationRunner.ResolveMigrationsPath(null, apiDirectory, apiDirectory, tree.BaseDirectory);

        resolved.Should().Be(tree.Migrations);
    }

    [Fact]
    public void Migration_path_resolution_uses_content_root_when_current_directory_is_elsewhere()
    {
        using var tree = MigrationTree.CreateRepository();
        using var other = MigrationTree.CreateIsolatedDirectory();
        var apiDirectory = Path.Combine(tree.Root, "src", "Sigov.Api");
        Directory.CreateDirectory(apiDirectory);

        var resolved = MigrationRunner.ResolveMigrationsPath(null, apiDirectory, other.Root, tree.BaseDirectory);

        resolved.Should().Be(tree.Migrations);
    }

    [Fact]
    public void Migration_path_resolution_from_bin_base_directory_finds_repository_manifest()
    {
        using var tree = MigrationTree.CreateRepository();

        var resolved = MigrationRunner.ResolveMigrationsPath(null, tree.ApiDirectory, tree.ApiDirectory, tree.BaseDirectory);

        resolved.Should().Be(tree.Migrations);
    }

    [Fact]
    public void Migration_path_resolution_accepts_valid_absolute_configuration()
    {
        using var tree = MigrationTree.CreateRepository();

        var resolved = MigrationRunner.ResolveMigrationsPath(tree.Migrations, tree.ApiDirectory, tree.ApiDirectory, tree.BaseDirectory);

        resolved.Should().Be(tree.Migrations);
    }

    [Fact]
    public void Migration_path_resolution_rejects_invalid_absolute_configuration()
    {
        using var tree = MigrationTree.CreateRepository();
        var missing = Path.Combine(tree.Root, "missing", "migrations");

        var action = () => MigrationRunner.ResolveMigrationsPath(missing, tree.ApiDirectory, tree.ApiDirectory, tree.BaseDirectory);

        action.Should().Throw<DirectoryNotFoundException>().WithMessage($"*{missing}*");
    }

    [Fact]
    public void Migration_path_resolution_finds_relative_configuration_in_ancestors()
    {
        using var tree = MigrationTree.CreateRepository();

        var resolved = MigrationRunner.ResolveMigrationsPath("database/postgres/migrations", tree.ApiDirectory, tree.ApiDirectory, tree.BaseDirectory);

        resolved.Should().Be(tree.Migrations);
    }

    [Fact]
    public void Migration_path_resolution_reports_examined_paths_for_missing_relative_configuration()
    {
        using var tree = MigrationTree.CreateRepository();

        var action = () => MigrationRunner.ResolveMigrationsPath("missing/migrations", tree.ApiDirectory, tree.ApiDirectory, tree.BaseDirectory);

        action.Should().Throw<DirectoryNotFoundException>().WithMessage("*PathsExaminados*missing*");
    }

    [Fact]
    public void Migration_path_resolution_rejects_two_relative_checkout_candidates_deterministically()
    {
        using var checkoutA = MigrationTree.CreateRepository();
        using var checkoutB = MigrationTree.CreateRepository();

        var action = () => MigrationRunner.ResolveMigrationsPath("database/postgres/migrations", checkoutB.Root, checkoutB.Root, checkoutA.BaseDirectory);

        var exception = action.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain(checkoutA.Migrations);
        exception.Message.Should().Contain(checkoutB.Migrations);
    }

    [Fact]
    public void Migration_path_resolution_rejects_manifest_inside_bin()
    {
        using var tree = MigrationTree.CreateIsolatedDirectory();
        var binMigrations = Path.Combine(tree.Root, "bin", "database", "postgres", "migrations");
        Directory.CreateDirectory(binMigrations);
        File.WriteAllText(Path.Combine(binMigrations, "manifest.json"), "{}");

        var action = () => MigrationRunner.ResolveMigrationsPath(binMigrations, tree.Root, tree.Root, tree.Root);

        action.Should().Throw<DirectoryNotFoundException>().WithMessage("*bin*");
    }

    [Fact]
    public void Migration_path_resolution_accepts_docker_absolute_app_path()
    {
        using var app = MigrationTree.CreateIsolatedDirectory();
        var migrations = Path.Combine(app.Root, "database", "postgres", "migrations");
        Directory.CreateDirectory(migrations);
        File.WriteAllText(Path.Combine(migrations, "manifest.json"), "{}");

        var resolved = MigrationRunner.ResolveMigrationsPath(migrations, app.Root, app.Root, app.Root);

        resolved.Should().Be(migrations);
    }

    [Fact]
    public void Migration_path_resolution_finds_published_package_migrations()
    {
        using var package = MigrationTree.CreateIsolatedDirectory();
        var migrations = Path.Combine(package.Root, "database", "postgres", "migrations");
        Directory.CreateDirectory(migrations);
        File.WriteAllText(Path.Combine(migrations, "manifest.json"), "{}");

        var resolved = MigrationRunner.ResolveMigrationsPath(null, package.Root, package.Root, package.Root);

        resolved.Should().Be(migrations);
    }

    [Fact]
    public void Migration_runner_does_not_resolve_path_in_constructor()
    {
        var runner = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Persistence/Migrations/MigrationRunner.cs"));
        var constructorStart = runner.IndexOf("public MigrationRunner(", StringComparison.Ordinal);
        var constructorEnd = runner.IndexOf("private MigrationPathResolution PathResolution", StringComparison.Ordinal);
        var constructor = runner[constructorStart..constructorEnd];
        constructor.Should().NotContain("ResolveMigrations(");
        constructor.Should().Contain("_hostEnvironment = hostEnvironment");
        runner.IndexOf("MigrationRunner desabilitado", StringComparison.Ordinal).Should()
            .BeLessThan(runner.IndexOf("Directory.Exists(MigrationsDirectory)", StringComparison.Ordinal));
    }

    [Fact]
    public void Migration_path_resolution_treats_generated_only_candidate_as_zero_matches()
    {
        using var tree = MigrationTree.CreateIsolatedDirectory();
        var generated = Path.Combine(tree.Root, "bin", "database", "postgres", "migrations");
        Directory.CreateDirectory(generated);
        File.WriteAllText(Path.Combine(generated, "manifest.json"), "{}");

        var action = () => MigrationRunner.ResolveMigrationsPath(null, tree.Root, tree.Root, tree.Root);

        action.Should().Throw<DirectoryNotFoundException>().WithMessage("*Nenhum manifest.json canônico encontrado*");
    }

    [Fact]
    public void Migration_path_resolution_rejects_backup_directory_candidate()
    {
        using var tree = MigrationTree.CreateIsolatedDirectory();
        var backup = Path.Combine(tree.Root, "backups", "database", "postgres", "migrations");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "manifest.json"), "{}");

        var action = () => MigrationRunner.ResolveMigrationsPath(backup, tree.Root, tree.Root, tree.Root);

        action.Should().Throw<DirectoryNotFoundException>().WithMessage("*backups*");
    }

    [Fact]
    public void Migration_path_resolution_rejects_missing_expected_latest_migration_message_contract()
    {
        var runner = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Persistence/Migrations/MigrationRunner.cs"));

        runner.Should().Contain("MANIFEST_OUTDATED");
        runner.Should().Contain("ExpectedLatestMigration");
    }

    [Fact]
    public void Legacy_wrapper_is_removed_without_touching_plpgsql()
    {
        const string raw = "\uFEFF-- legacy\r\nBEGIN;\r\nDO $body$\r\nBEGIN\r\n  PERFORM 'commit;';\r\nEND\r\n$body$;\r\nCOMMIT; -- eof\r\n";

        var prepared = MigrationSqlPolicy.PrepareForExecution("legacy", raw, legacyTransactionWrapper: true);

        prepared.Should().Contain("DO $body$");
        prepared.Should().Contain("BEGIN\r\n  PERFORM 'commit;';");
        prepared.Should().NotStartWith("BEGIN;");
        prepared.Should().NotContain("\r\nCOMMIT;");
    }

    [Theory]
    [InlineData("begin; select 1; commit;")]
    [InlineData("select 1; rollback;")]
    [InlineData("savepoint unsafe; select 1;")]
    [InlineData("start transaction; select 1;")]
    [InlineData("end transaction;")]
    public void Undeclared_transaction_control_is_rejected(string sql)
    {
        var action = () => MigrationSqlPolicy.PrepareForExecution("new", sql, legacyTransactionWrapper: false);

        action.Should().Throw<MigrationTransactionException>().WithMessage("*controle transacional explícito*");
    }

    [Fact]
    public void Transaction_words_inside_comments_literals_and_plpgsql_are_preserved()
    {
        const string sql = """
            -- begin;
            /* rollback; */
            select 'commit;';
            do $$ begin perform 1; end $$;
            create function example() returns void language plpgsql as $function$
            begin
                perform 'savepoint hidden;';
            end
            $function$;
            """;

        MigrationSqlPolicy.PrepareForExecution("safe", sql, legacyTransactionWrapper: false).Should().Be(sql);
    }

    [Fact]
    public void Rejection_identifies_version_command_line_and_safe_excerpt()
    {
        const string sql = "-- harmless\nselect 1;\nrollback to savepoint secret_name;";

        var action = () => MigrationSqlPolicy.PrepareForExecution("20260817131000", sql, legacyTransactionWrapper: false);

        action.Should().Throw<MigrationTransactionException>()
            .WithMessage("*20260817131000*ROLLBACK*linha 3*Trecho seguro: \"ROLLBACK TO SAVEPOINT SECRET_NAME;\"*");
    }

    [Fact]
    public void Legacy_marker_does_not_allow_additional_transaction_commands()
    {
        var action = () => MigrationSqlPolicy.PrepareForExecution(
            "unsafe-legacy", "begin; savepoint extra; select 1; commit;", legacyTransactionWrapper: true);

        action.Should().Throw<MigrationTransactionException>().WithMessage("*exclusivamente um wrapper externo*");
    }

    private sealed class MigrationTree : IDisposable
    {
        private MigrationTree(string root)
        {
            Root = Path.GetFullPath(root);
            ApiDirectory = Path.Combine(Root, "src", "Sigov.Api");
            BaseDirectory = Path.Combine(ApiDirectory, "bin", "Debug", "net10.0");
            Migrations = Path.Combine(Root, "database", "postgres", "migrations");
        }

        public string Root { get; }
        public string ApiDirectory { get; }
        public string BaseDirectory { get; }
        public string Migrations { get; }

        public static MigrationTree CreateRepository()
        {
            var tree = CreateIsolatedDirectory();
            Directory.CreateDirectory(tree.ApiDirectory);
            Directory.CreateDirectory(tree.BaseDirectory);
            Directory.CreateDirectory(tree.Migrations);
            File.WriteAllText(Path.Combine(tree.Root, "sigov.sln"), string.Empty);
            File.WriteAllText(Path.Combine(tree.Root, "AGENTS.md"), string.Empty);
            File.WriteAllText(Path.Combine(tree.Migrations, "manifest.json"), "{}");
            return tree;
        }

        public static MigrationTree CreateIsolatedDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), $"sigov-migrations-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new MigrationTree(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
