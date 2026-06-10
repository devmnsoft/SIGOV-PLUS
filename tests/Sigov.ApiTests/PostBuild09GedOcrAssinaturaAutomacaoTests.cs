using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PostBuild09GedOcrAssinaturaAutomacaoTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260610220000_pos_build_09_ged_ocr_assinatura_automacao.sql"));
    private static readonly string ScriptCompleto = File.ReadAllText(TestRepoPath.Get("database/script_completo.sql"));
    private static readonly string Api = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/GedController.cs"));
    private static readonly string Web = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/GedController.cs"));
    private static readonly string Sidebar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml"));

    [Fact]
    public void Migration_cria_ged_ocr_contratos_protocolos_workflow_e_tramitacao_multi_tenant()
    {
        foreach (var table in new[] { "ged_documento", "ged_anexo", "ged_indice", "ged_historico", "ged_assinatura", "ged_workflow", "protocolo", "contrato", "fluxo_tramitacao", "ocr_digitalizacao" })
        {
            Migration.Should().Contain($"create table if not exists sigov.{table}");
            ScriptCompleto.Should().Contain($"create table if not exists sigov.{table}");
        }

        Migration.Should().Contain("tenant_id bigint not null references sigov.tenant(id)")
            .And.Contain("idx_ged_documento_tenant_status")
            .And.Contain("idx_ged_documento_metadata")
            .And.Contain("idx_ocr_tenant_status")
            .And.Contain("GED_AUTOMACAO_PLUS")
            .And.Contain("WF_GED_BASICO");
    }

    [Fact]
    public void Permissoes_e_catalogo_saas_documental_estao_seedados()
    {
        foreach (var permissao in new[] { "ged.visualizar", "ged.upload", "ged.download", "ged.indexar", "ged.assinar", "ged.tramitar", "contrato.visualizar", "contrato.criar", "contrato.assinar", "fluxo.visualizar", "ocr.processar" })
        {
            Migration.Should().Contain(permissao);
        }

        File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Commercial/ModuleCatalogService.cs"))
            .Should().Contain("GED/OCR e Automação Documental")
            .And.Contain("/Ged/Dashboard")
            .And.Contain("GED_AUTOMACAO_PLUS");

        File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Saas/Modules/ModuleCatalogService.cs"))
            .Should().Contain("GED/OCR e Automação Documental")
            .And.Contain("ocr.processar")
            .And.Contain("GED_AUTOMACAO_PLUS");
    }

    [Fact]
    public void Api_expoe_upload_download_ocr_assinatura_tramitacao_contratos_historico_e_auditoria()
    {
        Api.Should().Contain("[RequireModule(\"ged\")]")
            .And.Contain("HttpPost(\"documentos\")")
            .And.Contain("HttpPost(\"documentos/{id:long}/anexos\")")
            .And.Contain("HttpGet(\"documentos/{id:long}/download\")")
            .And.Contain("HttpPost(\"documentos/{id:long}/ocr\")")
            .And.Contain("HttpPost(\"documentos/{id:long}/assinaturas/simular\")")
            .And.Contain("HttpPost(\"documentos/{id:long}/tramitar\")")
            .And.Contain("HttpGet(\"documentos/{id:long}/historico\")")
            .And.Contain("HttpPost(\"contratos\")")
            .And.Contain("tenant_id=@TenantId")
            .And.Contain("Auditar")
            .And.Contain("classificacao_lgpd");
    }

    [Fact]
    public void Telas_demo_e_documentacao_do_modulo_documental_foram_entregues()
    {
        foreach (var action in new[] { "Dashboard", "Documentos", "Upload", "Pesquisa", "Workflow", "Historico", "AssinaturaTeste", "Contratos", "Tramitacoes", "Ocr" })
        {
            Web.Should().Contain($"IActionResult {action}");
            File.Exists(TestRepoPath.Get($"src/Sigov.Web/Views/Ged/{action}.cshtml")).Should().BeTrue();
        }

        Sidebar.Should().Contain("/Ged/Dashboard")
            .And.Contain("ged.visualizar")
            .And.Contain("/Ged/Workflow")
            .And.Contain("contrato.visualizar");

        File.ReadAllText(TestRepoPath.Get("scripts/demo-local.ps1")).Should().Contain("SIGOV Pós-Build 09");
        File.Exists(TestRepoPath.Get("docs/ged-ocr-assinatura-automacao.md")).Should().BeTrue();
    }
}
