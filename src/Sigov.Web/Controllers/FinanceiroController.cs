using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Financeiro;

using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class FinanceiroController : Controller
{
    private readonly FinanceiroOperationalService _operationalDemo;
    private readonly ILogger<FinanceiroController> _operationalLogger;

    public FinanceiroController(FinanceiroOperationalService operationalDemo, ILogger<FinanceiroController> operationalLogger)
    {
        _operationalDemo = operationalDemo;
        _operationalLogger = operationalLogger;
    }

    [Route("/Financeiro")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Dashboard", q, cancellationToken));
    public IActionResult Dashboard() => View(new FinanceiroDashboardViewModel());
    [Route("/Financeiro/Exercicios")] public async Task<IActionResult> Exercicios(string? q, CancellationToken ct) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Exercícios", q, ct));
    [Route("/Financeiro/UnidadesOrcamentarias")] public async Task<IActionResult> UnidadesOrcamentarias(string? q, CancellationToken ct) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Unidades orçamentárias", q, ct));
    [Route("/Financeiro/Dotacoes")] public IActionResult Dotacoes() => RedirectToAction(nameof(OrcamentoDespesa));
    [Route("/Financeiro/FontesRecursos")] public IActionResult FontesRecursos() => RedirectToAction(nameof(FontesRecurso));
    [Route("/Financeiro/OrdensPagamento")] public async Task<IActionResult> OrdensPagamento(string? q, CancellationToken ct) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Ordens de pagamento", q, ct));
    [Route("/Financeiro/Arrecadacao")] public async Task<IActionResult> Arrecadacao(string? q, CancellationToken ct) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Arrecadação", q, ct));
    [Route("/Financeiro/RestosPagar")] public async Task<IActionResult> RestosPagar(string? q, CancellationToken ct) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Restos a pagar", q, ct));
    [Route("/Financeiro/Suprimentos")] public async Task<IActionResult> Suprimentos(string? q, CancellationToken ct) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Suprimentos e adiantamentos", q, ct));
    public IActionResult PlanoContas() => View(new PlanoContasFormViewModel());
    public IActionResult FontesRecurso() => View(new FonteRecursoFormViewModel());
    public IActionResult Programas() => View(new ProgramaFormViewModel());
    public IActionResult Acoes() => View(new AcaoFormViewModel());
    public IActionResult NaturezasDespesa() => View(new NaturezaDespesaFormViewModel());
    public IActionResult NaturezasReceita() => View(new NaturezaReceitaFormViewModel());
    [Route("/Financeiro/Orcamento")]
    public IActionResult Orcamento() => View(nameof(OrcamentoDespesa), new OrcamentoDespesaFormViewModel());
    public IActionResult OrcamentoDespesa() => View(new OrcamentoDespesaFormViewModel());
    public IActionResult OrcamentoReceita() => View(new OrcamentoReceitaFormViewModel());
    public IActionResult Empenhos() => View();
    public IActionResult EmpenhoCriar() => View(new EmpenhoFormViewModel());
    public IActionResult EmpenhoEditar(long id) => View(new EmpenhoFormViewModel { Id = id });
    public IActionResult EmpenhoDetalhe(long id) => View(id);
    public IActionResult Liquidacoes() => View(new LiquidacaoFormViewModel());
    public IActionResult Pagamentos() => View(new PagamentoFormViewModel());
    public IActionResult Receitas() => View(new ReceitaLancamentoFormViewModel());
    [Route("/Financeiro/ContasReceber")]
    public async Task<IActionResult> ContasReceber(string? q = null, CancellationToken cancellationToken = default) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "ContasReceber", q, cancellationToken));
    public IActionResult CentrosCusto() => View("FinanceiroEmpresarial", "Centros de Custo");
    public IActionResult Naturezas() => View("FinanceiroEmpresarial", "Naturezas Financeiras");
    public IActionResult ContasBancarias() => View("FinanceiroEmpresarial", "Contas Bancárias");
    public IActionResult FormasPagamento() => View("FinanceiroEmpresarial", "Formas de Pagamento");
    [Route("/Financeiro/ContasPagar")]
    public async Task<IActionResult> ContasPagar(string? q = null, CancellationToken cancellationToken = default) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "ContasPagar", q, cancellationToken));
    public IActionResult Movimentos() => View("FinanceiroEmpresarial", "Movimentos Financeiros");
    public IActionResult FluxoCaixa() => View("FinanceiroEmpresarial", "Fluxo de Caixa");
    public IActionResult Conciliacao() => View("FinanceiroEmpresarial", "Conciliação Bancária");
    public IActionResult Configuracao() => View("FinanceiroEmpresarial", "Configuração Financeira");


    [Route("/Financeiro/Caixa")]
    public async Task<IActionResult> Caixa(string? q = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Caixa", q, cancellationToken));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Financeiro/Caixa");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Em implantação", null, cancellationToken));
        }
    }

    [Route("/Financeiro/Categorias")]
    public async Task<IActionResult> Categorias(string? q = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Categorias", q, cancellationToken));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Financeiro/Categorias");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Em implantação", null, cancellationToken));
        }
    }

    [Route("/Financeiro/Relatorios")]
    public async Task<IActionResult> Relatorios(string? q = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Relatorios", q, cancellationToken));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Financeiro/Relatorios");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Financeiro", "Em implantação", null, cancellationToken));
        }
    }
}
