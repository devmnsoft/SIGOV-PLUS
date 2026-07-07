using Xunit;

namespace Sigov.IntegrationTests;

public sealed class PosRc03WebOperationalStaticTests
{
    private static readonly string ProtocoloController = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ProtocoloController.cs"));
    private static readonly string GedController = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/GedController.cs"));
    private static readonly string RealService = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Services/PosRcWebOperationalService.cs"));
    private static readonly string RelatoriosController = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/RelatoriosController.cs"));
    private static readonly string BuscaController = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/BuscaController.cs"));
    private static readonly string PocService = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Services/Editais/EditalPocService.cs"));

    [Fact]
    public void ProtocoloController_Deve_Usar_Persistencia_Real_E_Permissoes()
    {
        Assert.Contains("protocolo.criar", ProtocoloController);
        Assert.Contains("protocolo.tramitar", ProtocoloController);
        Assert.Contains("CriarProtocoloAsync", ProtocoloController);
        Assert.Contains("TramitarProtocoloAsync", ProtocoloController);
        Assert.Contains("insert into sigov.protocolo", RealService);
        Assert.Contains("insert into sigov.workflow_instancia", RealService);
        Assert.Contains("insert into sigov.tarefa", RealService);
        Assert.Contains("insert into sigov.notificacao", RealService);
        Assert.Contains("protocolo.criado", RealService);
        Assert.Contains("protocolo.tramitado", RealService);
    }

    [Fact]
    public void GedController_Deve_Usar_Storage_Hash_Versao_Validacao_E_Permissoes()
    {
        Assert.Contains("ged.upload", GedController);
        Assert.Contains("ged.download", GedController);
        Assert.Contains("SHA256.HashData", GedController);
        Assert.Contains("insert into sigov.documento", GedController);
        Assert.Contains("insert into sigov.documento_versao", GedController);
        Assert.Contains("portal_validacao_documento", GedController);
        Assert.Contains("documento.criado", GedController);
        Assert.DoesNotContain("return PhysicalFile", GedController);
    }

    [Fact]
    public void Relatorios_Busca_E_Poc_Devem_Proteger_Lgpd_E_Fallback()
    {
        Assert.Contains("LgpdMaskingHelper", RelatoriosController);
        Assert.Contains("AuditarExportacaoAsync", RelatoriosController);
        Assert.Contains("BuscarAsync", BuscaController);
        Assert.Contains("MensagemFallback", BuscaController);
        Assert.Contains("Não Atende", PocService);
    }
}
