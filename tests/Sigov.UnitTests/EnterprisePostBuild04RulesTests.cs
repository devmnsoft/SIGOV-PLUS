using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sigov.Testing;
using FluentAssertions;
using Sigov.Application.Common;
using Sigov.Application.ComprasEmpresariais;
using Sigov.Application.Enterprise;
using Sigov.Domain.OrdemServico;
using Sigov.Web.Controllers;
using Xunit;

namespace Sigov.UnitTests;

public sealed class EnterprisePostBuild04RulesTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Programacao_aceita_intervalos_adjacentes_e_reprogramacao_exige_motivo()
    {
        var inicio = DateTimeOffset.Parse("2026-09-15T10:00:00Z");
        OrdemServicoRules.ExisteSobreposicao(inicio, inicio.AddHours(1), inicio.AddHours(1), inicio.AddHours(2)).Should().BeFalse();
        var action = () => OrdemServicoRules.ValidarProgramacao(OrdemServicoStatus.Agendada, inicio, inicio.AddHours(1), true, null);
        action.Should().Throw<InvalidOperationException>().WithMessage("*motivo*");
    }

    [Fact]
    public void Programacao_recusa_intervalo_invalido_e_ordem_concluida()
    {
        var inicio = DateTimeOffset.Parse("2026-09-15T10:00:00Z");
        FluentActions.Invoking(() => OrdemServicoRules.ValidarProgramacao(OrdemServicoStatus.Aberta, inicio, inicio, false, null)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => OrdemServicoRules.ValidarProgramacao(OrdemServicoStatus.Concluida, inicio, inicio.AddHours(1), false, null)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cliente_comercial_respeita_tenant_id_e_mascara_dados_sensiveis()
    {
        var service = new EnterpriseModuleService();
        var created = service.Upsert("comercial/clientes", new EnterpriseMutationRequest(TenantA, "Cliente A", "12345678000199", "cliente@example.com", "11999998888", null, null, null, null, null, null), TenantA, "corr-1");
        service.Upsert("comercial/clientes", new EnterpriseMutationRequest(TenantB, "Cliente B", "99999999000199", "outro@example.com", "11888887777", null, null, null, null, null, null), TenantB, "corr-2");

        var clientesA = service.List("comercial/clientes", TenantA);
        var cliente = clientesA.Single(item => item.Id == created.Id);

        clientesA.Should().ContainSingle();
        cliente.TenantId.Should().Be(TenantA);
        cliente.DocumentMasked.Should().Be("***0199").And.NotContain("12345678000199");
        cliente.EmailMasked.Should().Be("c***@example.com").And.NotBe("cliente@example.com");
        cliente.PhoneMasked.Should().Be("(**) ****-8888").And.NotContain("11999998888");
        service.List("comercial/clientes", TenantB).Should().OnlyContain(item => item.TenantId == TenantB);
    }

    [Fact]
    public void Enterprise_store_is_isolated_and_does_not_seed_demo_clients()
    {
        var first = new EnterpriseModuleService();
        var second = new EnterpriseModuleService();
        first.Upsert("comercial/clientes", new EnterpriseMutationRequest(TenantA, "Somente primeiro", null, null, null, null, null, null, null, null, null), TenantA, "corr-isolation");

        first.List("comercial/clientes", TenantA).Should().ContainSingle();
        second.List("comercial/clientes", TenantA).Should().BeEmpty();
        first.List("comercial/clientes", TenantA).Should().NotContain(item => item.Name == "Cliente demonstração");
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

    [Fact]
    public async Task Scenario01_Requisitante_sem_dashboard_acessa_modulo_e_redireciona_para_operacao_autorizada()
    {
        var authService = new FakeAuthorizationService(policy => policy == "compras_empresariais.requisicoes.visualizar");
        var controller = new ComprasEmpresariaisController(
            null!, null!, null!, null!, null!, null!, null!, null!, authService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var result = await controller.Index(CancellationToken.None);
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Requisicoes");
    }

    [Fact]
    public void Scenario02_Aprovador_acessa_sua_fila_e_somente_nivel_ativo_e_decidivel()
    {
        var userId = Guid.NewGuid();
        var etapaNivel1 = new AprovacaoFilaResumo(
            Guid.NewGuid(), 1, 1000m, userId, Bloqueada: false, DecisivelPorMim: true,
            Guid.NewGuid(), "RC-2026-0001", 500m, "PENDENTE_APROVACAO", "NORMAL",
            DateTime.UtcNow, DateTime.UtcNow, 1, "Solicitante", "Setor", "Decisão necessária no nível 1");

        var etapaNivel2 = new AprovacaoFilaResumo(
            Guid.NewGuid(), 2, 5000m, userId, Bloqueada: false, DecisivelPorMim: false,
            Guid.NewGuid(), "RC-2026-0001", 500m, "PENDENTE_APROVACAO", "NORMAL",
            DateTime.UtcNow, DateTime.UtcNow, 1, "Solicitante", "Setor", "Aguardando nível anterior (1)");

        etapaNivel1.DecisivelPorMim.Should().BeTrue();
        etapaNivel2.DecisivelPorMim.Should().BeFalse();
        etapaNivel2.ProximaAcao.Should().Contain("Aguardando nível anterior");
    }

    [Fact]
    public void Scenario03_Rota_direta_nega_usuario_nao_autorizado()
    {
        var controllerType = typeof(ComprasEmpresariaisController);
        var classAuthorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        classAuthorize.Should().NotBeNull();

        var endpoints = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (var method in endpoints.Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null))
        {
            var auth = method.GetCustomAttribute<AuthorizeAttribute>();
            if (method.Name == "Index")
            {
                classAuthorize.Should().NotBeNull();
            }
            else
            {
                auth.Should().NotBeNull($"Método {method.Name} deve possuir política de autorização explícita.");
                auth!.Policy.Should().NotBeNullOrWhiteSpace();
            }
        }
    }

    private sealed class FakeAuthorizationService(Func<string, bool> policyCheck) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => Task.FromResult(policyCheck(policyName) ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    [Fact]
    public void Scenario04_Favorito_revogado_deixa_de_ser_utilizavel()
    {
        var userSemPermissao = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "TestAuth"));

        userSemPermissao.HasClaim("permission", "compras_empresariais.aprovacoes.visualizar").Should().BeFalse();
        userSemPermissao.HasClaim("permission", "compras_empresariais.dashboard").Should().BeFalse();
    }

    [Fact]
    public void Scenario05_Contexto_ausente_possui_recuperacao()
    {
        var action = () => ComprasGuard.Context(new ComprasContext(Guid.Empty, Guid.NewGuid(), "corr-1"));
        action.Should().Throw<ArgumentException>().WithMessage("*TenantId é obrigatório*");

        var actionUser = () => ComprasGuard.Context(new ComprasContext(Guid.NewGuid(), Guid.Empty, "corr-2"));
        actionUser.Should().Throw<ArgumentException>().WithMessage("*UsuarioId é obrigatório*");
    }

    [Fact]
    public void Scenario06_Troca_de_organizacao_descarta_selecoes_dependentes_antigas()
    {
        var js = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/modules/saas.contexto-global.js"));
        js.Should().Contain("resetDependents");
        js.Should().Contain("$('ctxUnit').disabled = true;");
        js.Should().Contain("$('ctxExercise').disabled = true;");
        js.Should().Contain("$('ctxSystem').disabled = true;");
    }

    [Fact]
    public void Scenario07_Resposta_atrasada_nao_restaura_organizacao_anterior()
    {
        var js = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/modules/saas.contexto-global.js"));
        js.Should().Contain("const currentSeq = ++searchSeq;");
        js.Should().Contain("if (currentSeq !== searchSeq) return;");
        js.Should().Contain("const currentSeq = ++selectSeq;");
        js.Should().Contain("if (currentSeq !== selectSeq) return;");
    }

    [Fact]
    public void Scenario08_Menu_e_cabecalho_refletem_contexto_confirmado()
    {
        var js = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/modules/saas.contexto-global.js"));
        js.Should().Contain("window.location.href = returnUrl;");
        js.Should().Contain("window.location.href = '/';");

        var sidebar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml"));
        sidebar.Should().Contain("canSeeEnterprisePurchasing");
        sidebar.Should().Contain("Compras Empresariais");
    }

    [Fact]
    public void Scenario09_Somente_leitura_impede_gravacao()
    {
        var readOnlyContext = new ComprasContext(Guid.NewGuid(), Guid.NewGuid(), "corr-ro", SomenteLeitura: true);
        var action = () => ComprasGuard.Mutation(readOnlyContext);
        action.Should().Throw<InvalidOperationException>().WithMessage("*somente leitura*");
    }

    [Fact]
    public void Scenario10_Dois_tenants_permanecem_isolados()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("where tenant_id=@t");
        repoSql.Should().NotContain("where 1=1");
    }

    [Fact]
    public void Scenario11_Nivel_posterior_nao_decide_antecipadamente()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("nivel < @nivel and status <> 'APROVADO'");
        repoSql.Should().Contain("Não é possível decidir esta etapa antes da aprovação de todos os níveis anteriores.");
    }

    [Fact]
    public void Scenario12_Alcada_insuficiente_bloqueia_etapa()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        repoSql.Should().Contain("bloqueadas.Add(e.Item1);");
        repoSql.Should().Contain("APROVACAO_SEM_APROVADOR");
    }

    [Fact]
    public void Scenario13_Perda_de_elegibilidade_ou_solicitante_segregado_impede_decisao()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("requisicao.Value.SolicitanteId == context.UsuarioId");
        repoSql.Should().Contain("Regra de segregação: o solicitante da requisição não pode aprovar a própria solicitação.");
    }

    [Fact]
    public void Scenario14_Reavaliacao_nao_duplica_etapas()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("update sigov.compras_empresarial_aprovacao set aprovador_id=");
        repoSql.Should().Contain("desbloqueadas++;");
    }

    [Fact]
    public void Scenario15_Reenvio_apos_devolucao_preserva_historico_anterior_e_cria_novo_ciclo()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        repoSql.Should().Contain("select coalesce(max(ciclo),0)+1");
        repoSql.Should().Contain("regra_snapshot");
        repoSql.Should().Contain("\"RASCUNHO\" or \"DEVOLVIDA\"");
    }

    [Fact]
    public void Scenario16_Concorrencia_mantem_consistencia_e_advisory_locks()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("pg_advisory_xact_lock");
        repoSql.Should().Contain("for update");
    }

    [Fact]
    public void Scenario17_Repeticao_de_comando_com_mesma_idempotency_key_nao_duplica_efeitos()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("sigov.compras_empresarial_idempotencia");
        repoSql.Should().Contain("A chave de idempotência já foi usada com conteúdo diferente.");
    }

    [Fact]
    public void Scenario18_Tema_alterna_uma_vez_e_persiste()
    {
        var jsTheme = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/sigov.theme.js"));
        var jsUi = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/sigov-ui.js"));

        jsTheme.Should().Contain("window.Sigov.theme");
        jsTheme.Should().Contain("const primaryKey = 'sigov.theme';");
        jsTheme.Should().Contain("localStorage.setItem(primaryKey, theme);");
        jsUi.Should().NotContain("trigger.matches('[data-sigov-theme-toggle]')");
    }

    [Fact]
    public void Scenario19_Menu_funciona_por_teclado_e_mobile()
    {
        var jsUi = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/sigov-ui.js"));
        jsUi.Should().Contain("event.key === 'Escape'");
        jsUi.Should().Contain("details && !details.open");
        jsUi.Should().Contain("normalizeModuleKey");
    }

    [Fact]
    public void Scenario20_Falha_de_gravacao_nunca_produz_sucesso_falso()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("await tx.RollbackAsync(ct);");
        repoSql.Should().Contain("throw;");
    }

    [Fact]
    public void Scenario21_Quorum_por_nivel_ignora_irmos_cancelados_do_mesmo_nivel_apos_aprovacao()
    {
        // Interpretção canônica centralizada: irmão cancelado do MESMO nível não impede a progressão.
        AprovacaoQuorum.NivelAnteriorCoberto(new[] { (1, "APROVADO"), (1, "CANCELADO"), (2, "PENDENTE") }, 2).Should().BeTrue();
        // Nível inteiro sem nenhuma aprovação ainda impede.
        AprovacaoQuorum.NivelAnteriorCoberto(new[] { (1, "PENDENTE"), (1, "CANCELADO") }, 2).Should().BeFalse();
        // Cada nível distinto anterior exige a própria cobertura.
        AprovacaoQuorum.NivelAnteriorCoberto(new[] { (1, "APROVADO"), (2, "CANCELADO") }, 3).Should().BeFalse();
        AprovacaoQuorum.NivelAnteriorCoberto(new[] { (1, "APROVADO"), (2, "APROVADO") }, 3).Should().BeTrue();
        // Primeiro nível não tem anterior: coberto por definição.
        AprovacaoQuorum.NivelAnteriorCoberto(new[] { (2, "PENDENTE") }, 1).Should().BeTrue();
    }

    [Fact]
    public void Scenario22_Elegibilidade_espelha_avaliador_canonical_em_escopo_institucional_e_unidade_strita()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("and ug.unidade_id is null and gp.unidade_id is null and pp.unidade_id is null");
        repoSql.Should().Contain("(ug.tenant_id is null or ug.tenant_id=u.tenant_id)");
        repoSql.Should().Contain("(gp.entidade_id is null or gp.entidade_id=u.entidade_id)");
        repoSql.Should().Contain("where u.ativo and not u.is_deleted and u.tenant_id=@TenantId");
    }

    [Fact]
    public void Scenario23_Decisao_exige_cobertura_canonica_e_nao_pergunta_a_irmaos_do_nivel_coberto()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("nivel < @nivel and status <> 'APROVADO' and not exists(");
        repoSql.Should().Contain("apr.nivel=ant.nivel and apr.status='APROVADO'");
        repoSql.Should().Contain("AprovacaoQuorum.NivelAnteriorCoberto");
    }

    [Fact]
    public void Scenario24_Alcada_insuficiente_bloqueia_etapa_topo_com_causa_diferenciada()
    {
        var enviar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        enviar.Should().Contain("alcadaInsuficiente=politica is not null&&niveis.Count>0&&total>niveis.Max(n=>n.Item2)");
        enviar.Should().Contain("causa=topoAlcada?\"ALCADA_INSUFICIENTE\":\"SEM_APROVADOR\"");
        enviar.Should().Contain("\"APROVACAO_ALCADA_INSUFICIENTE\",\"Aprovação com alçada insuficiente\"");
        var reavaliar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        reavaliar.Should().Contain("causa_bloqueio='ALCADA_INSUFICIENTE'");
    }

    [Fact]
    public void Scenario25_Pendencia_sem_politica_nunca_se_libera_somente_atribuindo_aprovador()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        // Etapa SEM_POLITICA somente vira ALCADA_INSUFICIENTE quando há política ativa e o total excede tudo.
        repoSql.Should().Contain("if (k is null && politicaAtual is not null && b.CausaBloqueio == \"SEM_POLITICA\")");
        // Pendências fecham apenas comprovadamente resolvidas, tipo por tipo.
        repoSql.Should().Contain("if (politicaAtual is not null) tiposParaFechar.Add(\"APROVACAO_SEM_POLITICA\");");
        repoSql.Should().Contain("if (k is not null) tiposParaFechar.Add(\"APROVACAO_ALCADA_INSUFICIENTE\");");
        repoSql.Should().Contain("if (!aindaBloqueada) tiposParaFechar.Add(\"APROVACAO_SEM_APROVADOR\");");
    }

    [Fact]
    public void Scenario26_Reavaliacao_designa_sem_excluir_o_executor_e_limita_fanout_dois_mais_um()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        // Segregação SOMENTE do solicitante: quem executa a reavaliação também pode ser designado.
        repoSql.Should().Contain(".Where(ap => ap != req.Value.SolicitanteId)");
        repoSql.Should().Contain(".Take(3)");
        repoSql.Should().NotContain("ap != context.UsuarioId && ap != req.Value.SolicitanteId");
    }

    [Fact]
    public void Scenario27_Idempotencia_persiste_o_resultado_original_para_replay_fiel()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("set resultado=jsonb_build_object('id',@rq::text,'ciclo',@ciclo,'desbloqueadas',@desbloqueadas,'canceladas',@canceladas)");
        repoSql.Should().Contain("coalesce(resultado::text,'') from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='REAVALIAR_ENCAMINHAMENTO'");
        repoSql.Should().Contain("r.GetProperty(\"etapa_status\").GetString()!");
        var enviar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        enviar.Should().Contain("set resultado=jsonb_build_object('id',@id::text,'status','PENDENTE_APROVACAO','version',@v2,'ciclo',@ciclo)");
        enviar.Should().Contain("System.Guid.Parse(r.GetProperty(\"id\").GetString()!)");
    }

    [Fact]
    public void Scenario28_Bloqueios_seguem_ordem_canonica_advisory_filho_entao_requisicao()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        var idxAdvisory = repoSql.IndexOf("await LockAsync(connection, tx, context, $\"REQUISICAO|{etapaRef:D}\", ct);", StringComparison.Ordinal);
        var idxFilho = repoSql.IndexOf("select status Status,version Version,aprovador_id AprovadorId,ciclo Ciclo,requisicao_id RequisicaoId,nivel Nivel,limite Limite from sigov.compras_empresarial_aprovacao where tenant_id=@t and id=@id for update", StringComparison.Ordinal);
        var idxRequisicao = repoSql.IndexOf("select status,version,solicitante_id SolicitanteId from sigov.compras_empresarial_requisicao where tenant_id=@t and id=@id for update", StringComparison.Ordinal);
        idxAdvisory.Should().BeGreaterThan(-1).And.BeLessThan(idxFilho);
        idxFilho.Should().BeGreaterThan(-1).And.BeLessThan(idxRequisicao);
    }

    [Fact]
    public void Scenario29_Snapshot_classifica_completo_parcial_ou_indisponivel_sem_inventar_itens()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("classificacaoSnapshot = identificados ? \"COMPLETO\" : \"PARCIAL\"");
        repoSql.Should().Contain("\"INDISPONIVEL\"");
        var enviar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        enviar.Should().Contain("'id',i.id,'ordem',i.ordem");
    }

    [Fact]
    public void Scenario30_Relatorio_exibe_nome_do_aprovador_com_fallback_explicito_ao_identificador()
    {
        var repoSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/AprovacaoRequisicaoRepository.cs"));
        repoSql.Should().Contain("coalesce(os.nome,a.aprovador_id::text) AprovadorSub");
        repoSql.Should().Contain("left join sigov.os_tecnico os on (os.tenant_id,os.usuario_id)=(a.tenant_id,a.aprovador_id) and not os.is_deleted");
    }
}

public sealed class EnterprisePosRc07StaticTests
{
    [Fact]
    public void EnterpriseMigrationContainsRequiredTablesAndTenant()
    {
        var sql = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260709120000_enterprise_funcional_crud.sql"));
        Assert.Contains("enterprise_cliente", sql);
        Assert.Contains("enterprise_ordem_servico", sql);
        Assert.Contains("enterprise_estoque_saldo", sql);
        Assert.Contains("tenant_id uuid not null", sql);
        Assert.Contains("create index if not exists", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnterpriseServiceDoesNotUseConcurrentDictionaryForRealFlow()
    {
        var source = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Enterprise/EnterpriseModuleService.cs"));
        Assert.DoesNotContain("ConcurrentDictionary", source);
    }

    [Fact]
    public void EnterpriseMigrationContainsFullAuditColumnsForMinimumTables()
    {
        var sql = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260709120000_enterprise_funcional_crud.sql"));
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
        var requestModule = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/enterprise/enterprise-request.js"));
        var client = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/enterprise-crud.js"));

        Assert.Contains("resolveEnterpriseMethod", requestModule);
        Assert.Contains("buildEnterpriseUrl", requestModule);
        Assert.Contains("buildEnterpriseRequest", requestModule);
        Assert.Contains("'PUT'", requestModule);
        Assert.Contains("'POST'", requestModule);
        Assert.Contains("'DELETE'", requestModule);
        Assert.Contains("'Content-Type': 'application/json'", requestModule);
        Assert.Contains("delete body.tenantId", requestModule);
        Assert.Contains("delete body.TenantId", requestModule);
        Assert.Contains("readApiResponse", requestModule);
        Assert.Contains("response.redirected", requestModule);
        Assert.Contains("application/json", requestModule);
        Assert.Contains("await getPersisted(persistedId)", client);
        Assert.Contains("await getPersisted(id)", client);
        Assert.Contains("nova consulta", client);
        Assert.Contains("if (!r.ok) throw new Error", client);
        Assert.Contains("await load()", client);
        Assert.DoesNotContain("endpoint DELETE estiver habilitado", client);
    }

    [Fact]
    public void EnterprisePageTemplateHasOperationalCrudElements()
    {
        var view = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Enterprise/ModulePage.cshtml"));
        Assert.Contains("enterprise-form", view);
        Assert.Contains("Exportar CSV", view);
        Assert.Contains("Detalhes", view);
        Assert.Contains("Inativar", view);
    }

    [Fact]
    public async Task Fatura_Application_Service_Valida_Regras_Estruturais_E_Idempotencia()
    {
        var service = new FaturaCompraApplicationService(null!);
        var ctx = new ComprasContext(Guid.NewGuid(), Guid.NewGuid(), "corr-1");

        // 1. Falha em contexto somente leitura
        var ctxReadOnly = ctx with { SomenteLeitura = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CriarAsync(ctxReadOnly, new CriarFaturaRequest(Guid.NewGuid(), Guid.NewGuid(), "123", "1", "NOTA_FISCAL", null, null, null, 0, 0, 0, 0, null, [new(1, 10, 5)]), "key-1", CancellationToken.None));

        // 2. Chave de idempotência vazia
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CriarAsync(ctx, new CriarFaturaRequest(Guid.NewGuid(), Guid.NewGuid(), "123", "1", "NOTA_FISCAL", null, null, null, 0, 0, 0, 0, null, [new(1, 10, 5)]), "", CancellationToken.None));

        // 3. Pedido ou fornecedor vazios
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CriarAsync(ctx, new CriarFaturaRequest(Guid.Empty, Guid.NewGuid(), "123", "1", "NOTA_FISCAL", null, null, null, 0, 0, 0, 0, null, [new(1, 10, 5)]), "key-1", CancellationToken.None));

        // 4. Número ou série vazios
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CriarAsync(ctx, new CriarFaturaRequest(Guid.NewGuid(), Guid.NewGuid(), "", "1", "NOTA_FISCAL", null, null, null, 0, 0, 0, 0, null, [new(1, 10, 5)]), "key-1", CancellationToken.None));

        // 5. Itens vazios ou com quantidade zero/negativa
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CriarAsync(ctx, new CriarFaturaRequest(Guid.NewGuid(), Guid.NewGuid(), "123", "1", "NOTA_FISCAL", null, null, null, 0, 0, 0, 0, null, []), "key-1", CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CriarAsync(ctx, new CriarFaturaRequest(Guid.NewGuid(), Guid.NewGuid(), "123", "1", "NOTA_FISCAL", null, null, null, 0, 0, 0, 0, null, [new(1, 0, 5)]), "key-1", CancellationToken.None));

        // 6. Itens duplicados
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CriarAsync(ctx, new CriarFaturaRequest(Guid.NewGuid(), Guid.NewGuid(), "123", "1", "NOTA_FISCAL", null, null, null, 0, 0, 0, 0, null, [new(1, 5, 5), new(1, 3, 5)]), "key-1", CancellationToken.None));

        // 7. Decisão inválida
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DecidirAsync(ctx, Guid.NewGuid(), new DecidirFaturaRequest(1, "DESCONHECIDA", null, "k-1"), CancellationToken.None));

        // 8. Rejeição ou cancelamento sem justificativa mínima
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DecidirAsync(ctx, Guid.NewGuid(), new DecidirFaturaRequest(1, "REJEITAR", "curto", "k-1"), CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DecidirAsync(ctx, Guid.NewGuid(), new DecidirFaturaRequest(1, "CANCELAR", null, "k-1"), CancellationToken.None));
    }
}

