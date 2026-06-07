using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class AuthPermissionRegressionTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Api_Deve_Ter_Middleware_De_Erros_Sem_StackTrace_E_Headers_De_Seguranca()
    {
        var program = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Program.cs"));
        var exceptionMiddleware = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Middlewares", "ExceptionHandlingMiddleware.cs"));

        program.Should().Contain("UseMiddleware<ExceptionHandlingMiddleware>");
        program.Should().Contain("UseMiddleware<SecurityHeadersMiddleware>");
        exceptionMiddleware.Should().Contain("ProblemDetails");
        exceptionMiddleware.Should().Contain("Não foi possível processar a solicitação");
        exceptionMiddleware.Should().NotContain("StackTrace");
    }

    [Fact]
    public void Services_De_Modulos_Devem_Validar_Permissoes_E_Tenant_Antes_De_Acessar_Dados()
    {
        var serviceFiles = new[]
        {
            "src/Sigov.Application/Processos/ProcessosServices.cs",
            "src/Sigov.Application/Financeiro/FinanceiroServices.cs",
            "src/Sigov.Application/Rh/RhServices.cs",
            "src/Sigov.Application/Educacao/EducacaoServices.cs",
            "src/Sigov.Application/Saude/SaudeServices.cs",
            "src/Sigov.Application/Saneamento/SaneamentoServices.cs",
            "src/Sigov.Application/Social/SocialServices.cs",
            "src/Sigov.Application/Integracoes/IntegracoesApplication.cs"
        };

        foreach (var relativePath in serviceFiles)
        {
            var source = File.ReadAllText(Path.Combine(Root, relativePath));
            source.Should().Contain("TenantId", $"{relativePath} deve exigir contexto tenant");
            source.Should().Contain("HasPermissionAsync", $"{relativePath} deve validar permissão");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório sigov não encontrada.");
    }
}
