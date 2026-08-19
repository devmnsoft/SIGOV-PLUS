using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Security;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("Seguranca/MatrizAcesso")]
public sealed class MatrizAcessoController : Controller
{
    private static readonly string[] Profiles =
    {
        "SUPERADMIN", "ADMIN_TENANT", "GESTOR_MUNICIPAL", "COORDENADOR_AREA", "OPERACIONAL",
        "FINANCEIRO", "AUDITOR", "ATENDIMENTO", "GESTOR_MODULO", "LEITURA", "CIDADAO"
    };
    private readonly IAuditTrailService _audit;

    public MatrizAcessoController(IAuditTrailService audit) => _audit = audit;

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? perfil) => View(Build(perfil));

    [HttpGet("Exportar")]
    public async Task<IActionResult> Exportar([FromQuery] string? perfil, CancellationToken cancellationToken)
    {
        var model = Build(perfil);
        if (!model.CanExport)
        {
            await AuditAsync("EXPORTACAO_NEGADA", model.Profile, "permissao_exportar_ausente", cancellationToken).ConfigureAwait(false);
            return Forbid();
        }

        var csv = new StringBuilder("modulo;recurso;acao;liberado;motivo\n");
        foreach (var row in model.Rows)
            csv.Append(Csv(row.Module)).Append(';').Append(Csv(row.Resource)).Append(';').Append(Csv(row.Action)).Append(';')
                .Append(row.Allowed ? "sim" : "nao").Append(';').Append(Csv(row.Reason)).AppendLine();
        await AuditAsync("MATRIZ_ACESSO_EXPORTADA", model.Profile, "exportacao_autorizada", cancellationToken).ConfigureAwait(false);
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "matriz-acesso.csv");
    }

    private AccessMatrixViewModel Build(string? requestedProfile)
    {
        var profile = Profiles.Contains(requestedProfile, StringComparer.OrdinalIgnoreCase)
            ? requestedProfile!.ToUpperInvariant()
            : CurrentProfile();
        var rows = Matrix(profile);
        var canExport = IsSuperAdmin() || User.HasClaim("permission", "seguranca.matriz.exportar") || User.HasClaim("permissao", "seguranca.matriz.exportar");
        return new AccessMatrixViewModel { Profile = profile, Profiles = Profiles, Rows = rows, CanExport = canExport };
    }

    private static IReadOnlyList<AccessMatrixRowViewModel> Matrix(string profile)
    {
        var grants = profile switch
        {
            "SUPERADMIN" => new[] { "todos:todos:todos" },
            "ADMIN_TENANT" => new[] { "seguranca:usuarios:gerenciar", "seguranca:perfis:gerenciar", "tenant:configuracao:configurar" },
            "FINANCEIRO" => new[] { "financeiro:pagamento:baixar", "financeiro:fatura:consultar", "tributario:debito:consultar", "relatorios:financeiro:visualizar" },
            "AUDITOR" => new[] { "auditoria:trilha:visualizar", "lgpd:acessos:visualizar", "relatorios:governanca:visualizar" },
            "ATENDIMENTO" => new[] { "processos:protocolo:criar", "ouvidoria:manifestacao:encaminhar", "esic:pedido:consultar" },
            "COORDENADOR_AREA" => new[] { "area:dashboard:visualizar", "area:cadastro:validar", "area:tarefa:distribuir" },
            "GESTOR_MUNICIPAL" => new[] { "area:dashboard:visualizar", "area:relatorio:visualizar", "area:operacao:aprovar" },
            "OPERACIONAL" => new[] { "modulo:registro:criar", "modulo:registro:editar", "modulo:registro:consultar" },
            "GESTOR_MODULO" => new[] { "modulo:parametro:configurar", "modulo:auxiliar:gerenciar" },
            "CIDADAO" => new[] { "portal:dado_proprio:consultar", "portal:protocolo:criar" },
            _ => new[] { "modulo:registro:consultar" }
        };
        var rows = grants.Select(value => value.Split(':')).Select(parts => new AccessMatrixRowViewModel(parts[0], parts[1], parts[2], true, "Liberado pelo perfil padrão.")).ToList();
        rows.Add(new AccessMatrixRowViewModel("seguranca", "configuracao_global", "configurar", profile == "SUPERADMIN", profile == "SUPERADMIN" ? "Acesso global." : "Ação exclusiva do SuperAdmin."));
        rows.Add(new AccessMatrixRowViewModel("financeiro", "pagamento", "estornar", profile == "SUPERADMIN", profile == "SUPERADMIN" ? "Acesso global auditado." : "Exige permissão específica e segregação de função."));
        rows.Add(new AccessMatrixRowViewModel("saude", "dado_sensivel", "visualizar", profile == "SUPERADMIN", profile == "SUPERADMIN" ? "Acesso crítico auditado." : "Dado sensível protegido pela LGPD."));
        return rows;
    }

    private string CurrentProfile() => Profiles.FirstOrDefault(profile => User.IsInRole(profile) || User.HasClaim("perfil", profile)) ?? "LEITURA";
    private bool IsSuperAdmin() => User.IsInRole("SUPERADMIN") || User.IsInRole("ADMIN_GERAL") || User.HasClaim("perfil", "SUPERADMIN");
    private async Task AuditAsync(string action, string profile, string reason, CancellationToken ct) =>
        await _audit.RegistrarAsync(ClaimLong("tenant_id"), ClaimLong("usuario_id"), action, "matriz_acesso", profile, null,
            new { perfil = profile, motivo = reason }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
    private long? ClaimLong(string type) => long.TryParse(User.FindFirst(type)?.Value, out var value) ? value : null;
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}
