using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PostBuild01RegressionTests
{
    [Fact]
    public void PosBuild01_Deve_Publicar_Rotas_Web_Principais()
    {
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Controllers/AuthController.cs")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Controllers/DashboardController.cs")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Saas/Tenants.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Saas/Modulos.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Operacao/Health.cshtml")).Should().BeTrue();
    }

    [Fact]
    public void PosBuild01_Deve_Ter_Seed_Admin_E_Auditoria_Idempotente()
    {
        var migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260609090000_pos_build_dashboard_saas.sql"));
        migration.Should().Contain("create table if not exists sigov.auditoria_evento");
        migration.Should().Contain("baseline estrutural não cria usuário, e-mail ou senha administrativa padrão");
        migration.Should().NotContain("admin@sigov.local");
        migration.Should().NotContain("SIGOV_PBKDF2_V1");
        migration.Should().Contain("on conflict");
        migration.ToLowerInvariant().Should().NotContain("drop table");
    }

    [Fact]
    public void PosBuild01_Deve_Ter_Apis_Saas_E_Health_Visual()
    {
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/SaasTenantsController.cs")).Should().Contain("api/saas/tenants");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/SaasModulesController.cs")).Should().Contain("/ativar").And.Contain("/desativar");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/OperacaoHealthController.cs")).Should().Contain("api/operacao/health");
    }

    [Fact]
    public void PosBuild01_Deve_Ter_Documentacao_E_Scripts_De_Ambiente_Local()
    {
        File.Exists(TestRepoPath.Get("docs/ambiente-local.md")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("scripts/check-local.ps1")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("scripts/demo-local.ps1")).Should().BeTrue();
    }

    [Fact]
    public void Recebimento_Deve_Revalidar_Idempotencia_Depois_Do_Lock_Do_Pedido()
    {
        var repository = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        var methodStart = repository.IndexOf("public async Task<RecebimentoCriado> CriarEConfirmarAsync", StringComparison.Ordinal);
        methodStart.Should().BeGreaterThanOrEqualTo(0);

        var methodEnd = repository.IndexOf("private static string? Clean", methodStart, StringComparison.Ordinal);
        methodEnd.Should().BeGreaterThan(methodStart);

        var method = repository[methodStart..methodEnd];
        var lockPosition = method.IndexOf("for update", StringComparison.OrdinalIgnoreCase);
        var queryPositions = System.Text.RegularExpressions.Regex.Matches(method, "new CommandDefinition\\(idempotencyQuery,idempotencyArgs")
            .Select(match => match.Index)
            .ToArray();

        lockPosition.Should().BeGreaterThanOrEqualTo(0);
        queryPositions.Should().HaveCount(2);
        queryPositions[0].Should().BeLessThan(lockPosition);
        queryPositions[1].Should().BeGreaterThan(lockPosition);
    }

    [Fact]
    public void Conferencia_Deve_Ser_Atomica_E_Integrada_A_Central()
    {
        var repository = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs"));
        var detail = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Recebimentos/Detalhe.cshtml"));

        repository.Should().Contain("public async Task<RecebimentoCriado> ConcluirInspecaoAsync")
            .And.Contain("for update")
            .And.Contain("INSPECAO_RECEBIMENTO_COMPRA")
            .And.Contain("pendencia_operacional")
            .And.Contain("status='RESOLVIDA'");
        controller.Should().Contain("compras_empresariais.recebimentos.inspecionar")
            .And.Contain("ExportarRecebimentos");
        detail.Should().Contain("Para que serve")
            .And.Contain("Como funciona")
            .And.Contain("Regras importantes")
            .And.Contain("Próximo passo")
            .And.Contain("buscaResponsavel")
            .And.Contain("JustificativaInformada")
            .And.Contain("TemProximaPaginaResponsavel");
    }

    [Fact]
    public void Responsaveis_Deve_Paginar_45_Sem_Salto_E_Buscar_Nome_Ou_Login()
    {
        const int total = 45, tamanho = 20;
        var ids = Enumerable.Range(1, total).ToArray();
        var paginas = Enumerable.Range(1, 3)
            .SelectMany(pagina => ids.Skip((pagina - 1) * tamanho).Take(tamanho))
            .ToArray();

        paginas.Should().Equal(ids);
        paginas.Distinct().Should().HaveCount(total);
        ids.Skip(0).Take(tamanho).Should().HaveCount(20);
        ids.Skip(20).Take(tamanho).Should().HaveCount(20);
        ids.Skip(40).Take(tamanho).Should().HaveCount(5);

        var service = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Governanca/TransversalGovernancaService.cs"));
        service.Should().Contain("Offset = Offset(pagina, tamanhoLogico)")
            .And.Contain("Limit = tamanhoLogico + 1")
            .And.Contain("p.nome_social ilike @Term or p.nome ilike @Term or u.login ilike @Term")
            .And.Contain("order by coalesce(p.nome_social,p.nome,u.login),u.id");
    }

    [Fact]
    public void Fila_Operacional_Deve_Expor_Visoes_Totais_E_Revisao_De_Conflito()
    {
        var contracts = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Governanca/TransversalContracts.cs"));
        var service = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Governanca/TransversalGovernancaService.cs"));
        var detail = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/GovernancaTransversal/Detalhe.cshtml"));

        contracts.Should().Contain("PendenciasPaginaDto").And.Contain("TotalFiltrado").And.Contain("TemProximaPagina");
        service.Should().Contain("'MINHAS'").And.Contain("'NAO_ATRIBUIDAS'").And.Contain("'ENCERRADAS'")
            .And.Contain("prazo is not null and prazo<now()")
            .And.Contain("status in ('RESOLVIDA','CANCELADA')");
        detail.Should().Contain("Revisão obrigatória").And.Contain("confirmarRevisao")
            .And.Contain("sessionStorage").And.Contain("ResponsavelInformadoElegivel");
    }

    [Fact]
    public void Devolucao_Elegibilidade_Unificada_E_Selecao_Explicita()
    {
        var repo = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/DevolucaoCompraRepository.cs"));
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs"));
        var viewNova = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Devolucoes/Nova.cshtml"));
        var viewRecebimento = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Recebimentos/Detalhe.cshtml"));

        repo.Should().Contain("resultado_inspecao in('RECUSADO','REPROVADO','REPROVADO_PARCIAL','ACEITO_COM_RESSALVA')")
            .And.Contain("not exists(select 1 from sigov.compras_empresarial_recebimento_item rx where rx.tenant_id=r.tenant_id and rx.recebimento_id=r.id and rx.quantidade_conferencia>0)");

        repo.Should().Contain("O recebimento não está elegível para devolução ou ainda possui conferência em andamento.");

        controller.Should().Contain("item.Selecionado")
            .And.Contain("Pelo menos um item deve ser selecionado")
            .And.Contain("A quantidade do item selecionado deve ser positiva")
            .And.Contain("Quantidade negativa é inválida");

        viewNova.Should().Contain("item-checkbox")
            .And.Contain("Devolver?")
            .And.Contain("ResponsavelId")
            .And.Contain("Contexto institucional autorizado");

        viewRecebimento.Should().Contain("Acompanhamento da destinação dos itens rejeitados")
            .And.Contain("Devoluções físicas vinculadas a este recebimento");
    }

    [Fact]
    public void Devolucao_Acompanhamento_Destinacao_Sem_Sobreposicao_E_Encerramento_Comprovado()
    {
        var repoDev = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/DevolucaoCompraRepository.cs"));
        var repoDiv = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/DivergenciaRecebimentoRepository.cs"));
        var viewDiv = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Divergencias/Detalhe.cshtml"));

        repoDev.Should().Contain("QuantidadeReservada")
            .And.Contain("QuantidadeExpedidaNaoEntregue")
            .And.Contain("QuantidadeEntregue")
            .And.Contain("SaldoSemDestinacao")
            .And.NotContain("greatest(0,");

        repoDev.Should().Contain("Inconsistência detectada");
        repoDiv.Should().Contain("Inconsistência de saldo");

        repoDiv.Should().Contain("Não é possível encerrar o tratamento como devolução sem que haja devoluções físicas com entrega comprovada ao fornecedor.")
            .And.Contain("Não é possível declarar devolução integral quando resta quantidade sem entrega comprovada ao fornecedor.");

        viewDiv.Should().Contain("Acompanhamento da destinação do item")
            .And.Contain("Devoluções físicas vinculadas a este item")
            .And.Contain("DEVOLUCAO_AO_FORNECEDOR")
            .And.Contain("DEVOLUCAO_INTEGRAL");
    }

    [Fact]
    public void Devolucao_Edicao_Rascunho_Auditoria_E_Datas_Operacionais()
    {
        var appService = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/ComprasEmpresariais/ComprasApplicationServices.cs"));
        var repo = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/DevolucaoCompraRepository.cs"));
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs"));
        var viewEditar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Devolucoes/Editar.cshtml"));
        var viewDetalhe = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Devolucoes/Detalhe.cshtml"));

        repo.Should().Contain("Require(h, r.Version, \"RASCUNHO\");")
            .And.Contain("A ação exige devolução em")
            .And.Contain("RASCUNHO_EDITADO")
            .And.Contain("antes = new { motivo = h.Motivo")
            .And.Contain("depois = new { motivo = r.Motivo");

        controller.Should().Contain("EditarDevolucao")
            .And.Contain("SalvarEdicaoDevolucao");

        viewEditar.Should().Contain("Editar devolução")
            .And.Contain("Salvar alterações do rascunho");

        viewDetalhe.Should().Contain("Editar rascunho")
            .And.Contain("Histórico legível da operação")
            .And.Contain("Responsável/Autor:");

        appService.Should().Contain("A confirmação de saída física não pode registrar data futura.")
            .And.Contain("A confirmação de entrega física não pode registrar data futura.");

        repo.Should().Contain("A entrega não pode ser anterior à expedição.")
            .And.Contain("\"EXPEDIDA\", \"RASCUNHO\", \"EXPEDIDA\"")
            .And.NotContain("sigov.estoque_saldo");
    }

    [Fact]
    public void Devolucao_Central_Operacional_E_Exportacao_Completa()
    {
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs"));
        var viewIndex = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Devolucoes/Index.cshtml"));

        controller.Should().Contain("ExportarDevolucoes")
            .And.Contain("Recebimento;Fornecedor;Produto;Unidade;QuantidadeRejeitada;Reservada;EmTransporte;Entregue;SaldoElegivel;Situacao;Responsavel;OrigemFisica;Destino;CriadaEm;ExpedidaEm;EntregueEm;ProtocoloEntrega")
            .And.Contain("count>50000");

        viewIndex.Should().Contain("Central de devoluções físicas")
            .And.Contain("Recebimentos com itens rejeitados elegíveis para devolução")
            .And.Contain("responsavelId")
            .And.Contain("Exportar relatório completo (CSV)");
    }
}
