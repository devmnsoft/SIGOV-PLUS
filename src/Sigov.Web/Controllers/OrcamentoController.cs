using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

/// <summary>
/// Entradas MVC da Estação de Orçamento e Responsabilidade Fiscal (RC-EVO Ciclo 2 Sprint 13).
/// As telas consomem exclusivamente os endpoints tipados <c>/api/orcamento/*</c>
/// (motor multi-esfera Lei 4.320/64 + LC 101/2000); nada mockado, sem catálogo
/// demonstrativo (regras 12/13). Rotas estáveis /Orcamento/* usadas pelo menu lateral.
/// </summary>
public sealed class OrcamentoController : Controller
{
    public IActionResult Index() => View();
    [HttpGet("/Orcamento/Estrutura")] public IActionResult Estrutura() => View();
    [HttpGet("/Orcamento/Dotacoes")] public IActionResult Dotacoes() => View();
    [HttpGet("/Orcamento/Empenhos")] public IActionResult Empenhos() => View();
    [HttpGet("/Orcamento/Liquidacoes")] public IActionResult Liquidacoes() => View();
    [HttpGet("/Orcamento/Receitas")] public IActionResult Receitas() => View();
    [HttpGet("/Orcamento/Lrf")] public IActionResult Lrf() => View();
    [HttpGet("/Orcamento/Previsao")] public IActionResult Previsao() => View();
    [HttpGet("/Orcamento/Rreo")] public IActionResult Rreo() => View();
    [HttpGet("/Orcamento/Exercicios")] public IActionResult Exercicios() => View();
    [HttpGet("/Orcamento/Parametros")] public IActionResult Parametros() => View();
}
