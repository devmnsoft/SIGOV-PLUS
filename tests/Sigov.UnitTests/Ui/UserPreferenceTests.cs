using FluentAssertions;
using Sigov.Application.Ui;
using Xunit;

namespace Sigov.UnitTests.Ui;

public sealed class UserPreferenceTests
{
    [Fact]
    public void Preferencia_De_Tema_Deve_Salvar_E_Ler()
    {
        var service = new UserPreferenceService();

        service.Save(new UserPreferenceUpdateRequest(1, 2, "tema", "{\"value\":\"dark\"}"));
        var saved = service.Get(1, 2, "tema");

        saved.ValueJson.Should().Contain("dark");
    }
}
