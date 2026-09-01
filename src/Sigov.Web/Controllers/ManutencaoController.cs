using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

/// <summary>Entrada canônica da manutenção de ativos; reutiliza as ordens persistidas de Frotas360.</summary>
[Authorize]
public sealed class ManutencaoController : Controller
{
    [HttpGet("/Manutencao"), HttpGet("/Manutencao/OrdensServico"), HttpGet("/Manutencao/Corretivas"), HttpGet("/Manutencao/Atendimentos")]
    public IActionResult OrdensServico() => Redirect("/Frotas/OrdensServico");

    [HttpGet("/Manutencao/Preventivas")]
    public IActionResult Preventivas() => Redirect("/Frotas/Manutencoes");

    [HttpGet("/Manutencao/Relatorios")]
    public IActionResult Relatorios() => Redirect("/Ativos/Relatorios");
}
