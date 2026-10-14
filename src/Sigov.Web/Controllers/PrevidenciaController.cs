using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

/// <summary>
/// Entradas MVC da Estação de Folha e Previdência Pública (RC-EVO Ciclo 2 Sprint 12).
/// As telas consomem exclusivamente os endpoints tipados <c>/api/previdencia/*</c>
/// (motor determinístico multi-esfera); nada mockado, sem catálogo demonstrativo
/// (regras 12/13). Rotas estáveis /Previdencia/* usadas pelo menu lateral.
/// </summary>
public sealed class PrevidenciaController : Controller
{
    public IActionResult Index() => View();
    [HttpGet("/Previdencia/Cargos")] public IActionResult Cargos() => View();
    [HttpGet("/Previdencia/Servidores")] public IActionResult Servidores() => View();
    [HttpGet("/Previdencia/Folhas")] public IActionResult Folhas() => View();
    [HttpGet("/Previdencia/Ferias")] public IActionResult Ferias() => View();
    [HttpGet("/Previdencia/Afastamentos")] public IActionResult Afastamentos() => View();
    [HttpGet("/Previdencia/Contracheques")] public IActionResult Contracheques() => View();
    [HttpGet("/Previdencia/Esocial")] public IActionResult Esocial() => View();
    [HttpGet("/Previdencia/Parametros")] public IActionResult Parametros() => View();
}
