using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PostBuild08TributarioAvancadoTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260610200000_pos_build_08_tributario_avancado_iptu_iss_dam.sql"));
    private static readonly string Api = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/TributarioController.cs"));
    private static readonly string Web = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/TributarioController.cs"));
    private static readonly string Sidebar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml"));

    [Fact]
    public void Migration_cria_tabelas_tributarias_avancadas_com_tenant_indices_e_triggers()
    {
        foreach (var table in new[] { "tributos_impostos", "iptu", "iss", "taxas_municipais", "contribuinte", "parcela", "arrecadacao", "documento_arrecadacao_municipal", "livro_eletronico_tributario", "parcelamento_divida_ativa", "integracao_nfse" })
        {
            Migration.Should().Contain($"create table if not exists sigov.{table}");
        }

        Migration.Should().Contain("tenant_id bigint not null references sigov.tenant(id)")
            .And.Contain("create index if not exists idx_iptu_tenant_inscricao")
            .And.Contain("create index if not exists idx_parcela_tenant_vencimento")
            .And.Contain("fn_tributario_avancado_set_updated_at")
            .And.Contain("on conflict");
    }

    [Fact]
    public void Permissoes_e_catalogo_saas_do_tributario_avancado_estao_seedados()
    {
        Migration.Should().Contain("tributario.dashboard.visualizar")
            .And.Contain("tributario.iptu.editar")
            .And.Contain("tributario.arrecadacao.registrar")
            .And.Contain("tributario.nfse.emitir")
            .And.Contain("tributario.livro_eletronico.gerar")
            .And.Contain("GOV_TRIBUTARIO_PLUS");

        File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Commercial/ModuleCatalogService.cs"))
            .Should().Contain("Tributário Avançado")
            .And.Contain("/Tributario/Dashboard")
            .And.Contain("tributario.dashboard.visualizar");
    }

    [Fact]
    public void Api_expoe_crud_filtros_dam_nfse_arrecadacao_livro_e_auditoria_por_tenant()
    {
        Api.Should().Contain("[RequireModule(\"tributario\")]")
            .And.Contain("tenant_id=@TenantId")
            .And.Contain("HttpPost(\"dam/emitir\")")
            .And.Contain("HttpPost(\"nfse/simular\")")
            .And.Contain("HttpGet(\"arrecadacao/status\")")
            .And.Contain("HttpPost(\"livro-eletronico/gerar\")")
            .And.Contain("financeiro_conta_receber")
            .And.Contain("correlationId")
            .And.Contain("HasPermissionAsync")
            .And.Contain("right(documento,4)");
    }

    [Fact]
    public void Telas_docs_e_demo_do_tributario_avancado_foram_entregues()
    {
        foreach (var view in new[] { "Iptu", "Iss", "Taxas", "Parcelamentos", "Arrecadacao", "LivroEletronico", "RelatoriosFiscais", "Nfse" })
        {
            File.Exists(TestRepoPath.Get($"src/Sigov.Web/Views/Tributario/{view}.cshtml")).Should().BeTrue();
        }

        Web.Should().Contain("Lista(\"Contribuintes\"")
            .And.Contain("Lista(\"Lançamentos tributários\"")
            .And.Contain("Lista(\"Dívida ativa\"")
            .And.Contain("Lista(\"Parcelamentos\"");

        Sidebar.Should().Contain("/Tributario/Dashboard")
            .And.Contain("/Tributario/Contribuintes")
            .And.Contain("/Tributario/Nfse/LivroEletronico");
        File.ReadAllText(TestRepoPath.Get("scripts/demo-local.ps1")).Should().Contain("SIGOV Pós-Build 08");
        File.Exists(TestRepoPath.Get("docs/tributario-avancado.md")).Should().BeTrue();
    }

    [Fact]
    public void Csv_sanitiza_formulas_e_delimitadores_corretamente()
    {
        Sigov.Infrastructure.Tributario.TributarioAvancadoRepository.SanitizarCsvCampo("=cmd|' /C calc'!A0")
            .Should().StartWith("'=");
        Sigov.Infrastructure.Tributario.TributarioAvancadoRepository.SanitizarCsvCampo("+12345")
            .Should().StartWith("'+");
        Sigov.Infrastructure.Tributario.TributarioAvancadoRepository.SanitizarCsvCampo("-exec")
            .Should().StartWith("'-");
        Sigov.Infrastructure.Tributario.TributarioAvancadoRepository.SanitizarCsvCampo("@SUM(B1)")
            .Should().StartWith("'@");
        Sigov.Infrastructure.Tributario.TributarioAvancadoRepository.SanitizarCsvCampo("normal")
            .Should().Be("normal");
        Sigov.Infrastructure.Tributario.TributarioAvancadoRepository.SanitizarCsvCampo("com;delimitador")
            .Should().Be("\"com;delimitador\"");
    }

    [Fact]
    public void PortalContribuinte_rotas_de_debitos_pagamentos_e_validacao_publica_estao_protegidas()
    {
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/PortalContribuinteController.cs"));
        controller.Should().Contain("contribuintes/{codigo}/debitos")
            .And.Contain("contribuintes/{codigo}/pagamentos")
            .And.Contain("ResolverContribuinteAutorizadoAsync")
            .And.Contain("ValidarTitularidadeOperacaoAsync")
            .And.Contain("ValidarCertidaoPublicaAsync")
            .And.Contain("NotFound")
            .And.Contain("AllowAnonymous");

        var repo = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Tributario/TributarioAvancadoRepository.cs"));
        repo.Should().Contain("ObterContribuintesAutorizadosAsync")
            .And.Contain("ListarDebitosAsync")
            .And.Contain("ListarPagamentosAsync")
            .And.Contain("ValidarCertidaoPublicaAsync")
            .And.Contain("SanitizarCsvCampo")
            .And.Contain("tributario_carne_emissao");
    }

    [Fact]
    public void XSS_pontos_em_importacao_e_telas_de_rh_eliminam_interpolacao_em_innerhtml()
    {
        var importJs = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/modules/operational-import.js"));
        importJs.Should().NotContain("${i.field}: ${i.error}")
            .And.Contain("replaceChildren")
            .And.Contain("createTextNode");

        var rhView = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Rh/PortalContracheques.cshtml"));
        rhView.Should().NotContain("${i.dados?.status??'ATIVO'}")
            .And.Contain("replaceChildren")
            .And.Contain("createElement('tr')");
    }

    [Fact]
    public void CSP_e_permissoes_possuem_configuracao_segura()
    {
        var program = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Program.cs"));
        program.Should().Contain("CspNonce")
            .And.Contain("nonce-");

        var perms = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/sigov.permissions.js"));
        perms.Should().Contain("userPermissions")
            .And.Contain("servidor");

        var scriptDb = File.ReadAllText(TestRepoPath.Get("scripts/provision-sigov-db-user.ps1"));
        scriptDb.Should().Contain("$identRegex")
            .And.Contain("$quotedUser");
    }
}
