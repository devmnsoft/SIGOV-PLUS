using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("FiltrosSalvos")]
public sealed class SavedFiltersController(SavedFilterService service, ICurrentTenant tenant, ICurrentUser user) : Controller
{
    [HttpPost("Salvar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string screen, string name, string? status, bool isDefault, CancellationToken ct)
    {
        try { await service.SaveAsync(TenantId(), UserId(), screen, name, status, isDefault, ct).ConfigureAwait(false); TempData["Success"] = "Filtro salvo para seu usuário neste contexto."; }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        return Redirect(ScreenUrl(screen, status));
    }

    [HttpPost("Renomear"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(string screen, string currentName, string newName, CancellationToken ct)
    {
        try { await service.RenameAsync(TenantId(), UserId(), screen, currentName, newName, ct).ConfigureAwait(false); TempData["Success"] = "Filtro renomeado."; }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException) { TempData["Error"] = ex.Message; }
        return Redirect(ScreenUrl(screen, null));
    }

    [HttpPost("Excluir"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string screen, string name, CancellationToken ct)
    {
        try { await service.DeleteAsync(TenantId(), UserId(), screen, name, ct).ConfigureAwait(false); TempData["Success"] = "Filtro excluído."; }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        return Redirect(ScreenUrl(screen, null));
    }

    private long TenantId() => tenant.TenantId is > 0 ? tenant.TenantId.Value : throw new UnauthorizedAccessException("Tenant obrigatório.");
    private long UserId() => user.UsuarioId is > 0 ? user.UsuarioId.Value : throw new UnauthorizedAccessException("Usuário obrigatório.");
    private static string ScreenUrl(string screen, string? status) => screen switch
    {
        "minha-central" => string.IsNullOrWhiteSpace(status) ? "/MinhaCentral" : $"/MinhaCentral?status={Uri.EscapeDataString(status)}",
        "distribuicao-materiais" => string.IsNullOrWhiteSpace(status) ? "/Almoxarifado/Requisicoes" : $"/Almoxarifado/Requisicoes?status={Uri.EscapeDataString(status)}",
        _ => throw new ArgumentException("Tela de filtro não permitida.")
    };
}
