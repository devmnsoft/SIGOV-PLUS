using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class AgroParte4ApiTests
{
    private static readonly string ProgramasController = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Sigov.Api", "Controllers", "AgroProgramasController.cs"));
    private static readonly string PatrulhaController = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Sigov.Api", "Controllers", "AgroPatrulhaMecanizadaController.cs"));

    [Fact] public void Controllers_exigem_login_e_modulo_agro() { ProgramasController.Should().Contain("Authorize").And.Contain("RequireModule(\"agro\")"); PatrulhaController.Should().Contain("Authorize").And.Contain("RequireModule(\"agro\")"); }
    [Fact] public void Programas_beneficios_concessoes_e_insumos_possuem_rotas_rest() { ProgramasController.Should().Contain("api/agro/programas"); ProgramasController.Should().Contain("api/agro/beneficios/concessoes/{id:long}/autorizar"); ProgramasController.Should().Contain("api/agro/insumos/distribuicoes"); }
    [Fact] public void Maquinas_agenda_e_servicos_possuem_rotas_rest() { PatrulhaController.Should().Contain("api/agro/maquinas"); PatrulhaController.Should().Contain("api/agro/maquinas/agenda"); PatrulhaController.Should().Contain("api/agro/servicos-maquina/{id:long}/executar"); }
    [Fact] public void Erros_de_acesso_sao_mapeados() { ProgramasController.Should().Contain("Unauthorized").And.Contain("Forbid"); PatrulhaController.Should().Contain("UnprocessableEntity"); }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
