using System.Security.Claims;
using Sigov.Application.Security;

namespace Sigov.UnitTests.Security;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void Policies_HaveUniqueNames()
    {
        var policyNames = PermissionCatalog.Policies.Select(policy => policy.Policy).ToArray();

        Assert.Equal(policyNames.Length, policyNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void SupplierEdit_IsDifferentFromSupplierCreate()
    {
        var identity = new ClaimsIdentity(
            [new Claim("permission", "compras_empresariais.fornecedores.criar")],
            authenticationType: "test");
        var user = new ClaimsPrincipal(identity);
        var edit = Assert.Single(PermissionCatalog.All,
            permission => permission.Code == "compras_empresariais.fornecedores.editar");

        Assert.False(PermissionCatalog.UserHasPermission(user, edit));
    }

    [Fact]
    public void CompatibilityAlias_AuthorizesCanonicalPermission()
    {
        var identity = new ClaimsIdentity(
            [new Claim("permission", "com.meuerp.compras.fornecedor.editar")],
            authenticationType: "test");
        var user = new ClaimsPrincipal(identity);
        var edit = Assert.Single(PermissionCatalog.All,
            permission => permission.Code == "compras_empresariais.fornecedores.editar");

        Assert.True(PermissionCatalog.UserHasPermission(user, edit));
    }
}
