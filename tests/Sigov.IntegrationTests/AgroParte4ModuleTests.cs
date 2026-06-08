using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class AgroParte4ModuleTests
{
    private static readonly string Migration = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "database", "postgres", "migrations", "20260608110000_agro_programas_beneficios_patrulha_mecanizada.sql"));

    [Theory]
    [InlineData("sigov.agro_programa_rural")]
    [InlineData("sigov.agro_beneficio_rural")]
    [InlineData("sigov.agro_beneficio_concessao")]
    [InlineData("sigov.agro_insumo")]
    [InlineData("sigov.agro_distribuicao_insumo")]
    [InlineData("sigov.agro_maquina")]
    [InlineData("sigov.agro_implemento")]
    [InlineData("sigov.agro_agenda_maquina")]
    [InlineData("sigov.agro_servico_maquina")]
    public void Migration_cria_tabelas_da_parte_4_no_schema_sigov(string table) => Migration.Should().Contain(table);

    [Fact] public void Migration_nao_cria_schema_agro() => Migration.Should().NotContain("create schema agro");
    [Fact] public void Tabelas_operacionais_possuem_tenant_id() => Migration.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
    [Fact] public void Dashboard_retorna_indicadores_parte_4() { Migration.Should().Contain("total_programas"); Migration.Should().Contain("servicos_maquina_pendentes"); }
    [Fact] public void Fluxos_estruturais_estao_presentes() { Migration.Should().Contain("AUTORIZADO"); Migration.Should().Contain("ENTREGUE"); File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Sigov.Infrastructure", "Agro", "Repositories", "AgroProgramasRepository.cs")).Should().Contain("AgroInsumoDistribuido"); Migration.Should().Contain("idx_agro_agenda_maquina_tenant_maquina"); }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
