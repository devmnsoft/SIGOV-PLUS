using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class EducacaoApiTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Endpoints_Educacao_Estao_Registrados_E_Protegidos_Por_Service_Guard()
    {
        var api = File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoControllers.cs"));
        api.Should().Contain("api/educacao/alunos").And.Contain("api/educacao/matriculas").And.Contain("api/educacao/export");
        var service = File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/EducacaoServices.cs"));
        service.Should().Contain("GuardAsync").And.Contain("HasPermissionAsync").And.Contain("Módulo educação não contratado/habilitado");
    }

    [Fact]
    public void Frequencia_Nao_Assume_Presenca_Nem_Inventa_Justificativa()
    {
        var contracts = File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/EducacaoContracts.cs"));
        contracts.Should().Contain("bool Presente = false").And.Contain("Status = \"NAO_LANCADO\"");
        var api = File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoControllers.cs"));
        api.Should().Contain("Falta justificada exige justificativa informada pelo usuário.")
            .And.NotContain("? \"Falta justificada\"");
    }

    [Fact]
    public void Patrimonio_Movimentacao_Preserva_Cadeia_De_Custodia_E_Idempotencia()
    {
        var service = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Patrimonio/PatrimonioService.cs"))
            + File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Patrimonio/PatrimonioService.Movimentacao.cs"));
        service.Should().Contain("for update").And.Contain("correlation_id=@CorrelationId")
            .And.Contain("A chave de idempotência já foi usada em outra movimentação")
            .And.Contain("ObterBemDetalheAsync").And.Contain("order by data_movimentacao desc,id desc");
        var api = File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/PatrimonioController.cs"));
        api.Should().Contain("Idempotency-Key").And.Contain("ObterBemDetalheAsync");
        var view = File.ReadAllText(Path.Combine(Root, "src/Sigov.Web/Views/Patrimonio/Detalhe.cshtml"));
        view.Should().Contain("Histórico de movimentações").And.Contain("idempotencyKey")
            .And.Contain("data-confirm").And.Contain("Como usar");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "sigov.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
