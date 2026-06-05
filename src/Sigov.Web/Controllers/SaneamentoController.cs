using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Saneamento;

namespace Sigov.Web.Controllers;

public sealed class SaneamentoController : Controller
{
    public IActionResult Dashboard() => View(new SaneamentoDashboardViewModel());
    public IActionResult Consumidores() => View(new SaneamentoConsumidorFormViewModel());
    public IActionResult ConsumidorDetalhe(long id) { ViewData["ConsumidorId"] = id; return View(); }
    public IActionResult Ligacoes() => View(new LigacaoSaneamentoFormViewModel());
    public IActionResult UnidadesConsumidoras() => View(new UnidadeConsumidoraFormViewModel());
    public IActionResult UnidadeConsumidoraDetalhe(long id) { ViewData["UnidadeConsumidoraId"] = id; return View(); }
    public IActionResult Hidrometros() => View(new HidrometroFormViewModel());
    public IActionResult Leituras() => View(new LeituraConsumoFormViewModel());
    public IActionResult Faturas() => View(new FaturaSaneamentoFormViewModel());
    public IActionResult FaturaDetalhe(long id) { ViewData["FaturaId"] = id; return View(); }
    public IActionResult Arrecadacoes() => View(new ArrecadacaoSaneamentoFormViewModel());
    public IActionResult Parcelamentos() => View(new ParcelamentoSaneamentoFormViewModel());
    public IActionResult OrdensServico() => View(new OrdemServicoSaneamentoFormViewModel());
    public IActionResult OrdemServicoDetalhe(long id) { ViewData["OrdemServicoId"] = id; return View(); }
    public IActionResult EquipesCampo() => View(new EquipeCampoSaneamentoFormViewModel());
    public IActionResult Laboratorio() => View(new LaboratorioAmostraFormViewModel());
    public IActionResult Rede() => View(new RedeSaneamentoTrechoFormViewModel());
}
