using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.IntegrationTests;

public sealed class SaasAdminRegressionTests
{
    [Fact]
    public void SaasAdmin_Deve_Ter_Telas_Principais()
    {
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/TenantDetalhe.cshtml")).Should().Contain("Assinatura").And.Contain("Auditoria");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/Tenants.cshtml")).Should().NotContain("Município Comercial");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/TenantDetalhe.cshtml")).Should().NotContain("Município Comercial");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/SaasAdminController.cs")).Should().Contain("ISaasTenantAdministrationService").And.Contain("justification");
        var service = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/SaasTenantAdministrationService.cs"));
        service.Should().Contain("reativação não renova o contrato automaticamente")
            .And.Contain("Somente uma contratação suspensa pode ser reativada")
            .And.Contain("if (before is null)")
            .And.Contain("var currentContract = before;")
            .And.Contain("PreserveTerm = !isNewContract")
            .And.Contain("tenant_modulo_contratado.vigencia_inicio")
            .And.Contain("RandomNumberGenerator.GetBytes(48)")
            .And.Contain("deve_alterar_senha")
            .And.Contain("tenant_id=@TenantId and (codigo_externo=@Codigo or nome=@Codigo)")
            .And.Contain("RevokeTenantSessionsAsync")
            .And.Contain("RevokeUserSessionsAsync")
            .And.NotContain("$2a$11$")
            .And.NotContain("before!");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/TenantDetalhe.cshtml"))
            .Should().Contain("nenhuma cobrança será quitada ou renovada automaticamente")
            .And.Contain("não inclui dependências silenciosamente")
            .And.Contain("o cadastro não gera nem exibe senha provisória")
            .And.Contain("Recuperar acesso");
    }

    [Fact]
    public void RC_SAAS_AUT_A9_limite_transacional_emite_LIMITE_ATINGIDO_e_status_bloqueante_emite_BLOQUEADO_COMERCIAL()
    {
        var contracts = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Saas/SuperAdmin/TenantAdministrationContracts.cs"));
        contracts.Should().Contain("Motivo403");
        var service = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/SaasTenantAdministrationService.cs"));
        service.Should().Contain("SaasForbiddenMotivo.LimiteAtingido");
        var checker = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Saas/Modules/ModuleAccessChecker.cs"));
        checker.Should().Contain("TenantCommercialStatus.IsBlocked(contract.Status)")
            .And.Contain("SaasForbiddenMotivo.BloqueadoComercial");
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/SaasAdminController.cs"));
        controller.Should().Contain("ForbiddenResponse.Registrar(this, result.Motivo403.Value, result.Message)")
            .And.Contain("SAAS_USUARIO_CRIAR_LIMITE");
        var helper = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Helpers/ForbiddenResponse.cs"));
        helper.Should().Contain("StatusCodes.Status403Forbidden").And.Contain("SaasForbiddenMotivos.ToWire");
    }

    [Fact]
    public void RC_SAAS_AUT_A3_ultimo_admin_protegido_e_anti_autopromocao_em_ambas_as_superficies()
    {
        var infra = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/SaasTenantAdministrationService.cs"));
        infra.Should().Contain("Não é possível inativar ou bloquear o último administrador do cliente. Transfira a permissão para outro usuário antes.")
            .And.Contain("not p.delegavel")
            .And.Contain("p3.chave='saas.plataforma.administrar'")
            .And.Contain("Anti-autopromoção: o perfil contém permissões que não podem ser delegadas pelo usuário atual")
            .And.Contain("USUARIO_INATIVO");
        var web = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Services/SegurancaAdminService.cs"));
        web.Should().Contain("USUARIO_INATIVAR_RECUSADO")
            .And.Contain("motivo = \"ultimo_admin\"")
            .And.Contain("motivo_encerramento = 'USUARIO_INATIVO'")
            .And.Contain("PERMISSOES_SALVAR_RECUSADO")
            .And.Contain("not p.delegavel")
            .And.Contain("p3.chave='saas.plataforma.administrar'");
    }

    [Fact]
    public void RC_SAAS_AUT_A4_ordem_deterministica_dupla_e_troca_de_contexto_auditada_na_sessao()
    {
        const string ordem = "order by case when pa.sistemico and exists(select 1 from sigov.perfil_permissao pp_x join sigov.permissao p_x on p_x.id=pp_x.permissao_id and p_x.chave='contexto.empresa.assumir' where pp_x.perfil_acesso_id=pa.id and pp_x.ativo and not pp_x.is_deleted) then 0 when pa.sistemico then 1 else 2 end, pa.id asc limit 1";
        var repo = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/TenantContextSwitchRepository.cs"));
        // duas ordens determinísticas: CTE perfil de ValidateAsync + ResolveProfileAsync.
        (repo.Split(ordem).Length - 1).Should().BeGreaterThanOrEqualTo(2);
        repo.Should().NotContain("order by pa.sistemico desc");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/TenantContextController.cs"))
            .Should().Contain("TimeSpan.FromHours(8)")
            .And.Contain("MaxAge = SessionLifetime");
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/SaasAdminController.cs"));
        controller.Should().Contain("StartSwitchAsync")
            .And.Contain("FinishSwitchAsync")
            .And.Contain("update sigov.identidade_sessao")
            .And.Contain("set tenant_id = @To, exercicio_id = null")
            .And.Contain("contexto_auditado")
            .And.Contain("[HttpPost(\"VoltarContexto\")]");
    }

    [Fact]
    public void RC_SAAS_AUT_A7_erro_403_exibe_motivo_canonico_e_matriz_vem_do_banco()
    {
        var error = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Home/Error.cshtml"));
        error.Should().Contain("ForbiddenResponse.MotivoItemKey")
            .And.Contain("ForbiddenResponse.DetalheItemKey")
            .And.Contain("Motivo: <code>");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/SegurancaController.cs"))
            .Should().Contain("ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao");
        var matriz = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/MatrizAcessoController.cs"));
        matriz.Should().Contain("ListarPerfisAsync")
            .And.Contain("ObterPermissoesPerfilAsync")
            .And.NotContain("private static readonly string[] Profiles")
            .And.NotContain("\"SUPERADMIN\", \"ADMIN_TENANT\"");
    }

    [Fact]
    public void RC_SAAS_AUT_A6_usos_limites_e_historico_de_assinaturas_no_detalhe()
    {
        var contracts = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Saas/SuperAdmin/TenantAdministrationContracts.cs"));
        contracts.Should().Contain("SaasUsageSummary? Usage = null")
            .And.Contain("IReadOnlyList<SaasSubscriptionHistoryItem> History = null!")
            .And.Contain("record SaasSubscriptionHistoryItem(");
        var service = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/SaasTenantAdministrationService.cs"));
        service.Should().Contain("limitValidator.GetUsageSummaryAsync(tenantId, cancellationToken)")
            .And.Contain("from sigov.saas_assinatura_historico h");
        var view = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/TenantDetalhe.cshtml"));
        view.Should().Contain("Uso contra limites do plano")
            .And.Contain("Histórico de assinaturas")
            .And.Contain("PercentualUsoUsuarios");
    }
}
