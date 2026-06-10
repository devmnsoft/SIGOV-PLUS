using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Financeiro;

namespace Sigov.Web.Controllers;

public sealed class FinanceiroController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Dashboard() => View(new FinanceiroDashboardViewModel());
    public IActionResult PlanoContas() => View(new PlanoContasFormViewModel());
    public IActionResult FontesRecurso() => View(new FonteRecursoFormViewModel());
    public IActionResult Programas() => View(new ProgramaFormViewModel());
    public IActionResult Acoes() => View(new AcaoFormViewModel());
    public IActionResult NaturezasDespesa() => View(new NaturezaDespesaFormViewModel());
    public IActionResult NaturezasReceita() => View(new NaturezaReceitaFormViewModel());
    public IActionResult OrcamentoDespesa() => View(new OrcamentoDespesaFormViewModel());
    public IActionResult OrcamentoReceita() => View(new OrcamentoReceitaFormViewModel());
    public IActionResult Empenhos() => View();
    public IActionResult EmpenhoCriar() => View(new EmpenhoFormViewModel());
    public IActionResult EmpenhoEditar(long id) => View(new EmpenhoFormViewModel { Id = id });
    public IActionResult EmpenhoDetalhe(long id) => View(id);
    public IActionResult Liquidacoes() => View(new LiquidacaoFormViewModel());
    public IActionResult Pagamentos() => View(new PagamentoFormViewModel());
    public IActionResult Receitas() => View(new ReceitaLancamentoFormViewModel());
    public IActionResult ContasReceber() => View();
    public IActionResult CentrosCusto() => View("FinanceiroEmpresarial", "Centros de Custo");
    public IActionResult Naturezas() => View("FinanceiroEmpresarial", "Naturezas Financeiras");
    public IActionResult ContasBancarias() => View("FinanceiroEmpresarial", "Contas Bancárias");
    public IActionResult FormasPagamento() => View("FinanceiroEmpresarial", "Formas de Pagamento");
    public IActionResult ContasPagar() => View("FinanceiroEmpresarial", "Contas a Pagar");
    public IActionResult Movimentos() => View("FinanceiroEmpresarial", "Movimentos Financeiros");
    public IActionResult FluxoCaixa() => View("FinanceiroEmpresarial", "Fluxo de Caixa");
    public IActionResult Conciliacao() => View("FinanceiroEmpresarial", "Conciliação Bancária");
    public IActionResult Configuracao() => View("FinanceiroEmpresarial", "Configuração Financeira");
}
