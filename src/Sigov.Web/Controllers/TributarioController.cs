using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Tributario;

using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

public sealed class TributarioController : Controller
{
    private readonly TributarioOperationalService _operationalDemo;
    private readonly ILogger<TributarioController> _operationalLogger;
    private readonly IAuditTrailService _auditTrail;

    public TributarioController(TributarioOperationalService operationalDemo, IAuditTrailService auditTrail, ILogger<TributarioController> operationalLogger)
    {
        _operationalDemo = operationalDemo;
        _auditTrail = auditTrail;
        _operationalLogger = operationalLogger;
    }

    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Dashboard", null, cancellationToken));

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
    public async Task<IActionResult> Imoveis(CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Imoveis", null, cancellationToken));
    public async Task<IActionResult> Economicos(CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Economicos", null, cancellationToken));
    [Route("/Tributario/Contribuintes")]
    public async Task<IActionResult> Contribuintes(CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Contribuintes", null, cancellationToken));
    public IActionResult ContribuinteCriar() => View(new ContribuinteFormViewModel());
    public IActionResult ContribuinteEditar(long id) => View(new ContribuinteFormViewModel { Id = id });
    [Route("/Tributario/Contribuintes/{id:long}")]
    public async Task<IActionResult> ContribuinteDetalhe(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", $"Contribuinte #{id}", null, cancellationToken));
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
    public async Task<IActionResult> DividaAtiva(CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "DividaAtiva", null, cancellationToken));
    public IActionResult Carnes() => View(new CarneFormViewModel());
    public IActionResult CarneDetalhe(long id) => View(id);


    [Route("/Tributario")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Dashboard", q, cancellationToken));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Tributario/Index");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Em implantação", null, cancellationToken));
        }
    }

    [Route("/Tributario/Debitos")]
    public async Task<IActionResult> Debitos(string? q = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Debitos", q, cancellationToken));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Tributario/Debitos");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Em implantação", null, cancellationToken));
        }
    }

    [Route("/Tributario/Relatorios")]
    public async Task<IActionResult> Relatorios(string? q = null, CancellationToken cancellationToken = default) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Relatorios", q, cancellationToken));
    [Route("/Tributario/Contribuintes/Novo")]
    public async Task<IActionResult> NovoContribuinte(CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Novo contribuinte", null, cancellationToken));
    [HttpPost, ValidateAntiForgeryToken, Route("/Tributario/Contribuintes/Novo")]
    public async Task<IActionResult> NovoContribuintePost(CancellationToken cancellationToken) { await Audit("tributario.contribuinte.criar", null, cancellationToken); TempData["Warning"] = "Contribuinte não salvo sem tabela sigov.contribuinte homologada."; return Redirect("/Tributario/Contribuintes"); }
    [Route("/Tributario/ContribuintesCsv")]
    public IActionResult ContribuintesCsv() => File(System.Text.Encoding.UTF8.GetBytes("codigo;nome;documento_mascarado;status\nTRI-001;Registro demonstrativo;***.123.456-**;Em implantação\n"), "text/csv", "contribuintes-mascarado.csv");

    [Route("/Tributario/Guias")]
    public async Task<IActionResult> Guias(string? q = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Guias", q, cancellationToken));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Tributario/Guias");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Tributario", "Em implantação", null, cancellationToken));
        }
    }
    private async Task Audit(string acao, string? id, CancellationToken ct) { try { await _auditTrail.RegistrarAsync(null, null, acao, "tributario", id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct); } catch (Exception ex) { _operationalLogger.LogWarning(ex, "Auditoria tributária falhou"); } }
}
