using FluentAssertions;
using Sigov.Application.Security;
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
        exceptionMiddleware.Should().Contain("ApiResponse<object>.Fail");
        exceptionMiddleware.Should().Contain("Não foi possível processar a solicitação");
        exceptionMiddleware.Should().NotContain("StackTrace");
    }

    [Fact]
    public void Api_Deve_Autenticar_Antes_De_Autorizar_E_Usar_Fallback_FailClosed()
    {
        var program = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Program.cs"));
        var apiKeyMiddleware = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Middlewares", "ApiKeyV1Middleware.cs"));
        var gedController = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Controllers", "GedController.cs"));

        program.Should().Contain("AddAuthentication(options =>");
        program.Should().Contain("options.FallbackPolicy");
        program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal).Should()
            .BeLessThan(program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal));
        apiKeyMiddleware.Should().Contain("new ClaimsIdentity(claims, SigovApiAuthenticationHandler.SchemeName)");
        apiKeyMiddleware.Should().Contain("new(\"tenant_id\", row.TenantId.ToString");
        gedController.Should().Contain("User.Identity?.IsAuthenticated == true &&");
        gedController.Should().NotContain("User.Identity?.IsAuthenticated != true || User.IsInRole");
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

    [Fact]
    public void Web_Deve_Manter_Permissoes_Fora_Do_Ticket_E_Carrega_Las_Uma_Vez_Por_Requisicao()
    {
        var controller = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Web", "Controllers", "AuthController.cs"));
        var program = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Web", "Program.cs"));
        var transformation = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Web", "Services", "RequestPermissionClaimsTransformation.cs"));

        controller.Should().NotContain("access.Permissions.Select(permission => new Claim(\"permission\"");
        controller.Should().Contain("TicketDataFormat.Protect(ticket)");
        controller.Should().Contain("protectedTicketSize");
        program.Should().Contain("AddScoped<IClaimsTransformation, RequestPermissionClaimsTransformation>");
        transformation.Should().Contain("principal.Clone()");
        transformation.Should().Contain("_access ??=");
        transformation.Should().Contain("_modules ??=");
        transformation.Should().Contain("GetRequestAccessAsync");
        transformation.Should().NotContain("SignInAsync");
        transformation.Should().NotContain("GetAwaiter().GetResult()");
    }

    [Theory]
    [InlineData("529.982.247-25", AuthenticationIdentifierKind.Cpf, "52998224725")]
    [InlineData("04.252.011/0001-10", AuthenticationIdentifierKind.Cnpj, "04252011000110")]
    [InlineData(" USUARIO@ORGAO.GOV.BR ", AuthenticationIdentifierKind.Email, "usuario@orgao.gov.br")]
    [InlineData("admin-legado", AuthenticationIdentifierKind.LegacyLogin, "admin-legado")]
    public void Identificador_De_Login_Deve_Normalizar_Formatos_Suportados(string input, AuthenticationIdentifierKind kind, string expected)
    {
        var result = AuthenticationIdentifierNormalizer.Normalize(input);

        result.IsValid.Should().BeTrue();
        result.Kind.Should().Be(kind);
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("111.111.111-11")]
    [InlineData("00.000.000/0000-00")]
    [InlineData("email-invalido@")]
    public void Identificador_De_Login_Deve_Rejeitar_Documento_Ou_Email_Invalido(string input)
    {
        AuthenticationIdentifierNormalizer.Normalize(input).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Web_Deve_Limpar_Cookie_Base_E_Chunks_Legados_Com_Limite_Defensivo()
    {
        var controller = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Web", "Controllers", "AuthController.cs"));

        controller.Should().Contain("DeleteLegacyAuthenticationChunks()");
        controller.Should().Contain("DeleteLegacyAuthenticationChunks(includeBaseCookie: true)");
        controller.Should().Contain("maximumChunksToDelete = 64");
        controller.Should().Contain("Response.Cookies.Delete(key, options)");
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
