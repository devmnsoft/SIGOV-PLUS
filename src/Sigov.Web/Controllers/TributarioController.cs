using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Tributario;

namespace Sigov.Web.Controllers;

public sealed class TributarioController : Controller
{
    public IActionResult Dashboard() => View(new TributarioDashboardViewModel());

    public IActionResult Iptu() => View();
    public IActionResult Iss() => View();
    public IActionResult Taxas() => View();
    public IActionResult Parcelamentos() => View();
    public IActionResult Arrecadacao() => View();
    public IActionResult LivroEletronico() => View();
    public IActionResult RelatoriosFiscais() => View();
    public IActionResult Nfse() => View();
    public IActionResult Configuracao() => View();
    public IActionResult TiposCadastro() => View();
    public IActionResult CamposDinamicos() => View();
    public IActionResult Imoveis() => View();
    public IActionResult Economicos() => View();
    public IActionResult Contribuintes() => View(new ContribuinteFormViewModel());
    public IActionResult ContribuinteCriar() => View(new ContribuinteFormViewModel());
    public IActionResult ContribuinteEditar(long id) => View(new ContribuinteFormViewModel { Id = id });
    public IActionResult ContribuinteDetalhe(long id) => View(id);
    public IActionResult CadastroImobiliario() => View(new CadastroImobiliarioFormViewModel());
    public IActionResult CadastroMercantil() => View(new CadastroMercantilFormViewModel());
    public IActionResult AtividadesEconomicas() => View(new AtividadeEconomicaFormViewModel());
    public IActionResult Lancamentos() => View(new LancamentoTributarioFormViewModel());
    public IActionResult LancamentoCriar() => View(new LancamentoTributarioFormViewModel());
    public IActionResult LancamentoDetalhe(long id) => View(id);
    public IActionResult Parcelas() => View();
    public IActionResult DamBoletos() => View();
    public IActionResult PixPagamentos() => View();
    public IActionResult Certidoes() => View(new CertidaoFormViewModel());
    public IActionResult DividaAtiva() => View(new DividaAtivaFormViewModel());
    public IActionResult Carnes() => View(new CarneFormViewModel());
    public IActionResult CarneDetalhe(long id) => View(id);
}
