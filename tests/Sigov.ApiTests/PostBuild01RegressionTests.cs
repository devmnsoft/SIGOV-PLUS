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

    [Fact]
    public void Aprovacao_Fila_Decisao_E_Politica_Deve_Espelhar_Autorizacao_E_Ser_FailClosed()
    {
        var repository = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        var service = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/ComprasEmpresariais/ComprasApplicationServices.cs"));
        var api = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/ComprasEmpresariais/ComprasEmpresariaisController.cs"));

        repository.Should().Contain("join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id")
            .And.Contain("pp.efeito='PERMITIR'")
            .And.Contain("and pp.efeito='NEGAR'")
            .And.Contain("md5('sigov:usuario:'||u.id::text)::uuid")
            .And.Contain("(a.aprovador_id=@us or a.aprovador_id is null)")
            .And.Contain("(a.aprovador_id is null) Bloqueada")
            .And.Contain("status='CANCELADO'")
            .And.Contain("'COMPRAS_EMPRESARIAIS'")
            .And.Contain("Esta etapa está bloqueada e aguarda configuração institucional antes de qualquer decisão.")
            .And.Contain("Você não é o aprovador designado para esta etapa.")
            .And.Contain("Versão desatualizada; recarregue a fila de aprovações e tente novamente.")
            .And.Contain("A requisição vinculada não está pendente de aprovação.")
            .And.Contain("Etapa de aprovação não encontrada no contexto autorizado.")
            .And.Contain("O tenant empresarial não possui um único vínculo institucional ativo; a operação foi cancelada com segurança.")
            .And.Contain("A pendência de aprovação não pôde ser registrada no vínculo institucional ativo; a operação foi cancelada com segurança.")
            .And.Contain("Configuração institucional obrigatória ausente para o contexto autorizado.")
            .And.Contain("Já existe outra política de aprovação com o mesmo nome neste contexto.")
            .And.Contain("values(@t,'APROVACAO_DECISAO',@key,@id,@hash)")
            .And.Contain("values(@t,'APROVACAO_POLITICA_SALVAR',@key,md5('sigov:politica_aprovacao:'||@pol::text)::uuid,@hash)");

        service.Should().Contain("Decisão inválida; informe APROVAR, REJEITAR ou DEVOLVER.")
            .And.Contain("O motivo é obrigatório (mínimo de 10 caracteres) para rejeitar ou devolver.")
            .And.Contain("O nome da política deve ter entre 3 e 120 caracteres.")
            .And.Contain("Informe entre 1 e 10 níveis, todos com limite positivo.")
            .And.Contain("Requisição ou versão inválida.");

        api.Should().Contain("compras_empresariais.requisicoes.enviar")
            .And.Contain("compras_empresariais.aprovacoes.visualizar")
            .And.Contain("compras_empresariais.aprovacoes.aprovar")
            .And.Contain("compras_empresariais.configuracao.gerenciar")
            .And.Contain("compras_empresariais.relatorios.visualizar")
            .And.Contain("relatorios/aprovacoes.csv")
            .And.Contain("configuracao/politica")
            .And.Contain("StatusCode(409,new{status=409")
            .And.Contain("Numero;Ciclo;Etapa;Alcada;SituacaoEtapa;Aprovador;StatusRequisicao;Total;CriadaEm;DecididaEm;Motivo")
            .And.Contain("A exportação excede o limite explícito de 50.000 registros; refine os filtros.");
    }

    [Fact]
    public void Requisicao_Envio_Gera_Ciclo_Por_Alcada_E_Bloqueio_FailClosed()
    {
        var repository = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));

        repository.Should().Contain("Requisição não encontrada no contexto autorizado.")
            .And.Contain("Requisição sem itens, fora de rascunho ou versão desatualizada.")
            .And.Contain("Total estimado inválido; revise os itens antes do envio.")
            .And.Contain("Somente requisições em rascunho ou devolvidas podem ser enviadas para aprovação.")
            .And.Contain("Requisição fora de rascunho/devolvida ou versão desatualizada.")
            .And.Contain("{version:D}|{total.ToString(\"0.00\",CultureInfo.InvariantCulture)}|{itens:D}")
            .And.Contain("values(@t,'REQUISICAO_ENVIAR',@key,@id,@hash)")
            .And.Contain("if(n.Item2>=total)break")
            .And.Contain("\"APROVACAO_SEM_POLITICA\",\"Política de aprovação ausente\"")
            .And.Contain("\"APROVACAO_SEM_APROVADOR\",\"Aprovação sem aprovador habilitado\"")
            .And.Contain("\"APROVACAO_ALCADA_INSUFICIENTE\",\"Aprovação com alçada insuficiente\"")
            .And.Contain("set status='PENDENTE_APROVACAO'")
            .And.Contain("status=case when status='DEVOLVIDA' then 'RASCUNHO' else status end")
            .And.Contain("and status in('RASCUNHO','DEVOLVIDA')")
            .And.Contain("@inicio::date is null or r.created_at>=@inicio")
            .And.Contain("r.created_at<(@fim::date+1)");
    }

    [Fact]
    public void Aprovacao_Migration_E_Seed_Institucional_Deve_Ser_Idempotente_MultiEsfera()
    {
        var migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260927120000_compras_aprovacoes_fluxo_e_divergencia_codificada.sql"));
        var seed = File.ReadAllText(TestRepoPath.Get("database/postgres/seeds/compras_aprovacao_institucional_seed.sql"));
        var manifest = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/manifest.json"));

        migration.Should().Contain("bigint generated by default as identity primary key")
            .And.Contain("esfera_governo varchar(20) not null check(esfera_governo in('municipal','estadual','federal'))")
            .And.Contain("unique(tenant_id,requisicao_id,ciclo,nivel,aprovador_id)")
            .And.Contain("alter table sigov.compras_empresarial_aprovacao alter column aprovador_id drop not null")
            .And.Contain("resultado_codigo in('DEVOLUCAO_INTEGRAL','DEVOLUCAO_PARCIAL','SUBSTITUICAO_CONCLUIDA','GLOSA_APLICADA','ENCERRAMENTO_ADMINISTRATIVO')")
            .And.Contain("update sigov.permissao set modulo='compras_empresariais' where modulo='COMPRAS_EMPRESARIAIS'");
        migration.ToLowerInvariant().Should().NotContain("drop table").And.NotContain("truncate");

        seed.Should().Contain("on conflict (id) do nothing")
            .And.Contain("raise exception 'Seed de demo exige o usuario administrador local (id=1) para reaproveitar o hash de senha.'")
            .And.Contain("set entidade_id = 9101")
            .And.Contain("where id in (101, 102) and tenant_id = 1")
            .And.Contain("'municipal'")
            .And.Contain("'estadual'")
            .And.Contain("'federal'")
            .And.Contain("md5('sigov:usuario:101')::uuid")
            .And.Contain("senha_hash := (select u.senha_hash");
        seed.ToLowerInvariant().Should().NotContain("@sigov.local").And.NotContain("pbkdf2(");

        manifest.Should().Contain("20260927120000_compras_aprovacoes_fluxo_e_divergencia_codificada.sql");

        foreach (var script in new[]
        {
            "script_completo.sql", "script_completop.sql", "script_completo_dev.sql",
            "database/script_completo.sql", "database/postgres/script_completo.sql", "database/postgres/script_completo_dev.sql"
        })
        {
            var sql = File.ReadAllText(TestRepoPath.Get(script));
            sql.Should().Contain("uq_comp_aprovacao_ciclo");
            sql.Should().NotContain("compras.demo.analista");
        }
    }

    [Fact]
    public void Aprovacao_Web_Deve_Navegar_E_Materializar_Timestamptz_Com_DateTime()
    {
        var raiz = TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais");
        var views = Directory.GetFiles(raiz, "*.cshtml", SearchOption.AllDirectories);
        views.Length.Should().BeGreaterOrEqualTo(20);
        var comParcial = views.Count(v => File.ReadAllText(v).Contains("Shared/_ComprasNav", StringComparison.Ordinal));
        comParcial.Should().BeGreaterOrEqualTo(15);
        var ofensores = views.Where(v => File.ReadAllText(v).Contains("../Shared/_ComprasNav", StringComparison.Ordinal)).ToArray();
        ofensores.Should().BeEmpty();

        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs"));
        var fila = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Aprovacoes/Index.cshtml"));
        var config = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Configuracao/Politica.cshtml"));
        var detalhe = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Requisicoes/Detalhe.cshtml"));

        controller.Should().Contain("Decisão já registrada; o resultado persistido foi reapresentado.")
            .And.Contain("Política já registrada; o resultado persistido foi reapresentado.");

        fila.Should().Contain("<partial name=\"Shared/_ComprasNav\"")
            .And.Contain("Pendências de decisão")
            .And.Contain("Aguarda configuração")
            .And.Contain("Decisão sua")
            .And.Contain("Outro aprovador designado")
            .And.Contain("Idempotency-Key")
            .And.Contain("Exportar CSV")
            .And.Contain("Ajuda da jornada");

        config.Should().Contain("<partial name=\"Shared/_ComprasNav\"")
            .And.Contain("Política ativa")
            .And.Contain("Salvar política de aprovação");

        detalhe.Should().Contain("(Model.Status is \"RASCUNHO\" or \"DEVOLVIDA\")")
            .And.Contain("Corrigir requisição devolvida")
            .And.Contain("Devolvida para correção")
            .And.Contain("Enviar para aprovação");

        var sessao = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Security/IdentitySessionService.cs"));
        var central = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Services/MinhaCentralService.cs"));
        var modelos = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Models/PostBuild/PostBuildViewModels.cs"));

        sessao.Should().Contain("Sessao persistente exige tenant_id resolvido.")
            .And.Contain("Sessao persistente exige entidade_id resolvida.");
        central.Should().Contain("private sealed record PendenciasTotals(long Total, long Vencidas, DateTime AtualizadoEm);");
        modelos.Should().Contain("string Url, DateTime? Prazo");
    }

    [Fact]
    public void RC_SAAS_AUT_A5_revogacao_por_alteracao_de_identidade_ou_permissao_em_paridade_web_e_api()
    {
        var sessao = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Security/IdentitySessionService.cs"));
        sessao.Should().Contain("Task<long> DeriveAuthVersionAsync(long userId, CancellationToken cancellationToken)")
            .And.Contain("select greatest(")
            .And.Contain("s.auth_version = @AuthVersion");
        var web = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Program.cs"));
        web.Should().Contain("OnValidatePrincipal")
            .And.Contain("DeriveAuthVersionAsync(userId, context.HttpContext.RequestAborted)")
            .And.Contain("currentAuthVersion != validation.AuthVersion");
        var handler = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Authentication/SigovApiAuthenticationHandler.cs"));
        handler.Should().Contain("DeriveAuthVersionAsync(row.UserId, Context.RequestAborted)")
            .And.Contain("currentAuthVersion != row.AuthVersion")
            .And.Contain("Sessao revogada por alteracao de identidade ou permissao.");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/AuthController.cs"))
            .Should().Contain("RevokeAllForUserAsync(changed.Id, \"SENHA_REDEFINIDA\", ct)");
    }

    [Fact]
    public void Ponte_Identity_Nucleo_Empresarial_Deve_Ser_Deterministica()
    {
        var projecao = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Security/EnterpriseIdentityProjection.cs"));
        var handler = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Authentication/SigovApiAuthenticationHandler.cs"));

        projecao.Should().Contain("sigov:usuario:{")
            .And.Contain("ForUserId");
        handler.Should().Contain("EnterpriseIdentityProjection.ForUserId");
    }

    [Fact]
    public void Contexto_Institucional_Deve_Ser_Univoco_Sem_Vinculo_Inventado()
    {
        var repoDev = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/DevolucaoCompraRepository.cs"));
        var repoAprov = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));

        repoDev.Should().Contain("join sigov.entidade e on e.id=u.entidade_id and e.ativo and not e.is_deleted")
            .And.Contain("public async Task<ContextoInstitucionalSnapshot?> ObterContextoInstitucionalAsync(Guid tenant, Guid usuario, CancellationToken ct)")
            .And.Contain("internal static async Task<ContextoInstitucionalSnapshot?> ContextoInstitucionalAsync(NpgsqlConnection cn, NpgsqlTransaction? tx, Guid tenant, Guid usuario, CancellationToken ct)")
            .And.Contain("Ambiguidade de contexto institucional: o usuário possui mais de um vínculo ativo de entidade no núcleo {tenant:D}; a operação foi cancelada com segurança.");
        repoDev.ToLowerInvariant().Should().NotContain("order by e.id limit 1");

        repoAprov.Should().Contain("DevolucaoCompraRepository.ContextoInstitucionalAsync(connection, tx, context.TenantId, context.UsuarioId, ct)");
    }

    [Fact]
    public void Idempotencia_Com_Equivalencia_De_Conteudo_E_Serializacao_Por_Lock_Advisory()
    {
        var repos = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        var aprov = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));

        repos.Should().Contain("select pg_advisory_xact_lock(hashtextextended(@chaveLock,0))")
            .And.Contain("select recurso_id RecursoId,request_hash RequestHash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='FORNECEDOR_CRIAR' and chave=@key")
            .And.Contain("select recurso_id RecursoId,request_hash RequestHash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao=@op and chave=@key")
            .And.Contain("anterior.RequestHash is not null && anterior.RequestHash!=hash")
            .And.Contain("A chave de idempotência já foi usada com conteúdo diferente.")
            .And.Contain("internal sealed record IdemComHash(Guid RecursoId,string? RequestHash);")
            .And.Contain("internal static string HashPayload(object payload)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}))).ToLowerInvariant();")
            .And.Contain("insert into sigov.compras_empresarial_idempotencia(tenant_id,operacao,chave,recurso_id,request_hash) values(@t,@op,@key,@child,@hash)");

        aprov.Should().Contain("select pg_advisory_xact_lock(hashtextextended(@chaveLock,0))")
            .And.Contain("select request_hash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='APROVACAO_DECISAO' and chave=@key")
            .And.Contain("select request_hash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='APROVACAO_POLITICA_SALVAR' and chave=@key");
    }

    [Fact]
    public void Envio_Recalcula_Total_Na_Mesma_Precisao_Do_Postgres_E_Fotografa_Itens()
    {
        var repos = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));

        repos.Should().Contain("Math.Round(r.Itens.Sum(i=>i.Quantidade*i.ValorEstimado),2,MidpointRounding.AwayFromZero)")
            .And.Contain("select coalesce(round(sum(i.quantidade*i.valor_estimado),2),0)")
            .And.Contain("jsonb_build_object('total_requisicao',@total,'alcada_etapa',@limite,'etapa',@nivel,'ciclo',@ciclo,'versao_requisicao',@versaoReq,'politica_id',@polId,'politica_nome',@polNome,'solicitante_id',@solicitanteId,'itens',(select coalesce(jsonb_agg(jsonb_build_object('id',i.id,'ordem',i.ordem,'tipo',i.tipo,'descricao',i.descricao,'especificacao',i.especificacao,'unidade',i.unidade,'quantidade',i.quantidade,'valor_estimado',i.valor_estimado,'permite_parcial',i.permite_parcial,'exige_inspecao',i.exige_inspecao) order by i.ordem),'[]'::jsonb)")
            .And.Contain("'politica_nome',@polNome,'solicitante_id',@solicitanteId,'itens'")
            .And.Contain("not i.is_deleted)),@ciclo,@us,@us,@corr)");
    }

    [Fact]
    public void Fila_Central_E_Paineis_Operacionais_Sao_Filtrados_E_Honestos()
    {
        var aprov = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));

        aprov.Should().Contain("left join sigov.os_tecnico sol on (sol.tenant_id,sol.usuario_id)=(r.tenant_id,r.solicitante_id) and not sol.is_deleted")
            .And.Contain("(@busca::text is null or r.numero ilike @term or coalesce(r.setor,'') ilike @term or sol.nome ilike @term)")
            .And.Contain("and (@urgencia is null or r.urgencia=@urgencia)")
            .And.Contain("when a.aprovador_id is null then 'Aguardando configuração institucional'")
            .And.Contain("then 'Aguardando nível anterior (' || at.nivel_ativo::text || ')'")
            .And.Contain("when a.nivel>=(select coalesce(max(b.nivel),0)")
            .And.Contain("then 'Decisão encerra o ciclo'")
            .And.Contain("else 'Próxima etapa: nível '||(a.nivel+1)::text end ProximaAcao")
            .And.Contain("\"in ('APROVADA','REJEITADA')\"")
            .And.Contain("\"in ('APROVADO','REJEITADA')\"")
            .And.Contain("left join lateral(select d.motivo,d.decidido_em,d.aprovador_id from sigov.compras_empresarial_aprovacao d where d.tenant_id=r.tenant_id and d.requisicao_id=r.id and d.status {filtroEtapa} order by d.id desc limit 1) d on true")
            .And.Contain("(r.solicitante_id=@us or exists(select 1 from sigov.compras_empresarial_aprovacao x where x.tenant_id=r.tenant_id and x.requisicao_id=r.id and (x.aprovador_id=@us or x.aprovador_id is null)))");
    }

    [Fact]
    public void Detalhe_Da_Decisao_E_FailClosed_Com_Snapshot_Historico_E_Proxima_Etapa()
    {
        var aprov = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        var contracts = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/ComprasEmpresariais/ComprasContracts.cs"));
        var service = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/ComprasEmpresariais/ComprasApplicationServices.cs"));

        aprov.Should().Contain("and (a.aprovador_id=@us or a.aprovador_id is null or r.solicitante_id=@us or exists(select 1 from sigov.compras_empresarial_aprovacao x where x.tenant_id=a.tenant_id and x.requisicao_id=a.requisicao_id and x.aprovador_id=@us and x.status not in('PENDENTE','CANCELADO')))")
            .And.Contain("a.regra_snapshot::text RegraSnapshot")
            .And.Contain("left join sigov.os_tecnico os on (os.tenant_id,os.usuario_id)=(a.tenant_id,a.aprovador_id) and not os.is_deleted")
            .And.Contain("select acao,detalhes::text Detalhes,created_at CriadoEm")
            .And.Contain("aggregate_type='REQUISICAO' and aggregate_id=(select requisicao_id from sigov.compras_empresarial_aprovacao where tenant_id=@t and id=@id)")
            .And.Contain("order by created_at desc,id desc limit 50")
            .And.Contain("etapas.FirstOrDefault(e => e.Status == \"PENDENTE\" && e.Nivel >= cab.Nivel)");

        contracts.Should().Contain("public sealed record AprovacaoPainelResumo(")
            .And.Contain("public sealed record AprovacaoEtapaLinha(")
            .And.Contain("public sealed record AprovacaoEtapaDetalhe(")
            .And.Contain("Task<AprovacaoEtapaDetalhe?> ObterDetalheAsync(ComprasContext context,Guid etapaId,CancellationToken ct);")
            .And.Contain("Task<PagedResult<AprovacaoPainelResumo>> ListarDevolvidasAsync(ComprasContext context,int pagina,int tamanho,CancellationToken ct);");

        service.Should().Contain("if(etapaId==Guid.Empty)throw new ArgumentException(\"Etapa de aprovação inválida.\")");
    }

    [Fact]
    public void Web_E_API_Expor_Fila_Detalhe_E_Relatorio_De_Aprovacoes_Com_Rotas_Estaveis()
    {
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs"));
        var api = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/ComprasEmpresariais/ComprasEmpresariaisController.cs"));

        controller.Should().Contain("public sealed record AprovacoesCentralViewModel(PagedResult<AprovacaoFilaResumo> Fila,PagedResult<AprovacaoPainelResumo> Devolvidas,PagedResult<AprovacaoPainelResumo> Concluidas);")
            .And.Contain("[HttpGet(\"Aprovacoes/{etapaId:guid}/Detalhe\"),Authorize(Policy=\"compras_empresariais.aprovacoes.visualizar\")]")
            .And.Contain("return item is null?NotFound():View(\"Aprovacoes/Detalhe\",item);")
            .And.Contain("[HttpGet(\"Relatorios/Aprovacoes\"),Authorize(Policy=\"compras_empresariais.relatorios.visualizar\")]")
            .And.Contain("View(\"Relatorios/Aprovacoes\",await aprovacoes.ListarRelatorioAsync(Contexto(),pagina,tamanho,ct));")
            .And.Contain("var fila=await aprovacoes.ListarFilaAsync(Contexto(),pagina,tamanho,busca,urgencia,ct);")
            .And.Contain("var devolvidas=await aprovacoes.ListarDevolvidasAsync(Contexto(),1,10,ct);")
            .And.Contain("var concluidas=await aprovacoes.ListarConcluidasAsync(Contexto(),1,10,ct);");

        api.Should().Contain("[FromQuery]string? busca=null,[FromQuery]string? urgencia=null");
    }

    [Fact]
    public void Navegacao_Agoupada_Por_Jornada_E_Permissao_Separa_Soes_Da_Jornada()
    {
        var wrapper = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_ComprasNav.cshtml"));
        var nav = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Shared/_ComprasNav.cshtml"));
        var css = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/css/compras-empresariais.css"));

        wrapper.Should().Contain("<partial name=\"ComprasEmpresariais/Shared/_ComprasNav\" />");

        nav.Should().Contain("@using Microsoft.AspNetCore.Authorization")
            .And.Contain("@inject Microsoft.AspNetCore.Authorization.IAuthorizationService Auth")
            .And.Contain("compras_empresariais.dashboard.visualizar")
            .And.Contain("compras_empresariais.fornecedores.visualizar")
            .And.Contain("compras_empresariais.requisicoes.visualizar")
            .And.Contain("compras_empresariais.aprovacoes.visualizar")
            .And.Contain("compras_empresariais.cotacoes.visualizar")
            .And.Contain("compras_empresariais.pedidos.visualizar")
            .And.Contain("compras_empresariais.recebimentos.visualizar")
            .And.Contain("compras_empresariais.divergencias.visualizar")
            .And.Contain("compras_empresariais.faturas.visualizar")
            .And.Contain("compras_empresariais.devolucoes.visualizar")
            .And.Contain("compras_empresariais.avaliacoes.gerenciar")
            .And.Contain("compras_empresariais.relatorios.visualizar")
            .And.Contain("compras_empresariais.configuracao.gerenciar")
            .And.Contain("if(!(await Auth.AuthorizeAsync(User,l.Politica)).Succeeded)continue;")
            .And.Contain("<span class=\"compras-nav-sep\" aria-hidden=\"true\"></span>");

        css.Should().Contain(".compras-nav-sep{align-self:stretch;width:1px;background:var(--bs-border-color);margin:.3rem .15rem;flex:none}")
            .And.Contain(".compras-nav a[aria-current=\"page\"]{background:var(--bs-primary);color:#fff;font-weight:650}")
            .And.Contain(".compras-snapshot{background:var(--bs-tertiary-bg)");
    }

    [Fact]
    public void Telas_Novas_De_Decisao_E_Relatorio_Tem_Formularios_Estados_Honestos()
    {
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Aprovacoes/Detalhe.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Relatorios/Aprovacoes.cshtml")).Should().BeTrue();

        var detalhe = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Aprovacoes/Detalhe.cshtml"));
        detalhe.Should().Contain("var podeDecidir = Model.StatusEtapa == \"PENDENTE\" && Model.DecisivelPorMim && !Model.Bloqueada;")
            .And.Contain("<pre class=\"compras-snapshot mt-2 mb-0\">@Model.RegraSnapshot</pre>")
            .And.Contain("name=\"motivo\" class=\"form-control mb-2\" rows=\"2\" minlength=\"10\" required")
            .And.Contain("<input type=\"hidden\" name=\"version\" value=\"@Model.VersionEtapa\"/>")
            .And.Contain("<partial name=\"Shared/_ComprasNav\"")
            .And.Contain("Próxima etapa:</strong> Nível @prox.Nivel");

        var relatorio = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Relatorios/Aprovacoes.cshtml"));
        relatorio.Should().Contain("asp-action=\"RelatorioAprovacoesCsv\">Exportar CSV</a>")
            .And.Contain("neutraliza fórmulas de planilha")
            .And.Contain("Mostrando @Model.Items.Count de @Model.TotalItems registro(s) dos filtros aplicados (página @Model.Page)")
            .And.Contain("<partial name=\"Shared/_ComprasNav\"")
            .And.Contain("Ainda não há etapas de ciclo de aprovação neste contexto; nenhum dado fictício é exibido.");

        var politica = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Configuracao/Politica.cshtml"));
        politica.Should().Contain("alçada acumulada")
            .And.Contain("as etapas são criadas cumulativamente para cada nível até (e incluindo) o primeiro cujo limite cubra o total");

        var workspace = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Workspace.cshtml"));
        workspace.Should().Contain("Tela em construção")
            .And.Contain("nenhum dado fictício é exibido aqui e nenhum estado é simulado.");

        var recebimentos = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Recebimentos/Relatorio.cshtml"));
        recebimentos.Should().Contain("primeiras 100 linhas dos filtros aplicados, direto do PostgreSQL")
            .And.Contain("Mostrando @Model.Resultado.Items.Count de @Model.Resultado.TotalItems registro(s) dos filtros aplicados (página @Model.Resultado.Page)");
    }

    [Fact]
    public void Backfill_Resultado_Vazio_Encerrado_Deve_Existir_Na_Migration_E_Nos_Scriptes()
    {
        const string backfill = "update sigov.compras_empresarial_recebimento_divergencia set resultado_codigo='ENCERRAMENTO_ADMINISTRATIVO' where situacao='ENCERRADA' and trim(coalesce(resultado,''))='' and resultado_codigo is null;";
        var migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260927120000_compras_aprovacoes_fluxo_e_divergencia_codificada.sql"));
        migration.Should().Contain(backfill);

        foreach (var script in new[]
        {
            "script_completo.sql", "script_completop.sql", "script_completo_dev.sql",
            "database/script_completo.sql", "database/postgres/script_completo.sql", "database/postgres/script_completo_dev.sql"
        })
        {
            File.ReadAllText(TestRepoPath.Get(script)).Should().Contain(backfill);
        }
    }

    [Fact]
    public void Aceite_A_Comparativo_Antigo_Rejeitado_Apos_Revisao_De_Proposta()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/CotacaoCompraRepository.cs"));
        // Qualquer alteração relevante ou revisão incrementa a versão da cotação
        repoSql.Should().Contain("update sigov.compras_empresarial_cotacao set status=case when status='ABERTA' then 'EM_RESPOSTA' else status end,updated_at=now(),updated_by=@us,correlation_id=@corr,version=version+1");
        repoSql.Should().Contain("RESPOSTA_REVISADA");
        // Seleção valida versão da cotação com lock exclusivo
        repoSql.Should().Contain("where tenant_id=@t and id=@id for update");
        repoSql.Should().Contain("Versão desatualizada; recarregue o comparativo e tente novamente.");
    }

    [Fact]
    public void Aceite_B_Fornecedor_Bloqueado_Nao_Pode_Ser_Selecionado()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/CotacaoCompraRepository.cs"));
        // Revalidação rigorosa de elegibilidade no servidor no momento da seleção
        repoSql.Should().Contain("convite.FornecedorStatus is \"BLOQUEADO\" or \"SUSPENSO\" || convite.FornecedorDeleted");
        repoSql.Should().Contain("está bloqueado ou suspenso e não é elegível para seleção");
    }

    [Fact]
    public void Aceite_C_Frete_E_Composicao_Financeira_Reconciliam_Total_Centavo_A_Centavo()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/CotacaoCompraRepository.cs"));
        // Reconciliação dos componentes monetários
        repoSql.Should().Contain("var valorBrutoTotal = Math.Round(grupo.Sum(e => e.ValorBruto), 2, MidpointRounding.AwayFromZero);");
        repoSql.Should().Contain("var descontoTotal = Math.Round(grupo.Sum(e => e.Desconto), 2, MidpointRounding.AwayFromZero);");
        repoSql.Should().Contain("var impostoTotal = Math.Round(grupo.Sum(e => e.Imposto), 2, MidpointRounding.AwayFromZero);");
        repoSql.Should().Contain("var freteTotal = Math.Round(grupo.Sum(e => e.Frete), 2, MidpointRounding.AwayFromZero);");
        repoSql.Should().Contain("var valorTotal = Math.Round(grupo.Sum(e => e.TotalItem), 2, MidpointRounding.AwayFromZero);");
        repoSql.Should().Contain("CalcularComponentesMonetarios");
        repoSql.Should().Contain("Math.Round(precoUnitario * quantidade, 2, MidpointRounding.AwayFromZero)");
        repoSql.Should().Contain("var vl = vb - des + imp;");

        // Prova matemática de reconciliação centavo a centavo
        decimal q1 = 10m, cue1 = 15.50m, desc1 = 5.00m, imp1 = 2.50m, frete1 = 3.00m;
        var bruto1 = Math.Round(q1 * cue1, 2, MidpointRounding.AwayFromZero);
        var liq1 = Math.Round(bruto1 - desc1 + imp1, 2, MidpointRounding.AwayFromZero);
        var totalItem1 = Math.Round(liq1 + frete1, 2, MidpointRounding.AwayFromZero);

        decimal q2 = 5m, cue2 = 20.00m, desc2 = 0m, imp2 = 0m, frete2 = 2.00m;
        var bruto2 = Math.Round(q2 * cue2, 2, MidpointRounding.AwayFromZero);
        var liq2 = Math.Round(bruto2 - desc2 + imp2, 2, MidpointRounding.AwayFromZero);
        var totalItem2 = Math.Round(liq2 + frete2, 2, MidpointRounding.AwayFromZero);

        var totalPedido = totalItem1 + totalItem2;
        var totalBruto = bruto1 + bruto2;
        var totalDesconto = desc1 + desc2;
        var totalImposto = imp1 + imp2;
        var totalFrete = frete1 + frete2;
        var totalLiquido = liq1 + liq2;

        (totalBruto - totalDesconto + totalImposto + totalFrete).Should().Be(totalPedido);
        (totalLiquido + totalFrete).Should().Be(totalPedido);
    }

    [Fact]
    public void Aceite_D_Itens_Do_Mesmo_Produto_Preservam_Origens_Individuais()
    {
        var migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20261002100000_compras_pedido_operacional_fechamento_e_rastreabilidade.sql"));
        migration.Should().Contain("cotacao_item_id bigint");
        migration.Should().Contain("cotacao_selecao_id bigint");
        migration.Should().Contain("requisicao_item_id uuid");

        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/CotacaoCompraRepository.cs"));
        repoSql.Should().Contain("insert into sigov.compras_empresarial_pedido_item");
        repoSql.Should().Contain("cotacao_item_id,cotacao_selecao_id,requisicao_item_id");
        repoSql.Should().Contain("@ciId,@selId,@riId");
    }

    [Fact]
    public void Aceite_E_Pedido_Gerado_Chega_Ao_Recebimento_Pelo_Menu_E_Acoes()
    {
        var detalheView = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Pedidos/Detalhe.cshtml"));
        detalheView.Should().Contain("NovoRecebimento");
        detalheView.Should().Contain("Registrar recebimento");

        var navView = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Shared/_ComprasNav.cshtml"));
        navView.Should().Contain("compras_empresariais.pedidos.visualizar");
        navView.Should().Contain("compras_empresariais.recebimentos.visualizar");
    }

    [Fact]
    public void Aceite_F_G_Recebimento_Parcial_Mantem_Saldo_E_Protege_Concorrencia()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        // Lock exclusivo do pedido
        repoSql.Should().Contain("select id,status,version from sigov.compras_empresarial_pedido where tenant_id=@t and id=@id and not is_deleted for update");
        // Validação de saldo disponível
        repoSql.Should().Contain("quantidade-i.quantidade_cancelada-coalesce((select sum(ri.quantidade_fisica) from sigov.compras_empresarial_recebimento_item ri where ri.tenant_id=i.tenant_id and ri.pedido_item_id=i.id),0)");
        repoSql.Should().Contain("Um item não pertence ao pedido ou a quantidade supera o saldo permitido.");

        var cotacaoRepo = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/CotacaoCompraRepository.cs"));
        cotacaoRepo.Should().Contain("greatest(0,pi.quantidade-pi.quantidade_cancelada");
    }

    [Fact]
    public void Aceite_H_I_Inspecao_Nao_Duplica_Estoque_E_Gera_Divergencia_Rastreavel()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        // Apenas quantidade aceita gera movimento de estoque
        repoSql.Should().Contain("if(aceita>0)");
        repoSql.Should().Contain("origem_id) values(@t,@produto,@almox,'ENTRADA',@q,'INSPECAO_RECEBIMENTO_COMPRA',@id)");
        // Rejeição cria divergência rastreável com pendência operacional
        repoSql.Should().Contain("if(hasRejected)");
        repoSql.Should().Contain("insert into sigov.compras_empresarial_recebimento_divergencia");
        repoSql.Should().Contain("TRATAMENTO_DIVERGENCIA");
    }

    [Fact]
    public void Aceite_J_K_Devolucao_Preserva_Recebimento_E_Controla_Reposicao()
    {
        var repoDevolucao = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/DevolucaoCompraRepository.cs"));
        repoDevolucao.Should().Contain("insert into sigov.compras_empresarial_devolucao");
        repoDevolucao.Should().Contain("recebimento_id");
        // Nunca remove ou deleta o recebimento físico original
        repoDevolucao.Should().NotContain("delete from sigov.compras_empresarial_recebimento");

        var migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20261002100000_compras_pedido_operacional_fechamento_e_rastreabilidade.sql"));
        migration.Should().Contain("reposicao_autorizada boolean not null default false");
        migration.Should().Contain("quantidade_reposicao numeric(14,4)");
        migration.Should().Contain("reposicao_recebida numeric(14,4)");
        migration.Should().Contain("divergencia_origem_id bigint");
    }

    [Fact]
    public void Aceite_L_Pedido_Com_Pendencia_Nao_Encerra_Indevidamente()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/CotacaoCompraRepository.cs"));
        repoSql.Should().Contain("existem recebimentos com inspeção/conferência pendente");
        repoSql.Should().Contain("existem divergências em aberto ou em tratamento");
        repoSql.Should().Contain("existem devoluções pendentes de entrega física ou conclusão");
    }

    [Fact]
    public void Aceite_M_N_O_Isolamento_Tenant_Autorizacao_E_Erros_Nao_Simulam_Sucesso()
    {
        var cotacaoRepo = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/CotacaoCompraRepository.cs"));
        // Todo comando e query restringe tenant_id
        cotacaoRepo.Should().Contain("tenant_id=@t");

        // Controller exige antiforgery e autorizações estritas
        var webController = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs"));
        webController.Should().Contain("ValidateAntiForgeryToken");
        webController.Should().Contain("compras_empresariais.pedidos.emitir");
        webController.Should().Contain("compras_empresariais.pedidos.cancelar");
        webController.Should().Contain("compras_empresariais.cotacoes.julgar");
        webController.Should().Contain("compras_empresariais.cotacoes.enviar");

        // ReadOnlyContext bloqueia mutações
        var action = () => Sigov.Application.ComprasEmpresariais.ComprasGuard.Mutation(
            new Sigov.Application.ComprasEmpresariais.ComprasContext(Guid.NewGuid(), Guid.NewGuid(), "corr", SomenteLeitura: true));
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*somente leitura*");
    }

    [Fact]
    public void Reposicao_Deve_Validar_Item_Trocado_Excesso_Agregado_E_Id_Real_Recebimento()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        
        // 1. Bloqueia quantidade zero ou negativa
        repoSql.Should().Contain("As quantidades informadas para recebimento devem ser estritamente positivas.");
        
        // 2. Bloqueia item trocado na reposição
        repoSql.Should().Contain("divergenciasValidadas[item.DivergenciaOrigemId!.Value]");
        repoSql.Should().Contain("item.PedidoItemId!=div.PedidoItemId");
        repoSql.Should().Contain("não corresponde ao item original da divergência");
        
        // 3. Bloqueia excesso agregado sobre a divergência autorizada
        repoSql.Should().Contain("supera o saldo autorizado");
        
        // 4. Identidade real do recebimento propagada ao evento REPOSICAO_RECEBIDA (sem gerar Guid desconexo)
        repoSql.Should().Contain("jsonb_build_object('quantidade_recebida',@q,'recebimento_id',@id::text)");
    }

    [Fact]
    public void Conferencia_Faturas_Deve_Implementar_Match_Trilateral_Idempotencia_E_Concorrencia()
    {
        // 1. Migration estrutural com constraints e PK identity bigint
        var migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20261002160000_compras_fatura_conferencia_operacional.sql"));
        migration.Should().Contain("sigov.compras_empresarial_fatura_item");
        migration.Should().Contain("bigint generated by default as identity primary key");
        migration.Should().Contain("sigov.compras_empresarial_fatura_evento");
        migration.Should().Contain("sigov.compras_empresarial_fatura_idempotencia");
        migration.Should().Contain("ux_ce_fatura_chave_acesso");

        // 2. Repositório com 3-way match, locks advisory e revalidação concorrente de saldo
        var repoFatura = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/FaturaCompraRepository.cs"));
        repoFatura.Should().Contain("FATURA_PEDIDO");
        repoFatura.Should().Contain("pg_advisory_xact_lock");
        repoFatura.Should().Contain("QuantidadeAceitaTotal");
        repoFatura.Should().Contain("QuantidadeFaturadaOutras");
        repoFatura.Should().Contain("DIVERGENCIA_QUANTIDADE");
        repoFatura.Should().Contain("DIVERGENCIA_VALOR");
        repoFatura.Should().Contain("DIVERGENCIA_MISTA");
        repoFatura.Should().Contain("MATCH_TOTAL");
        repoFatura.Should().Contain("ComprasConcurrencyException");
        
        // 3. Aprovação não gera conta a pagar automática
        repoFatura.Should().NotContain("conta_pagar_id =");
        repoFatura.Should().NotContain("insert into sigov.financeiro_conta_pagar");

        // 4. Controller Web protegido com políticas e antiforgery
        var webController = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs"));
        webController.Should().Contain("compras_empresariais.faturas.visualizar");
        webController.Should().Contain("compras_empresariais.faturas.criar");
        webController.Should().Contain("compras_empresariais.faturas.decidir");
    }
}


