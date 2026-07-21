using System.Xml.Linq;
using FluentAssertions;
using Sigov.Testing;

namespace Sigov.UnitTests;

public sealed class ArchitectureDependencyTests
{
    private static readonly IReadOnlyDictionary<string, string[]> ForbiddenReferences = new Dictionary<string, string[]>
    {
        ["src/Sigov.Domain/Sigov.Domain.csproj"] = new[] { "Sigov.Application", "Sigov.Infrastructure", "Sigov.Api", "Sigov.Web", "Sigov.Worker" },
        ["src/Sigov.Application/Sigov.Application.csproj"] = new[] { "Sigov.Infrastructure", "Sigov.Api", "Sigov.Web", "Sigov.Worker" },
        ["src/Sigov.Infrastructure/Sigov.Infrastructure.csproj"] = new[] { "Sigov.Api", "Sigov.Web", "Sigov.Worker" }
    };

    [Fact]
    public void Projetos_De_Nucleo_Nao_Devem_Referenciar_Camadas_Externas()
    {
        var violations = new List<string>();

        foreach (var rule in ForbiddenReferences)
        {
            var projectPath = TestRepoPath.Get(rule.Key);
            var references = ReadProjectReferences(projectPath);

            foreach (var forbidden in rule.Value)
            {
                if (references.Any(reference => reference.Contains($"/{forbidden}/", StringComparison.OrdinalIgnoreCase) || reference.Contains($"\\{forbidden}\\", StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{rule.Key} referencia camada proibida {forbidden}");
                }
            }
        }

        violations.Should().BeEmpty("a Clean Architecture do SIGOV PLUS exige dependências apontando apenas para dentro");
    }

    [Fact]
    public void Projetos_De_Borda_Devem_Referenciar_Apenas_Application_E_Infrastructure_Entre_Camadas_Sigov()
    {
        var edgeProjects = new[]
        {
            "src/Sigov.Api/Sigov.Api.csproj",
            "src/Sigov.Web/Sigov.Web.csproj",
            "src/Sigov.Worker/Sigov.Worker.csproj"
        };
        var allowed = new[] { "Sigov.Application", "Sigov.Infrastructure" };
        var violations = new List<string>();

        foreach (var project in edgeProjects)
        {
            foreach (var reference in ReadProjectReferences(TestRepoPath.Get(project)))
            {
                var name = Path.GetFileNameWithoutExtension(reference);
                if (name.StartsWith("Sigov.", StringComparison.OrdinalIgnoreCase) && !allowed.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    violations.Add($"{project} possui referência inválida para {name}");
                }
            }
        }

        violations.Should().BeEmpty("API, Web e Worker devem depender de Application/Infrastructure, sem acoplamento lateral");
    }

    private static IReadOnlyList<string> ReadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar) ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}
