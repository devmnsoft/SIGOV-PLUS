using FluentAssertions;
using Xunit;

namespace SIGOV.IntegrationTests;

public sealed class InfrastructureSmokeTests
{
    [Fact]
    public void Testcontainers_PostgreSql_Esta_Referenciado()
    {
        typeof(Testcontainers.PostgreSql.PostgreSqlBuilder).FullName.Should().Contain("PostgreSqlBuilder");
    }
}
