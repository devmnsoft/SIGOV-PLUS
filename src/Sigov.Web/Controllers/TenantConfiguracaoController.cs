using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Saas.WhiteLabel;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class TenantConfiguracaoController : Controller
{
    private const long MaxLogoBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> AllowedLogoContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp" };
    private static readonly HashSet<string> AllowedLogoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    private readonly ITenantBrandingService _brandingService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TenantConfiguracaoController> _logger;

    public TenantConfiguracaoController(ITenantBrandingService brandingService, ICurrentTenant currentTenant, ICurrentUser currentUser, IWebHostEnvironment environment, ILogger<TenantConfiguracaoController> logger)
    {
        _brandingService = brandingService;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _environment = environment;
        _logger = logger;
    }

    public IActionResult MinhaAssinatura() => View();
    public IActionResult MeusModulos() => View();
    public IActionResult Dominios() => View();

    [HttpGet]
    public async Task<IActionResult> Branding(CancellationToken cancellationToken)
    {
        var context = TryResolveContext();
        if (context is null)
        {
            TempData["ErrorMessage"] = "Selecione um tenant operacional antes de alterar o branding.";
            return View(TenantBrandingFormViewModel.Empty());
        }

        var branding = await _brandingService.GetAsync(context.TenantId, cancellationToken).ConfigureAwait(false);
        return View(TenantBrandingFormViewModel.FromResponse(branding));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxLogoBytes + 65536)]
    public async Task<IActionResult> Branding(TenantBrandingFormViewModel model, CancellationToken cancellationToken)
    {
        var context = TryResolveContext();
        if (context is null)
            ModelState.AddModelError(string.Empty, "Selecione um tenant operacional antes de alterar o branding.");

        if (model.LogoArquivo is { Length: > 0 } logoArquivo)
            ValidateLogoArquivo(logoArquivo);

        if (!ModelState.IsValid)
            return View(model);

        string? logoUrl = NormalizeBlank(model.LogoUrl);
        string? logoStorageKey = null;
        string? logoOriginalFilename = null;
        string? logoContentType = null;
        long? logoSizeBytes = null;

        if (model.LogoArquivo is { Length: > 0 } logo)
        {
            var savedLogo = await SaveLogoAsync(context!.TenantId, logo, cancellationToken).ConfigureAwait(false);
            logoUrl = savedLogo.Url;
            logoStorageKey = savedLogo.StorageKey;
            logoOriginalFilename = savedLogo.OriginalFileName;
            logoContentType = savedLogo.ContentType;
            logoSizeBytes = savedLogo.SizeBytes;
            model.LogoUrl = savedLogo.Url;
        }

        var request = new TenantBrandingUpdateRequest(
            model.NomeExibicao.Trim(),
            logoUrl,
            logoStorageKey,
            NormalizeBlank(model.CorPrimaria),
            NormalizeBlank(model.CorSecundaria),
            NormalizeBlank(model.CorAcento),
            model.Tema,
            NormalizeBlank(model.FaviconUrl),
            NormalizeBlank(model.CssCustomizado),
            model.WhiteLabelAtivo,
            model.LogoWidthPx,
            model.LogoHeightPx,
            model.LogoFit,
            logoOriginalFilename,
            logoContentType,
            logoSizeBytes);

        var result = await _brandingService.UpdateAsync(context!.TenantId, request, context.UsuarioId, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Branding invalido.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Branding atualizado com sucesso. A nova identidade sera aplicada nas proximas telas carregadas.";
        _logger.LogInformation("Branding web do tenant {TenantId} atualizado com logo {LogoUrl}.", context.TenantId, logoUrl);
        return RedirectToAction(nameof(Branding));
    }

    private TenantRequestContext? TryResolveContext()
    {
        if (_currentTenant.TenantId is > 0 tenantId)
            return new TenantRequestContext(tenantId, _currentUser.UsuarioId ?? 0);

        return null;
    }

    private void ValidateLogoArquivo(IFormFile logo)
    {
        if (logo.Length > MaxLogoBytes)
            ModelState.AddModelError(nameof(TenantBrandingFormViewModel.LogoArquivo), "A logo deve ter no maximo 2 MB.");

        if (!AllowedLogoContentTypes.Contains(logo.ContentType))
            ModelState.AddModelError(nameof(TenantBrandingFormViewModel.LogoArquivo), "Envie a logo em PNG, JPG ou WebP.");

        var extension = Path.GetExtension(logo.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedLogoExtensions.Contains(extension))
            ModelState.AddModelError(nameof(TenantBrandingFormViewModel.LogoArquivo), "A extensao da logo deve ser .png, .jpg, .jpeg ou .webp.");
    }

    private async Task<SavedLogo> SaveLogoAsync(long tenantId, IFormFile logo, CancellationToken cancellationToken)
    {
        var extension = NormalizeLogoExtension(logo);
        var tenantSegment = tenantId.ToString(CultureInfo.InvariantCulture);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath) ? Path.Combine(_environment.ContentRootPath, "wwwroot") : _environment.WebRootPath;
        var relativeDirectory = Path.Combine("uploads", "tenant-branding", tenantSegment);
        var absoluteDirectory = Path.Combine(webRoot, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var absolutePath = Path.Combine(absoluteDirectory, fileName);
        await using var stream = System.IO.File.Create(absolutePath);
        await logo.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);

        var url = string.Join('/', string.Empty, "uploads", "tenant-branding", tenantSegment, fileName);
        var storageKey = string.Join('/', "tenant-branding", tenantSegment, fileName);
        return new SavedLogo(url, storageKey, Path.GetFileName(logo.FileName), logo.ContentType, logo.Length);
    }

    private static string NormalizeLogoExtension(IFormFile logo)
    {
        var extension = Path.GetExtension(logo.FileName).ToLowerInvariant();
        if (AllowedLogoExtensions.Contains(extension))
            return extension;

        return logo.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".png";
    }

    private static string? NormalizeBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record TenantRequestContext(long TenantId, long UsuarioId);
    private sealed record SavedLogo(string Url, string StorageKey, string OriginalFileName, string ContentType, long SizeBytes);
}

public sealed class TenantBrandingFormViewModel
{
    [Required(ErrorMessage = "Informe o nome de exibicao.")]
    [StringLength(150, ErrorMessage = "O nome de exibicao deve ter no maximo 150 caracteres.")]
    public string NomeExibicao { get; set; } = "sigov";

    [StringLength(500, ErrorMessage = "A URL da logo deve ter no maximo 500 caracteres.")]
    public string? LogoUrl { get; set; }

    public IFormFile? LogoArquivo { get; set; }

    [Range(80, 480, ErrorMessage = "A largura da logo deve ficar entre 80 e 480 px.")]
    public int LogoWidthPx { get; set; } = 220;

    [Range(32, 180, ErrorMessage = "A altura da logo deve ficar entre 32 e 180 px.")]
    public int LogoHeightPx { get; set; } = 72;

    [RegularExpression("^(contain|cover|fill)$", ErrorMessage = "Selecione contain, cover ou fill.")]
    public string LogoFit { get; set; } = "contain";

    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "A cor primaria deve estar no formato #RRGGBB.")]
    public string CorPrimaria { get; set; } = "#0d6efd";

    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "A cor secundaria deve estar no formato #RRGGBB.")]
    public string CorSecundaria { get; set; } = "#6c757d";

    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "A cor de destaque deve estar no formato #RRGGBB.")]
    public string CorAcento { get; set; } = "#198754";

    [Required(ErrorMessage = "Selecione o tema.")]
    public string Tema { get; set; } = "SIGOV";

    [StringLength(500, ErrorMessage = "A URL do favicon deve ter no maximo 500 caracteres.")]
    public string? FaviconUrl { get; set; }

    [StringLength(4000, ErrorMessage = "O CSS customizado deve ter no maximo 4000 caracteres.")]
    public string? CssCustomizado { get; set; }

    public bool WhiteLabelAtivo { get; set; }
    public bool PlanoPermiteWhiteLabel { get; set; }

    public static TenantBrandingFormViewModel Empty() => new();

    public static TenantBrandingFormViewModel FromResponse(TenantBrandingResponse response) => new()
    {
        NomeExibicao = response.NomeExibicao,
        LogoUrl = response.LogoUrl,
        LogoWidthPx = response.LogoWidthPx,
        LogoHeightPx = response.LogoHeightPx,
        LogoFit = response.LogoFit,
        CorPrimaria = response.CorPrimaria ?? "#0d6efd",
        CorSecundaria = response.CorSecundaria ?? "#6c757d",
        CorAcento = response.CorAcento ?? "#198754",
        Tema = response.Tema,
        FaviconUrl = response.FaviconUrl,
        CssCustomizado = response.CssCustomizado,
        WhiteLabelAtivo = response.WhiteLabelAtivo,
        PlanoPermiteWhiteLabel = response.PlanoPermiteWhiteLabel
    };
}
