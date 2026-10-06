using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.Modules;
using Sigov.Web.Helpers;
using Sigov.Web.Models.Security;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("Seguranca/MatrizAcesso")]
public sealed class MatrizAcessoController : Controller
{
    private readonly IAuditTrailService _audit;
    private readonly IUserPermissionService _permissions;
    private readonly SegurancaAdminService _seguranca;

    public MatrizAcessoController(IAuditTrailService audit, IUserPermissionService permissions, SegurancaAdminService seguranca)
    {
        _audit = audit;
        _permissions = permissions;
        _seguranca = seguranca;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? perfil, CancellationToken ct) => View(await BuildAsync(perfil, ct).ConfigureAwait(false));

    [HttpGet("Exportar")]
    public async Task<IActionResult> Exportar([FromQuery] string? perfil, CancellationToken ct)
    {
        var model = await BuildAsync(perfil, ct).ConfigureAwait(false);
        if (!model.CanExport)
        {
            await AuditAsync("EXPORTACAO_NEGADA", model.Profile, "permissao_exportar_ausente", ct).ConfigureAwait(false);
            return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para exportar a matriz de acesso.");
        }

        var csv = new StringBuilder("modulo;recurso;acao;liberado;delegavel;motivo\n");
        foreach (var row in model.Rows)
            csv.Append(Csv(row.Module)).Append(';').Append(Csv(row.Resource)).Append(';').Append(Csv(row.Action)).Append(';')
                .Append(row.Allowed ? "sim" : "nao").Append(';').Append(row.Delegavel ? "sim" : "nao").Append(';').Append(Csv(row.Reason)).AppendLine();
        await AuditAsync("MATRIZ_ACESSO_EXPORTADA", model.Profile, "exportacao_autorizada", ct).ConfigureAwait(false);
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "matriz-acesso.csv");
    }

    // RC-SAAS-AUT (A7): matriz 100% DB-driven — perfis de sigov.perfil_acesso e permissões de
    // sigov.permissao/perfil_permissao (regra 12: sem catálogos hardcoded como autoridade).
    private async Task<AccessMatrixViewModel> BuildAsync(string? requestedProfile, CancellationToken ct)
    {
        var perfis = await _seguranca.ListarPerfisAsync(ct).ConfigureAwait(false);
        var perfilNames = perfis.Select(p => p.Nome).ToList();

        var selectedName = perfilNames.FirstOrDefault(n => n.Equals(requestedProfile, StringComparison.OrdinalIgnoreCase)) ?? CurrentProfile(perfilNames);

        var canExport = _permissions.HasPermission(User, "saas.plataforma.administrar") || _permissions.HasPermission(User, "seguranca.matriz.exportar");

        AccessMatrixViewModel result;
        if (selectedName is not null)
        {
            var perfilObj = perfis.First(p => p.Nome == selectedName);
            var permVm = await _seguranca.ObterPermissoesPerfilAsync(perfilObj.Id, ct).ConfigureAwait(false);
            var rows = permVm.Permissoes
                .Select(p => new AccessMatrixRowViewModel(p.Modulo, p.Recurso, p.Acao, p.Selecionada, p.Selecionada ? "Concedida ao perfil." : "Não concedida ao perfil."))
                .ToList();
            result = new AccessMatrixViewModel { Profile = selectedName, Profiles = perfilNames, Rows = rows, CanExport = canExport };
        }
        else
        {
            result = new AccessMatrixViewModel { Profile = "N/D", Profiles = perfilNames, Rows = Array.Empty<AccessMatrixRowViewModel>(), CanExport = canExport };
        }

        return result;
    }

    private string? CurrentProfile(IReadOnlyList<string> availableProfiles) =>
        availableProfiles.FirstOrDefault(profile => User.IsInRole(profile) || User.HasClaim("perfil", profile));

    private async Task AuditAsync(string action, string profile, string reason, CancellationToken ct) =>
        await _audit.RegistrarAsync(ClaimLong("tenant_id"), ClaimLong("usuario_id"), action, "matriz_acesso", profile, null,
            new { perfil = profile, motivo = reason }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
    private long? ClaimLong(string type) => long.TryParse(User.FindFirst(type)?.Value, out var value) ? value : null;
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}
