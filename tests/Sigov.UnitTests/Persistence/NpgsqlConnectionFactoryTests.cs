using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.UnitTests.Persistence;

public sealed class NpgsqlConnectionFactoryTests
{
    [Fact]
    public void NpgsqlConnectionFactory_DeveFalhar_QuandoConfigurationForNula()
    {
        var action = () => Activator.CreateInstance(
            typeof(NpgsqlConnectionFactory),
            new object?[] { null });

        action.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void NpgsqlConnectionFactory_DeveFalhar_QuandoConnectionStringNaoExistir()
    {
        var configuration = BuildConfiguration();

        var action = () => new NpgsqlConnectionFactory(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("ConnectionStrings:DefaultConnection não configurada. Em Production use variável de ambiente ou secret manager.");
    }

    [Fact]
    public void NpgsqlConnectionFactory_DeveFalhar_QuandoConnectionStringForVazia()
    {
        var configuration = BuildConfiguration("   ");

        var action = () => new NpgsqlConnectionFactory(configuration);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NpgsqlConnectionFactory_DeveCriarNpgsqlConnection_QuandoValida()
    {
        var factory = new NpgsqlConnectionFactory(BuildConfiguration(ValidConnectionString));

        using var connection = factory.CreateConnection();

        connection.Should().NotBeNull();
    }

    [Fact]
    public void NpgsqlConnectionFactory_DevePreservarConnectionStringConfigurada()
    {
        var factory = new NpgsqlConnectionFactory(BuildConfiguration(ValidConnectionString));

        using var connection = factory.CreateConnection();

        connection.ConnectionString.Should().Be(ValidConnectionString);
    }

    private const string ValidConnectionString =
        "Host=localhost;Port=5432;Database=sigov;Username=sigov;Password=test-only";

    private static IConfiguration BuildConfiguration(string? connectionString = null)
    {
        var values = new Dictionary<string, string?>();
        if (connectionString is not null)
        {
            values["ConnectionStrings:DefaultConnection"] = connectionString;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
