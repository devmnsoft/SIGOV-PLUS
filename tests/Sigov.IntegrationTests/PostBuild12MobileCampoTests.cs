using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class PostBuild12MobileCampoTests
{
    [Fact]
    public void Banco_DeveConterTabelasMobileCampoSyncGeoELgpd()
    {
        var sql = File.ReadAllText(TestRepoPath.Get("database/script_completo.sql"));
        var migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260611130000_pos_build_12_mobile_pwa_campo_offline_geo.sql"));

        sql.Should().Contain("sigov.mobile_dispositivo");
        sql.Should().Contain("sigov.mobile_sync_lote");
        sql.Should().Contain("sigov.mobile_sync_item");
        sql.Should().Contain("sigov.campo_atividade");
        sql.Should().Contain("sigov.campo_evidencia");
        sql.Should().Contain("sigov.campo_assinatura");
        sql.Should().Contain("sigov.campo_localizacao");
        sql.Should().Contain("consentimento varchar(120) not null");
        sql.Should().Contain("mobile_consumo_billing");
        migration.Should().Contain("on conflict(tenant_id,modulo_codigo,entidade)");
    }

    [Fact]
    public void Catalogo_DeveConterModulosAddonsPacotesMobileCampo()
    {
        var catalogo = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Commercial/ModuleCatalogService.cs"));
        var saas = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Saas/Modules/ModuleCatalogService.cs"));
        var sql = File.ReadAllText(TestRepoPath.Get("database/script_completo.sql"));

        catalogo.Should().Contain("mobile_pwa");
        catalogo.Should().Contain("campo_operacional");
        catalogo.Should().Contain("georreferenciamento");
        catalogo.Should().Contain("offline_sync");
        catalogo.Should().Contain("assinatura_campo");
        catalogo.Should().Contain("notificacoes_mobile");
        catalogo.Should().Contain("CAMPO_STARTER");
        catalogo.Should().Contain("GOV_CAMPO_FULL");
        saas.Should().Contain("FIELD_SERVICE_PRO");
        sql.Should().Contain("mobile_usuarios_extra");
        sql.Should().Contain("storage_fotos_campo");
        sql.Should().Contain("geolocalizacao_avancada");
        sql.Should().Contain("sincronizacao_offline_avancada");
    }

    [Fact]
    public void Api_DeveExporEndpointsMobileCampoComSyncAssinaturaGeoAuditoria()
    {
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/MobileCampoController.cs"));

        controller.Should().Contain("api/mobile/dispositivos/registrar");
        controller.Should().Contain("api/mobile/sync/upload");
        controller.Should().Contain("api/campo/atividades/{id:long}/concluir");
        controller.Should().Contain("api/campo/atividades/{id:long}/checklist/responder");
        controller.Should().Contain("api/campo/atividades/{id:long}/evidencias");
        controller.Should().Contain("api/campo/atividades/{id:long}/assinatura");
        controller.Should().Contain("api/campo/localizacao");
        controller.Should().Contain("EnsureDeviceActive");
        controller.Should().Contain("Hash(assinatura)");
        controller.Should().Contain("Item inválido mantido no lote");
        controller.Should().Contain("Audit(c, tenantId");
    }

    [Fact]
    public void Pwa_Web_Menu_Documentacao_E_Demo_DeveConterEntregaMobileCampo()
    {
        File.Exists(TestRepoPath.Get("src/Sigov.Web/wwwroot/manifest.json")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/wwwroot/service-worker.js")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Offline/Index.cshtml")).Should().BeTrue();

        var web = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/MobileCampoController.cs"));
        var sidebar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml"));
        var demo = File.ReadAllText(TestRepoPath.Get("scripts/demo-local.ps1"));

        web.Should().Contain("MobileController");
        web.Should().Contain("CampoController");
        sidebar.Should().Contain("data-module=\"mobile_pwa\"");
        sidebar.Should().Contain("data-module=\"campo_operacional\"");
        sidebar.Should().Contain("data-module=\"georreferenciamento\"");
        demo.Should().Contain("http://localhost:8080/Mobile/Home");
        File.Exists(TestRepoPath.Get("docs/mobile-pwa.md")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("docs/offline-sync.md")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("docs/lgpd-mobile-campo.md")).Should().BeTrue();
    }
}
