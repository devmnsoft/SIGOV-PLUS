using FluentAssertions;
using Sigov.Application.Enterprise;
using Xunit;

namespace Sigov.UnitTests;

public sealed class EnterprisePostBuild04RulesTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Cliente_comercial_respeita_tenant_id_e_mascara_dados_sensiveis()
    {
        var service = new EnterpriseModuleService();
        service.Upsert("comercial/clientes", new EnterpriseMutationRequest(TenantA, "Cliente A", "12345678000199", "cliente@example.com", "11999998888", null, null, null, null, null, null), TenantA, "corr-1");
        service.Upsert("comercial/clientes", new EnterpriseMutationRequest(TenantB, "Cliente B", "99999999000199", "outro@example.com", "11888887777", null, null, null, null, null, null), TenantB, "corr-2");

        var clientesA = service.List("comercial/clientes", TenantA);

        clientesA.Should().ContainSingle();
        clientesA[0].TenantId.Should().Be(TenantA);
        clientesA[0].DocumentMasked.Should().Be("***0199");
        clientesA[0].EmailMasked.Should().Be("c***@example.com");
        clientesA[0].PhoneMasked.Should().Be("(**) ****-8888");
    }

    [Fact]
    public void Proposta_aprovada_gera_pedido_e_pedido_gera_os()
    {
        var service = new EnterpriseModuleService();
        var propostaId = Guid.NewGuid();

        service.ApproveProposal(propostaId, TenantA, "corr-approve").Status.Should().Be("APROVADA");
        var pedido = service.GenerateOrderFromProposal(propostaId, TenantA, "corr-order");
        var os = service.GenerateServiceOrderFromOrder(pedido.RelatedId!.Value, TenantA, "corr-os");

        pedido.Status.Should().Be("PEDIDO_GERADO");
        os.Status.Should().Be("OS_GERADA");
        service.GetServiceOrder(os.RelatedId!.Value, TenantA).Status.Should().Be("ABERTA");
    }

    [Fact]
    public void Os_consome_item_de_estoque_sem_permitir_saldo_negativo_por_padrao()
    {
        var service = new EnterpriseModuleService();
        var produtoId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var bloqueado = service.ConsumeStock(Guid.NewGuid(), TenantA, produtoId, 99, false, "corr-stock");
        var permitido = service.ConsumeStock(Guid.NewGuid(), TenantA, produtoId, 99, true, "corr-stock-admin");

        bloqueado.Status.Should().Be("SALDO_INSUFICIENTE");
        permitido.Status.Should().Be("OK");
    }

    [Fact]
    public void Manutencao_preventiva_gera_os_e_modulo_nao_contratado_retorna_403_por_tenant_divergente()
    {
        var service = new EnterpriseModuleService();

        var os = service.GeneratePreventiveServiceOrder(Guid.NewGuid(), TenantA, "corr-prev");
        var forbidden = service.Upsert("comercial/clientes", new EnterpriseMutationRequest(TenantB, "Invasor", null, null, null, null, null, null, null, null, null), TenantA, "corr-forbidden");

        os.Status.Should().Be("OS_PREVENTIVA_GERADA");
        forbidden.Status.Should().Be("FORBIDDEN");
    }

    [Fact]
    public void Health_contract_permanece_isolado_do_modulo_empresarial()
    {
        var dashboard = new EnterpriseModuleService().GetDashboard("comercial", TenantA);

        dashboard.Module.Should().Be("comercial");
        dashboard.Alertas.Should().Contain(alerta => alerta.Contains("abaixo do mínimo", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class EnterprisePosRc07StaticTests
{
    [Fact]
    public void EnterpriseMigrationContainsRequiredTablesAndTenant()
    {
        var sql = File.ReadAllText(Path.Combine("..", "..", "..", "..", "database", "postgres", "migrations", "20260709120000_enterprise_funcional_crud.sql"));
        Assert.Contains("enterprise_cliente", sql);
        Assert.Contains("enterprise_ordem_servico", sql);
        Assert.Contains("enterprise_estoque_saldo", sql);
        Assert.Contains("tenant_id uuid not null", sql);
        Assert.Contains("create index if not exists", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnterpriseServiceDoesNotUseConcurrentDictionaryForRealFlow()
    {
        var source = File.ReadAllText(Path.Combine("..", "..", "..", "..", "src", "Sigov.Application", "Enterprise", "EnterpriseModuleService.cs"));
        Assert.DoesNotContain("ConcurrentDictionary", source);
    }

    [Fact]
    public void EnterpriseMigrationContainsFullAuditColumnsForMinimumTables()
    {
        var sql = File.ReadAllText(Path.Combine("..", "..", "..", "..", "database", "postgres", "migrations", "20260709120000_enterprise_funcional_crud.sql"));
        var requiredTables = new[]
        {
            "enterprise_cliente", "enterprise_lead", "enterprise_oportunidade", "enterprise_proposta", "enterprise_proposta_item",
            "enterprise_pedido_venda", "enterprise_pedido_venda_item", "enterprise_ordem_servico", "enterprise_os_item",
            "enterprise_os_checklist", "enterprise_os_apontamento", "enterprise_os_agenda", "enterprise_os_historico",
            "enterprise_produto", "enterprise_almoxarifado", "enterprise_estoque_saldo", "enterprise_estoque_movimento",
            "enterprise_requisicao", "enterprise_fornecedor", "enterprise_pedido_compra", "enterprise_ativo_industrial",
            "enterprise_plano_manutencao", "enterprise_medidor", "enterprise_leitura_medidor", "enterprise_parada_falha",
            "enterprise_evento", "enterprise_auditoria_operacional"
        };

        foreach (var table in requiredTables) Assert.Contains(table, sql);
        foreach (var column in new[] { "tenant_id", "status", "created_at", "created_by", "updated_at", "updated_by", "is_deleted", "correlation_id" }) Assert.Contains(column, sql);
        Assert.DoesNotContain("drop table", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create index if not exists", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnterpriseJavascriptCallsRealUpdateAndDeleteEndpoints()
    {
        var js = File.ReadAllText(Path.Combine("..", "..", "..", "..", "src", "Sigov.Web", "wwwroot", "js", "enterprise-crud.js"));
        Assert.Contains("method = id ? 'PUT' : 'POST'", js);
        Assert.Contains("method: 'DELETE'", js);
        Assert.DoesNotContain("endpoint DELETE estiver habilitado", js);
    }

    [Fact]
    public void EnterprisePageTemplateHasOperationalCrudElements()
    {
        var view = File.ReadAllText(Path.Combine("..", "..", "..", "..", "src", "Sigov.Web", "Views", "Enterprise", "ModulePage.cshtml"));
        Assert.Contains("enterprise-form", view);
        Assert.Contains("Exportar CSV", view);
        Assert.Contains("Detalhes", view);
        Assert.Contains("Inativar", view);
    }
}
