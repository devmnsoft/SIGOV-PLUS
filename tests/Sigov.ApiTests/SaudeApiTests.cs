using System.Reflection;
using Sigov.Api.Controllers;
using Xunit;

namespace Sigov.ApiTests;

public sealed class SaudeApiTests
{
    [Fact]
    public void Controllers_saude_estao_presentes()
    {
        var asm = typeof(SaudeDashboardController).Assembly;
        Assert.Contains(asm.GetTypes(), t => t.Name == "PacientesController");
        Assert.Contains(asm.GetTypes(), t => t.Name == "AcsSyncController");
        Assert.Contains(asm.GetTypes(), t => t.Name == "SaudeExportacaoController");
    }
}
