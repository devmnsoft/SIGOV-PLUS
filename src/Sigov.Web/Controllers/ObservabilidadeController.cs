using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Infrastructure.Health;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ObservabilidadeController : Controller
{
    private readonly ProjectStatusProvider _provider;
    public ObservabilidadeController(ProjectStatusProvider provider) => _provider = provider;

    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) => await Render(cancellationToken).ConfigureAwait(false);
    public async Task<IActionResult> Migrations(CancellationToken cancellationToken) => await Render(cancellationToken).ConfigureAwait(false);
    public async Task<IActionResult> Modulos(CancellationToken cancellationToken) => await Render(cancellationToken).ConfigureAwait(false);
    public async Task<IActionResult> Rotas(CancellationToken cancellationToken) => await Render(cancellationToken).ConfigureAwait(false);
    public async Task<IActionResult> Validadores(CancellationToken cancellationToken) => await Render(cancellationToken).ConfigureAwait(false);

    private async Task<IActionResult> Render(CancellationToken cancellationToken) =>
        View("Dashboard", await _provider.GetAsync(cancellationToken).ConfigureAwait(false));
}
