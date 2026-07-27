using Sigov.Testing;
using FluentAssertions;
using Xunit;
namespace Sigov.IntegrationTests;
public sealed class SocialModuleSmokeTests
{
    [Fact] public void Migration_Social_Usa_Apenas_Schema_Sigov()
    {
        var sql = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/024_assistencia_social_base.sql"));
        sql.Should().Contain("sigov.social_familia");
        sql.Should().NotContain("create schema " + "social");
        sql.Should().NotContain(" social" + ".");
        sql.Should().Contain("tenant_id bigint not null");
    }
}
