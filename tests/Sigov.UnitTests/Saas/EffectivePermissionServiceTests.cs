using FluentAssertions;
using Sigov.Application.Saas.Profiles;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class EffectivePermissionServiceTests
{
    [Fact]
    public async Task Administrador_geral_tem_escopo_global()
    {
        var service = new EffectivePermissionService(new FakeProfileRepository(new[] { "ADMINISTRADOR_GERAL" }));
        var result = await service.CalculateAsync(10, null, CancellationToken.None);
        result.Global.Should().BeTrue();
        result.HasPermission("saas.modulos.gerenciar").Should().BeTrue();
    }

    [Fact]
    public async Task Administrador_tenant_nao_acessa_outro_tenant_sem_escopo()
    {
        var service = new EffectivePermissionService(new FakeProfileRepository(new[] { "ADMINISTRADOR_TENANT" }));
        var result = await service.CalculateAsync(10, 2, CancellationToken.None);
        result.Global.Should().BeFalse();
        result.Scopes.Should().BeEmpty();
    }

    [Fact]
    public async Task Coordenador_sem_escopo_nao_recebe_area()
    {
        var service = new EffectivePermissionService(new FakeProfileRepository(new[] { "COORDENADOR" }));
        var result = await service.CalculateAsync(10, 1, CancellationToken.None);
        result.Scopes.Should().BeEmpty();
    }

    [Fact]
    public async Task Diretor_respeita_escopo()
    {
        var service = new EffectivePermissionService(new FakeProfileRepository(new[] { "DIRETOR" }, new[] { new UserAccessScope(1, 7, null, null, "ENTIDADE") }));
        var result = await service.CalculateAsync(10, 1, CancellationToken.None);
        result.Scopes.Should().ContainSingle(scope => scope.EntidadeId == 7);
    }

    [Fact]
    public async Task Servidor_acessa_apenas_permitido()
    {
        var service = new EffectivePermissionService(new FakeProfileRepository(new[] { "SERVIDOR" }, permissions: new[] { "processos.visualizar" }));
        var result = await service.CalculateAsync(10, 1, CancellationToken.None);
        result.Permissions.Should().Contain("processos.visualizar");
        result.Restrictions.Should().NotBeEmpty();
    }

    private sealed class FakeProfileRepository : IProfileLevelRepository
    {
        private readonly IReadOnlyCollection<string> _permissions;
        private readonly IReadOnlyCollection<string> _profiles;
        private readonly IReadOnlyCollection<UserAccessScope> _scopes;

        public FakeProfileRepository(IReadOnlyCollection<string> profiles, IReadOnlyCollection<UserAccessScope>? scopes = null, IReadOnlyCollection<string>? permissions = null)
        {
            _profiles = profiles;
            _scopes = scopes ?? Array.Empty<UserAccessScope>();
            _permissions = permissions ?? Array.Empty<string>();
        }

        public Task<IReadOnlyCollection<ProfileLevelItem>> GetLevelsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<ProfileLevelItem>>(Array.Empty<ProfileLevelItem>());
        public Task<IReadOnlyCollection<string>> GetUserProfileCodesAsync(long usuarioId, CancellationToken cancellationToken) => Task.FromResult(_profiles);
        public Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(long usuarioId, long? tenantId, CancellationToken cancellationToken) => Task.FromResult(_permissions);
        public Task<IReadOnlyCollection<UserAccessScope>> GetUserScopesAsync(long usuarioId, long? tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<UserAccessScope>>(_scopes.Where(scope => tenantId is null || scope.TenantId == tenantId).ToArray());
    }
}
