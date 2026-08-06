using System.Security.Claims;
using Sigov.Web.Models.QuickCreate;

namespace Sigov.Web.Services;

public sealed class QuickCreateService
{
    private static readonly IReadOnlyDictionary<string, QuickCreateOption> Options =
        new Dictionary<string, QuickCreateOption>(StringComparer.OrdinalIgnoreCase)
        {
            ["protocolo"] = new("protocolo", "protocolo.criar", "/Protocolo/Novo"),
            ["documento"] = new("documento", "ged.upload", "/Ged/Novo"),
            ["tarefa"] = new("tarefa", "tarefa.criar", "/Tarefas/Nova"),
            ["contrato"] = new("contrato", "contratos.criar", "/Contratos/Novo"),
            ["fornecedor"] = new("fornecedor", "compras_empresariais.fornecedores.criar", "/ComprasEmpresariais/Fornecedores/Novo"),
            ["usuario"] = new("usuario", "seguranca.usuarios.criar", "/Seguranca/Usuarios/Novo", true),
            ["chamado"] = new("chamado", "suporte.criar", "/Atendimento/Novo")
        };

    private readonly IUserPermissionService _permissions;
    public QuickCreateService(IUserPermissionService permissions) => _permissions = permissions;

    public QuickCreateOption? Find(string key) => Options.GetValueOrDefault(key);

    public bool CanStart(ClaimsPrincipal user, QuickCreateOption option)
    {
        var admin = user.IsInRole("ADMIN_GERAL") || user.IsInRole("ADMIN_TENANT") || user.IsInRole("ADMINISTRADOR_GERAL");
        return (!option.AdminOnly || admin) && (_permissions.HasPermission(user, option.Permission) || admin);
    }
}
