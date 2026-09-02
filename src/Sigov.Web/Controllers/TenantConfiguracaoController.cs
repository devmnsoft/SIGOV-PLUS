using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Saas.WhiteLabel;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class TenantConfiguracaoController(ITenantBrandingService brandingService, ICurrentTenant currentTenant, ICurrentUser currentUser, IWebHostEnvironment environment) : Controller
{
    private const long MaxLogoBytes = 2 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp" };

    public IActionResult MinhaAssinatura() => View();
    public IActionResult MeusModulos() => View();
    [HttpGet]
    public async Task<IActionResult> Branding(CancellationToken cancellationToken) => View(await brandingService.GetAsync(RequiredTenant(), cancellationToken));

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(MaxLogoBytes + 64 * 1024)]
    public async Task<IActionResult> Branding(TenantBrandingUpdateRequest input, IFormFile? logo, CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant();
        var current = await brandingService.GetAsync(tenantId, cancellationToken);
        string? newPath = null;
        try
        {
            if (logo is not null && logo.Length > 0)
            {
                var extension = Path.GetExtension(Path.GetFileName(logo.FileName)).ToLowerInvariant();
                if (logo.Length > MaxLogoBytes || !AllowedTypes.TryGetValue(extension, out var expectedType) || !string.Equals(logo.ContentType, expectedType, StringComparison.OrdinalIgnoreCase) || !await HasValidSignatureAsync(logo, extension, cancellationToken))
                {
                    ModelState.AddModelError(nameof(logo), "Envie uma imagem PNG, JPG/JPEG ou WEBP válida, com no máximo 2 MB.");
                }
                else
                {
                    var relativeDirectory = Path.Combine("uploads", "tenant-branding", tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    var directory = Path.Combine(environment.WebRootPath, relativeDirectory);
                    Directory.CreateDirectory(directory);
                    var safeName = $"logo-{Guid.NewGuid():N}{extension}";
                    newPath = Path.Combine(directory, safeName);
                    await using var output = System.IO.File.Create(newPath);
                    await logo.CopyToAsync(output, cancellationToken);
                    input = input with { LogoUrl = "/" + Path.Combine(relativeDirectory, safeName).Replace('\\', '/'), LogoStorageKey = Path.Combine(relativeDirectory, safeName).Replace('\\', '/'), LogoOriginalFilename = Path.GetFileName(logo.FileName), LogoContentType = expectedType, LogoSizeBytes = logo.Length, LogoUploadedAt = DateTimeOffset.UtcNow };
                }
            }
            else input = input with { LogoUrl = current.LogoUrl };

            if (!ModelState.IsValid) return View(current);
            var result = await brandingService.UpdateAsync(tenantId, input, currentUser.UsuarioId ?? throw new InvalidOperationException("Usuário autenticado obrigatório."), cancellationToken);
            if (result.IsFailure) { ModelState.AddModelError(string.Empty, result.Error ?? "Não foi possível salvar o branding."); return View(current); }
            TempData["Success"] = "Identidade visual e logo atualizadas com sucesso.";
            return RedirectToAction(nameof(Branding));
        }
        catch { if (newPath is not null) System.IO.File.Delete(newPath); throw; }
    }

    public IActionResult Dominios() => View();
    private long RequiredTenant() => currentTenant.TenantId ?? throw new InvalidOperationException("Contexto de tenant obrigatório.");
    private static async Task<bool> HasValidSignatureAsync(IFormFile file, string extension, CancellationToken ct)
    {
        var header = new byte[12]; await using var stream = file.OpenReadStream(); var read = await stream.ReadAsync(header, ct);
        return extension switch { ".png" => read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}), ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff, ".webp" => read >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP", _ => false };
    }
}
