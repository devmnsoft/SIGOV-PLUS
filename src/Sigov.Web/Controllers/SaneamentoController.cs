using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Saneamento;

using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class SaneamentoController : Controller
{
    private readonly OperationalDemoService _operationalDemo;
    private readonly ILogger<SaneamentoController> _operationalLogger;

    public SaneamentoController(OperationalDemoService operationalDemo, ILogger<SaneamentoController> operationalLogger)
    {
        _operationalDemo = operationalDemo;
        _operationalLogger = operationalLogger;
    }

    public IActionResult Dashboard() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Dashboard"));
    [Route("/Saneamento/Consumidores")]
    public IActionResult Consumidores(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Consumidores", q));
    public IActionResult ConsumidorDetalhe(long id) { ViewData["ConsumidorId"] = id; return View(); }
    [Route("/Saneamento/Ligacoes")]
    public IActionResult Ligacoes(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Ligacoes", q));
    public IActionResult UnidadesConsumidoras() => View(new UnidadeConsumidoraFormViewModel());
    public IActionResult UnidadeConsumidoraDetalhe(long id) { ViewData["UnidadeConsumidoraId"] = id; return View(); }
    [HttpGet("/Saneamento/Hidrometros")]
    public IActionResult Hidrometros() => View(new HidrometroFormViewModel());
    [HttpGet("/Saneamento/Hidrometros/Novo")]
    public IActionResult HidrometroNovo() => View("Hidrometros", new HidrometroFormViewModel());
    [HttpGet("/Saneamento/Hidrometros/{id:long}")]
    public IActionResult HidrometroDetalhe(long id) { ViewData["HidrometroId"] = id; return View("Hidrometros", new HidrometroFormViewModel()); }
    [Route("/Saneamento/Leituras")]
    public IActionResult Leituras(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Leituras", q));
    [Route("/Saneamento/Faturas")]
    public IActionResult Faturas(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Faturas", q));
    public IActionResult FaturaDetalhe(long id) { ViewData["FaturaId"] = id; return View(); }
    public IActionResult Arrecadacoes() => View(new ArrecadacaoSaneamentoFormViewModel());
    public IActionResult Parcelamentos() => View(new ParcelamentoSaneamentoFormViewModel());
    [Route("/Saneamento/OrdensServico")]
    public IActionResult OrdensServico(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "OrdensServico", q));
    public IActionResult OrdemServicoDetalhe(long id) { ViewData["OrdemServicoId"] = id; return View(); }
    public IActionResult EquipesCampo() => View(new EquipeCampoSaneamentoFormViewModel());
    public IActionResult Laboratorio() => View(new LaboratorioAmostraFormViewModel());
    public IActionResult Rede() => View(new RedeSaneamentoTrechoFormViewModel());


    [Route("/Saneamento")]
    public IActionResult Index(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Dashboard", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Saneamento/Index");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Em implantação"));
        }
    }

    [Route("/Saneamento/Gis")]
    public IActionResult Gis(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Gis", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Saneamento/Gis");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Em implantação"));
        }
    }

    [Route("/Saneamento/Consumidores/Novo")] public IActionResult ConsumidorNovo() => View(new SaneamentoConsumidorFormViewModel());
    [HttpPost("/Saneamento/Consumidores/Novo")][ValidateAntiForgeryToken] public IActionResult ConsumidorNovoPost(SaneamentoConsumidorFormViewModel model){ TempData["Warning"]="Cadastro real de consumidor depende da tabela sigov.saneamento_consumidor; nenhum salvamento foi simulado."; return RedirectToAction(nameof(Consumidores)); }
    [Route("/Saneamento/Consumidores/{id:long}")] public IActionResult ConsumidorDetalheRota(long id) => ConsumidorDetalhe(id);
    [Route("/Saneamento/Relatorios")] public IActionResult Relatorios(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saneamento", "Relatorios", q));
}
