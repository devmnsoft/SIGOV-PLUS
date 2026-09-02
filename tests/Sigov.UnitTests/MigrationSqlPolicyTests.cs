using FluentAssertions;
using Sigov.Infrastructure.Persistence.Migrations;

namespace Sigov.UnitTests;

public sealed class MigrationSqlPolicyTests
{
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
}
