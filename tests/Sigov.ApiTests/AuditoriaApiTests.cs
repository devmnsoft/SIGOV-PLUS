using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.ApiTests;

public sealed class AuditoriaApiTests
{
    [Fact]
    public void Auditoria_Deve_Ter_Telas_Com_Filtros()
    {
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Auditoria/Trilhas.cshtml")).Should().Contain("Tabela").And.Contain("Chave");
    }
}
