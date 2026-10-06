using FluentAssertions;
using Sigov.Application.Saas.Modules;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class ModuleAccessCheckerTests
{
    [Fact]
    public async Task Modulo_contratado_permite_acesso()
    {
        var checker = CreateChecker(new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Modulo_nao_contratado_bloqueia()
    {
        var checker = CreateChecker();
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "financeiro", new[] { "SERVIDOR" }), CancellationToken.None);
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Modulo_suspenso_bloqueia()
    {
        var checker = CreateChecker(new TenantModuleContract(1, "core", null, "SUSPENSO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Modulo_com_situacao_comercial_bloqueante_emite_BloqueadoComercial_e_nunca_fallback()
    {
        foreach (var status in new[] { "BLOQUEADO", "SUSPENSO", "INADIMPLENTE", "CANCELADO", "EXCLUIDO", "EXPIRADO" })
        {
            var checker = CreateCommercialChecker("ATIVO", null, new TenantModuleContract(1, "core", null, status, true));
            var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
            result.Allowed.Should().BeFalse($"status={status}");
            result.StatusCode.Should().Be(403, $"status={status}");
            result.Motivo.Should().Be(SaasForbiddenMotivo.BloqueadoComercial, $"status={status}");
        }
    }

    [Fact]
    public async Task Modulo_com_vigencia_expirada_bloqueia()
    {
        var checker = CreateChecker(new TenantModuleContract(1, "core", null, "ATIVO", true,
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))));

        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("expirado");
    }

    [Fact]
    public async Task Industria_producao_exige_dependencia_estoque()
    {
        var checker = CreateChecker(new TenantModuleContract(1, "industria_producao", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "industria_producao", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("Dependência de módulo não atendida");
    }

    [Fact]
    public async Task Feature_desabilitada_bloqueia()
    {
        var checker = CreateChecker(new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckFeatureAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), "core.operacao", CancellationToken.None);
        result.Allowed.Should().BeFalse();
        result.Motivo.Should().Be(SaasForbiddenMotivo.ForaEscopo);
    }

    [Fact]
    public void Fora_escopo_serializa_para_wire_FORA_ESCOPO()
        => SaasForbiddenMotivos.ToWire(SaasForbiddenMotivo.ForaEscopo).Should().Be("FORA_ESCOPO");

    [Fact]
    public async Task Tenant_inadimplente_bloqueia_tudo_com_BloqueadoComercial()
    {
        var checker = CreateCommercialChecker("INADIMPLENTE", null, new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Motivo.Should().Be(SaasForbiddenMotivo.BloqueadoComercial);
    }

    [Fact]
    public async Task Tenant_suspenso_bloqueia_tudo_com_BloqueadoComercial()
    {
        var checker = CreateCommercialChecker("SUSPENSO", null, new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Motivo.Should().Be(SaasForbiddenMotivo.BloqueadoComercial);
    }

    [Fact]
    public async Task Tenant_cancelado_bloqueia_tudo_com_BloqueadoComercial()
    {
        var checker = CreateCommercialChecker("CANCELADO", null, new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Motivo.Should().Be(SaasForbiddenMotivo.BloqueadoComercial);
    }

    [Fact]
    public async Task Assinatura_suspensa_bloqueia_com_BloqueadoComercial()
    {
        var checker = CreateCommercialChecker("ATIVO", new SaasSubscriptionSnapshot("SUSPENSA", null), new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeFalse();
        result.Motivo.Should().Be(SaasForbiddenMotivo.BloqueadoComercial);
    }

    [Fact]
    public async Task Assinatura_cancelada_bloqueia_com_BloqueadoComercial_mesmo_se_modulo_habilitado()
    {
        var checker = CreateCommercialChecker("ATIVO", new SaasSubscriptionSnapshot("CANCELADA", null), new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeFalse();
        result.Motivo.Should().Be(SaasForbiddenMotivo.BloqueadoComercial);
        result.Reason.Should().Contain("CANCELADA");
    }

    [Fact]
    public async Task Assinatura_ativa_com_vigencia_expirada_bloqueia_com_ContratoExpirado()
    {
        var checker = CreateCommercialChecker("ATIVO", new SaasSubscriptionSnapshot("ATIVA", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))), new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeFalse();
        result.Motivo.Should().Be(SaasForbiddenMotivo.ContratoExpirado);
    }

    [Fact]
    public async Task Assinatura_ativa_sem_data_fim_permite_acesso_ao_modulo_contratado()
    {
        var checker = CreateCommercialChecker("ATIVO", new SaasSubscriptionSnapshot("ATIVA", null), new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Sem_linha_de_assinatura_mantem_caminho_legacy_do_modulo()
    {
        var checker = CreateCommercialChecker("ATIVO", null);
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "financeiro", new[] { "SERVIDOR" }), CancellationToken.None);
        result.Allowed.Should().BeFalse();
        result.Motivo.Should().Be(SaasForbiddenMotivo.ModuloNaoContratado);
    }

    [Fact]
    public void Motivos_comerciais_serializam_para_wire_canonica()
    {
        SaasForbiddenMotivos.ToWire(SaasForbiddenMotivo.BloqueadoComercial).Should().Be("BLOQUEADO_COMERCIAL");
        SaasForbiddenMotivos.ToWire(SaasForbiddenMotivo.ContratoExpirado).Should().Be("CONTRATO_EXPIRADO");
        SaasForbiddenMotivos.ToWire(SaasForbiddenMotivo.LimiteAtingido).Should().Be("LIMITE_ATINGIDO");
    }

    private static ModuleAccessChecker CreateChecker(params TenantModuleContract[] contracts) => new(new ModuleCatalogService(), new FakeModuleAccessRepository(contracts));

    private static ModuleAccessChecker CreateCommercialChecker(string tenantStatus, SaasSubscriptionSnapshot? subscription, params TenantModuleContract[] contracts) =>
        new(new ModuleCatalogService(), new FakeModuleAccessRepository(contracts, tenantStatus, subscription));

    private sealed class FakeModuleAccessRepository : IModuleAccessRepository
    {
        private readonly IReadOnlyCollection<TenantModuleContract> _contracts;
        private readonly string _tenantStatus;
        private readonly SaasSubscriptionSnapshot? _subscription;

        public FakeModuleAccessRepository(IReadOnlyCollection<TenantModuleContract> contracts, string tenantStatus = "ATIVO", SaasSubscriptionSnapshot? subscription = null)
        {
            _contracts = contracts;
            _tenantStatus = tenantStatus;
            _subscription = subscription;
        }

        public Task<TenantModuleContract?> GetTenantModuleAsync(long tenantId, string moduleCode, CancellationToken cancellationToken) => Task.FromResult(_contracts.FirstOrDefault(item => item.TenantId == tenantId && string.Equals(item.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase)));
        public Task<IReadOnlyCollection<TenantModuleContract>> GetTenantModulesAsync(long tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<TenantModuleContract>>(_contracts.Where(item => item.TenantId == tenantId).ToArray());
        public Task<bool> IsFeatureEnabledAsync(long tenantId, string moduleCode, string featureCode, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task UpsertTenantModuleStatusAsync(long tenantId, string moduleCode, string status, long? userId, Guid? correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string?> GetTenantStatusAsync(long tenantId, CancellationToken cancellationToken) => Task.FromResult<string?>(_tenantStatus);
        public Task<SaasSubscriptionSnapshot?> GetActiveSubscriptionAsync(long tenantId, CancellationToken cancellationToken) => Task.FromResult<SaasSubscriptionSnapshot?>(_subscription);
    }
}
