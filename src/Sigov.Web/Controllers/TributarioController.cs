using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Tributario;

using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class TributarioController : Controller
{
    private readonly OperationalDemoService _operationalDemo;
    private readonly ILogger<TributarioController> _operationalLogger;
    private readonly IAuditTrailService _auditTrail;

    public TributarioController(OperationalDemoService operationalDemo, IAuditTrailService auditTrail, ILogger<TributarioController> operationalLogger)
    {
        _operationalDemo = operationalDemo;
        _auditTrail = auditTrail;
        _operationalLogger = operationalLogger;
    }

    public IActionResult Dashboard() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Dashboard"));

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
    [Route("/Tributario/Imoveis")]
    public IActionResult Imoveis() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Imoveis"));
    public IActionResult Economicos() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Economicos"));
    [Route("/Tributario/Contribuintes")]
    public IActionResult Contribuintes() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Contribuintes"));
    public IActionResult ContribuinteCriar() => View(new ContribuinteFormViewModel());
    public IActionResult ContribuinteEditar(long id) => View(new ContribuinteFormViewModel { Id = id });
    [Route("/Tributario/Contribuintes/{id:long}")]
    public IActionResult ContribuinteDetalhe(long id) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", $"Contribuinte #{id}"));
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
    public IActionResult DividaAtiva() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "DividaAtiva"));
    public IActionResult Carnes() => View(new CarneFormViewModel());
    public IActionResult CarneDetalhe(long id) => View(id);


    [Route("/Tributario")]
    public IActionResult Index(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Dashboard", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Tributario/Index");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Em implantação"));
        }
    }

    [Route("/Tributario/Debitos")]
    public IActionResult Debitos(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Debitos", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Tributario/Debitos");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Em implantação"));
        }
    }

    [Route("/Tributario/Relatorios")]
    public IActionResult Relatorios(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Relatorios", q));
    [Route("/Tributario/Contribuintes/Novo")]
    public IActionResult NovoContribuinte() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Novo contribuinte"));
    [HttpPost, ValidateAntiForgeryToken, Route("/Tributario/Contribuintes/Novo")]
    public async Task<IActionResult> NovoContribuintePost(CancellationToken cancellationToken) { await Audit("tributario.contribuinte.criar", null, cancellationToken); TempData["Warning"] = "Contribuinte não salvo sem tabela sigov.contribuinte homologada."; return Redirect("/Tributario/Contribuintes"); }
    [Route("/Tributario/ContribuintesCsv")]
    public IActionResult ContribuintesCsv() => File(System.Text.Encoding.UTF8.GetBytes("codigo;nome;documento_mascarado;status\nTRI-001;Registro demonstrativo;***.123.456-**;Em implantação\n"), "text/csv", "contribuintes-mascarado.csv");

    [Route("/Tributario/Guias")]
    public IActionResult Guias(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Guias", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Tributario/Guias");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Tributario", "Em implantação"));
        }
    }
    private async Task Audit(string acao, string? id, CancellationToken ct) { try { await _auditTrail.RegistrarAsync(null, null, acao, "tributario", id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct); } catch (Exception ex) { _operationalLogger.LogWarning(ex, "Auditoria tributária falhou"); } }
}
