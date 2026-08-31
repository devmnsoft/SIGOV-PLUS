using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Rh;

namespace Sigov.Web.Controllers;

[Route("Folha")]
public sealed class FolhaController : Controller
{
    [HttpGet("")]
    [HttpGet("Dashboard")]
    public IActionResult Dashboard() => View();

    [HttpGet("Competencias")]
    public IActionResult Competencias() => Registro("folhas", "Competências da folha");
    [HttpGet("Eventos")]
    public IActionResult Eventos() => Registro("folha-eventos", "Eventos da folha");
    [HttpGet("Calculo")]
    public IActionResult Calculo() => Registro("folhas", "Cálculo e memória da folha");
    [HttpGet("ContraCheques")]
    public IActionResult ContraCheques() => Registro("holerites", "Contracheques liberados");
    [HttpGet("Fechamento")]
    public IActionResult Fechamento() => Registro("folhas", "Fechamento e reabertura");
    [HttpGet("EmpenhoIntegracao")]
    public IActionResult EmpenhoIntegracao() => Registro("integracoes-financeiras", "Integração financeira autorizada");
    [HttpGet("Relatorios")]
    public IActionResult Relatorios() => Registro("folhas", "Relatórios da folha");

    private ViewResult Registro(string recurso, string titulo) => View("Registro", new RhRegistroViewModel(recurso, titulo));
}
