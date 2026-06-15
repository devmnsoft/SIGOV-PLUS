using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.PostBuild;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class PlaceholderController : Controller
{
    private readonly ILogger<PlaceholderController> _logger;

    public PlaceholderController(ILogger<PlaceholderController> logger) => _logger = logger;

    [HttpGet("/Placeholder/{modulo}")]
    [HttpGet("/Implantacao/{modulo}")]
    public IActionResult Modulo(string modulo)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modulo))
            {
                _logger.LogWarning("Placeholder solicitado sem módulo. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
                return BadRequest("Módulo inválido.");
            }

            var titulo = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(modulo.Replace('-', ' ').Replace('_', ' '));
            _logger.LogInformation("Navegação para módulo em implantação {Modulo}. CorrelationId={CorrelationId}", modulo, HttpContext.TraceIdentifier);
            return View("ModuloEmPreparacao", new ImplementationModuleViewModel
            {
                Codigo = modulo,
                Titulo = titulo,
                Descricao = "Este módulo já está previsto no roadmap do SIGOV PLUS e será liberado com CRUD, permissões, auditoria e integrações no padrão SaaS.",
                ProximosPassos = new[] { "Validar regras de negócio com o cliente piloto", "Conectar repositórios e endpoints reais", "Publicar CRUD padronizado com trilha de auditoria", "Habilitar permissões por perfil e tenant" },
                ModulosRelacionados = new[] { "Dashboard", "Auditoria", "Parâmetros", "Implantação guiada" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado no placeholder {Modulo}. CorrelationId={CorrelationId}", modulo, HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, "Não foi possível abrir o módulo em preparação.");
        }
    }
}
