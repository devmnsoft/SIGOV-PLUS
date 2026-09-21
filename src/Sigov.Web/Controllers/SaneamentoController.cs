using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sigov.Application.Saneamento;
using Sigov.Web.Models.Saneamento;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaneamentoController : Controller
{
    private readonly ISaneamentoConsumidorService _consumidores;
    private readonly ILigacaoSaneamentoService _ligacoes;
    private readonly IUnidadeConsumidoraService _unidades;
    private readonly IHidrometroService _hidrometros;
    private readonly ILeituraConsumoService _leituras;
    private readonly IFaturaSaneamentoService _faturas;
    private readonly IArrecadacaoSaneamentoService _arrecadacoes;
    private readonly IParcelamentoSaneamentoService _parcelamentos;
    private readonly IOrdemServicoSaneamentoService _ordens;
    private readonly ISaneamentoDashboardService _dashboard;
    private readonly ISaneamentoExportacaoService _exportacao;

    public SaneamentoController(
        ISaneamentoConsumidorService consumidores,
        ILigacaoSaneamentoService ligacoes,
        IUnidadeConsumidoraService unidades,
        IHidrometroService hidrometros,
        ILeituraConsumoService leituras,
        IFaturaSaneamentoService faturas,
        IArrecadacaoSaneamentoService arrecadacoes,
        IParcelamentoSaneamentoService parcelamentos,
        IOrdemServicoSaneamentoService ordens,
        ISaneamentoDashboardService dashboard,
        ISaneamentoExportacaoService exportacao)
    {
        _consumidores = consumidores;
        _ligacoes = ligacoes;
        _unidades = unidades;
        _hidrometros = hidrometros;
        _leituras = leituras;
        _faturas = faturas;
        _arrecadacoes = arrecadacoes;
        _parcelamentos = parcelamentos;
        _ordens = ordens;
        _dashboard = dashboard;
        _exportacao = exportacao;
    }

    // ──────────────────── Dashboard ────────────────────
    [Route("/Saneamento")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken ct = default)
    {
        return await Dashboard(ct);
    }

    public async Task<IActionResult> Dashboard(CancellationToken ct = default)
    {
        var result = await _dashboard.ObterAsync(ct);
        ViewBag.DashboardReal = result.IsSuccess ? result.Value : null;
        return View(new SaneamentoDashboardViewModel());
    }

    // ──────────────────── Consumidores ────────────────────
    [HttpGet("/Saneamento/Clientes")]
    [HttpGet("/Saneamento/Consumidores")]
    public async Task<IActionResult> Consumidores(int page = 1, string? termo = null, CancellationToken ct = default)
    {
        var filtro = new SaneamentoConsumidorFiltro(Page: page, Termo: termo);
        var result = await _consumidores.ListarAsync(filtro, ct);
        ViewBag.Consumidores = result.IsSuccess ? result.Value : null;
        return View(new SaneamentoConsumidorFormViewModel());
    }

    public async Task<IActionResult> ConsumidorDetalhe(long id, CancellationToken ct = default)
    {
        var result = await _consumidores.ObterAsync(id, false, ct);
        ViewBag.Consumidor = result.IsSuccess ? result.Value : null;
        ViewData["ConsumidorId"] = id;
        if (result.IsFailure) TempData["ToastErro"] = result.Error;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Consumidores/Criar")]
    public async Task<IActionResult> ConsumidorCriar(SaneamentoConsumidorFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["ToastErro"] = "Verifique os campos obrigatórios."; return RedirectToAction(nameof(Consumidores)); }
        var r = new SaneamentoConsumidorCreateRequest(vm.PessoaId, vm.CodigoConsumidor, vm.TipoConsumidor, vm.Situacao);
        var result = await _consumidores.CriarAsync(r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Consumidor cadastrado.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Consumidores));
    }

    // ──────────────────── Ligações ────────────────────
    [Route("/Saneamento/Ligacoes")]
    public async Task<IActionResult> Ligacoes(int page = 1, CancellationToken ct = default)
    {
        var filtro = new SaneamentoFiltro(Page: page);
        var result = await _ligacoes.ListarAsync(filtro, ct);
        ViewBag.Ligacoes = result.IsSuccess ? result.Value : null;
        var consumResult = await _consumidores.ListarAsync(new SaneamentoConsumidorFiltro(PageSize: 100, Situacao: "ATIVO"), ct);
        ViewBag.ConsumidoresAtivos = consumResult.IsSuccess ? consumResult.Value : null;
        return View(new LigacaoSaneamentoFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Ligacoes/Criar")]
    public async Task<IActionResult> LigacaoCriar(LigacaoSaneamentoFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["ToastErro"] = "Verifique os campos obrigatórios."; return RedirectToAction(nameof(Ligacoes)); }
        var r = new LigacaoSaneamentoCreateRequest(vm.ConsumidorId, vm.NumeroLigacao, vm.TipoLigacao, vm.Situacao, vm.Categoria);
        var result = await _ligacoes.CriarAsync(r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Ligação cadastrada.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Ligacoes));
    }

    // ──────────────────── Unidades Consumidoras ────────────────────
    [HttpGet("/Saneamento/UnidadesConsumidoras")]
    public async Task<IActionResult> UnidadesConsumidoras(int page = 1, string? termo = null, CancellationToken ct = default)
    {
        var filtro = new UnidadeConsumidoraFiltro(Page: page, Termo: termo);
        var result = await _unidades.ListarAsync(filtro, ct);
        ViewBag.Unidades = result.IsSuccess ? result.Value : null;
        var consumResult = await _consumidores.ListarAsync(new SaneamentoConsumidorFiltro(PageSize: 100, Situacao: "ATIVO"), ct);
        ViewBag.ConsumidoresAtivos = consumResult.IsSuccess ? consumResult.Value : null;
        return View(new UnidadeConsumidoraFormViewModel());
    }

    public async Task<IActionResult> UnidadeConsumidoraDetalhe(long id, CancellationToken ct = default)
    {
        var result = await _unidades.ObterAsync(id, ct);
        ViewBag.Unidade = result.IsSuccess ? result.Value : null;
        ViewData["UnidadeConsumidoraId"] = id;
        if (result.IsFailure) TempData["ToastErro"] = result.Error;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/UnidadesConsumidoras/Criar")]
    public async Task<IActionResult> UnidadeConsumidoraCriar(UnidadeConsumidoraFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["ToastErro"] = "Verifique os campos obrigatórios."; return RedirectToAction(nameof(UnidadesConsumidoras)); }
        var r = new UnidadeConsumidoraCreateRequest(vm.ConsumidorId, vm.LigacaoId, vm.CodigoUnidade, vm.EnderecoJson, vm.Bairro, null, vm.Rota, null, vm.Latitude, vm.Longitude, vm.Situacao);
        var result = await _unidades.CriarAsync(r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Unidade consumidora cadastrada.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(UnidadesConsumidoras));
    }

    // ──────────────────── Hidrômetros ────────────────────
    [HttpGet("/Saneamento/Hidrometros")]
    public async Task<IActionResult> Hidrometros(int page = 1, CancellationToken ct = default)
    {
        var filtro = new SaneamentoFiltro(Page: page);
        var result = await _hidrometros.ListarAsync(filtro, ct);
        ViewBag.Hidrometros = result.IsSuccess ? result.Value : null;
        return View(new HidrometroFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Hidrometros/Criar")]
    public async Task<IActionResult> HidrometroCriar(HidrometroFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["ToastErro"] = "Verifique os campos obrigatórios."; return RedirectToAction(nameof(Hidrometros)); }
        var r = new HidrometroCreateRequest(vm.UnidadeConsumidoraId, vm.NumeroSerie, Situacao: vm.Situacao);
        var result = await _hidrometros.CriarAsync(r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Hidrômetro instalado.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Hidrometros));
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Hidrometros/{id:long}/Substituir")]
    public async Task<IActionResult> HidrometroSubstituir(long id, HidrometroFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["ToastErro"] = "Verifique os campos obrigatórios."; return RedirectToAction(nameof(Hidrometros)); }
        var r = new HidrometroCreateRequest(vm.UnidadeConsumidoraId, vm.NumeroSerie, Situacao: "INSTALADO");
        var result = await _hidrometros.SubstituirAsync(id, r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Hidrômetro substituído com sucesso.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Hidrometros));
    }

    [HttpGet("/Saneamento/Hidrometros/Novo")]
    public IActionResult HidrometroNovo() => View("Hidrometros", new HidrometroFormViewModel());
    [HttpGet("/Saneamento/Hidrometros/{id:long}")]
    public IActionResult HidrometroDetalhe(long id) { ViewData["HidrometroId"] = id; return View("Hidrometros", new HidrometroFormViewModel()); }

    // ──────────────────── Leituras ────────────────────
    [Route("/Saneamento/Leituras")]
    public async Task<IActionResult> Leituras(int page = 1, long? ucId = null, string? competencia = null, CancellationToken ct = default)
    {
        var filtro = new LeituraConsumoFiltro(Page: page, UnidadeConsumidoraId: ucId, Competencia: competencia);
        var result = await _leituras.ListarAsync(filtro, ct);
        ViewBag.Leituras = result.IsSuccess ? result.Value : null;
        return View(new LeituraConsumoFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Leituras/Criar")]
    public async Task<IActionResult> LeituraCriar(LeituraConsumoFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["ToastErro"] = "Verifique os campos obrigatórios."; return RedirectToAction(nameof(Leituras)); }
        var r = new LeituraConsumoCreateRequest(vm.UnidadeConsumidoraId, vm.HidrometroId, vm.Competencia, DateOnly.FromDateTime(DateTime.UtcNow), vm.LeituraAnterior, vm.LeituraAtual, TipoLeitura: vm.TipoLeitura);
        var result = await _leituras.CriarAsync(r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Leitura registrada na competência.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Leituras));
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Leituras/{id:long}/GerarFatura")]
    public async Task<IActionResult> LeituraGerarFatura(long id, GerarFaturaPorLeituraRequest req, CancellationToken ct)
    {
        var result = await _leituras.GerarFaturaAsync(id, req, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Fatura gerada a partir da leitura.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Faturas));
    }

    // ──────────────────── Faturas ────────────────────
    [HttpGet("/Saneamento/Faturamento")]
    [HttpGet("/Saneamento/Faturas")]
    public async Task<IActionResult> Faturas(int page = 1, string? status = null, string? competencia = null, CancellationToken ct = default)
    {
        var filtro = new FaturaSaneamentoFiltro(Page: page, Status: status, Competencia: competencia);
        var result = await _faturas.ListarAsync(filtro, ct);
        ViewBag.Faturas = result.IsSuccess ? result.Value : null;
        return View(new FaturaSaneamentoFormViewModel());
    }

    public async Task<IActionResult> FaturaDetalhe(long id, CancellationToken ct = default)
    {
        var result = await _faturas.ObterAsync(id, ct);
        ViewBag.Fatura = result.IsSuccess ? result.Value : null;
        ViewData["FaturaId"] = id;
        if (result.IsFailure) TempData["ToastErro"] = result.Error;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Faturas/{id:long}/Cancelar")]
    public async Task<IActionResult> FaturaCancelar(long id, string motivo, CancellationToken ct)
    {
        var result = await _faturas.CancelarAsync(id, new CancelarFaturaRequest(motivo), ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Fatura cancelada.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Faturas));
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Faturas/{id:long}/Pagamento")]
    public async Task<IActionResult> FaturaRegistrarPagamento(long id, decimal valorPago, CancellationToken ct)
    {
        var r = new RegistrarPagamentoDevRequest(valorPago);
        var result = await _faturas.RegistrarPagamentoDevAsync(id, r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Pagamento registrado.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Faturas));
    }

    // ──────────────────── Arrecadação ────────────────────
    [HttpGet("/Saneamento/Arrecadacao")]
    public async Task<IActionResult> Arrecadacoes(int page = 1, CancellationToken ct = default)
    {
        var filtro = new SaneamentoFiltro(Page: page);
        var result = await _arrecadacoes.ListarAsync(filtro, ct);
        ViewBag.Arrecadacoes = result.IsSuccess ? result.Value : null;
        return View(new ArrecadacaoSaneamentoFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Arrecadacao/Criar")]
    public async Task<IActionResult> ArrecadacaoCriar(ArrecadacaoSaneamentoFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["ToastErro"] = "Verifique os campos obrigatórios."; return RedirectToAction(nameof(Arrecadacoes)); }
        var r = new ArrecadacaoSaneamentoCreateRequest(vm.FaturaId, ValorPago: vm.ValorPago);
        var result = await _arrecadacoes.CriarAsync(r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Arrecadação registrada.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Arrecadacoes));
    }

    // ──────────────────── Parcelamentos ────────────────────
    public async Task<IActionResult> Parcelamentos(int page = 1, CancellationToken ct = default)
    {
        var filtro = new SaneamentoFiltro(Page: page);
        var result = await _parcelamentos.ListarAsync(filtro, ct);
        ViewBag.Parcelamentos = result.IsSuccess ? result.Value : null;
        return View(new ParcelamentoSaneamentoFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/Parcelamentos/Criar")]
    public async Task<IActionResult> ParcelamentoCriar(ParcelamentoSaneamentoFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["ToastErro"] = "Verifique os campos obrigatórios."; return RedirectToAction(nameof(Parcelamentos)); }
        var r = new ParcelamentoSaneamentoCreateRequest(vm.ConsumidorId, QuantidadeParcelas: vm.QuantidadeParcelas, ValorTotal: vm.ValorTotal);
        var result = await _parcelamentos.CriarAsync(r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Parcelamento registrado.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(Parcelamentos));
    }

    // ──────────────────── Ordens de Serviço ────────────────────
    [Route("/Saneamento/OrdensServico")]
    public async Task<IActionResult> OrdensServico(int page = 1, string? status = null, CancellationToken ct = default)
    {
        var filtro = new OrdemServicoSaneamentoFiltro(Page: page, Status: status);
        var result = await _ordens.ListarAsync(filtro, ct);
        ViewBag.Ordens = result.IsSuccess ? result.Value : null;
        return View(new OrdemServicoSaneamentoFormViewModel());
    }

    public async Task<IActionResult> OrdemServicoDetalhe(long id, CancellationToken ct = default)
    {
        var result = await _ordens.ObterAsync(id, ct);
        ViewBag.Ordem = result.IsSuccess ? result.Value : null;
        ViewData["OrdemServicoId"] = id;
        if (result.IsFailure) TempData["ToastErro"] = result.Error;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/OrdensServico/Criar")]
    public async Task<IActionResult> OrdemServicoCriar(OrdemServicoSaneamentoFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["ToastErro"] = "Verifique os campos obrigatórios."; return RedirectToAction(nameof(OrdensServico)); }
        var r = new OrdemServicoSaneamentoCreateRequest(vm.UnidadeConsumidoraId, vm.ConsumidorId, null, vm.TipoServico, vm.Prioridade, vm.Descricao);
        var result = await _ordens.CriarAsync(r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Ordem de serviço aberta.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(OrdensServico));
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/OrdensServico/{id:long}/Executar")]
    public async Task<IActionResult> OrdemServicoExecutar(long id, string solucao, CancellationToken ct)
    {
        var r = new ExecutarOrdemServicoRequest(solucao);
        var result = await _ordens.ExecutarAsync(id, r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Ordem de serviço encerrada.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(OrdensServico));
    }

    [HttpPost, ValidateAntiForgeryToken, Route("/Saneamento/OrdensServico/{id:long}/Cancelar")]
    public async Task<IActionResult> OrdemServicoCancelar(long id, string motivo, CancellationToken ct)
    {
        var r = new CancelarOrdemServicoRequest(motivo);
        var result = await _ordens.CancelarAsync(id, r, ct);
        if (result.IsSuccess) TempData["ToastOk"] = "Ordem de serviço cancelada.";
        else TempData["ToastErro"] = result.Error;
        return RedirectToAction(nameof(OrdensServico));
    }

    // ──────────────────── Equipes / Laboratório / Rede ────────────────────
    public IActionResult EquipesCampo() => View(new EquipeCampoSaneamentoFormViewModel());
    public IActionResult Laboratorio() => View(new LaboratorioAmostraFormViewModel());
    public IActionResult Rede() => View(new RedeSaneamentoTrechoFormViewModel());

    // ──────────────────── Relatórios / CSV ────────────────────
    [Route("/Saneamento/Relatorios")]
    public IActionResult Relatorios() => View();

    [HttpGet("/Saneamento/Exportar/{recurso}")]
    public async Task<IActionResult> ExportarCsv(string recurso, CancellationToken ct)
    {
        var result = await _exportacao.ExportarAsync(recurso, "csv", ct);
        if (result.IsFailure) { TempData["ToastErro"] = result.Error; return RedirectToAction(nameof(Relatorios)); }
        return File(result.Value!, "text/csv; charset=utf-8", $"saneamento_{recurso}_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ──────────────────── Telas avançadas / aliases ────────────────────
    [Route("/Saneamento/Gis")]
    public IActionResult Gis() => Tela("Georreferenciamento", "gis-qualidade", "gis");

    [Route("/Saneamento/Consumidores/Novo")] public IActionResult ConsumidorNovo() => View("Consumidores", new SaneamentoConsumidorFormViewModel());
    [Route("/Saneamento/Consumidores/{id:long}")] public IActionResult ConsumidorDetalheRota(long id) => ConsumidorDetalhe(id).GetAwaiter().GetResult();
    [HttpGet("/Saneamento/Atendimento")]
    public IActionResult Atendimento() => Tela("Atendimento comercial", "comercial", "atendimentos");

    [HttpGet("/Saneamento/Consumidores/Create"), HttpGet("/Saneamento/Consumidores/Edit")]
    public IActionResult ConsumidorForm() => View("Consumidores", new SaneamentoConsumidorFormViewModel());
    [HttpGet("/Saneamento/Consumidores/Details")] public IActionResult ConsumidorDetails(long id) => ConsumidorDetalhe(id).GetAwaiter().GetResult();

    [HttpGet("/Saneamento/Ligacoes/Create"), HttpGet("/Saneamento/Ligacoes/Edit"), HttpGet("/Saneamento/Ligacoes/Details"), HttpGet("/Saneamento/Ligacoes/Cortar"), HttpGet("/Saneamento/Ligacoes/Religar")]
    public IActionResult LigacaoFluxo() => Ligacoes().GetAwaiter().GetResult();
    [HttpGet("/Saneamento/Hidrometros/Create"), HttpGet("/Saneamento/Hidrometros/Edit"), HttpGet("/Saneamento/Hidrometros/Details"), HttpGet("/Saneamento/Hidrometros/Substituir"), HttpGet("/Saneamento/Hidrometros/Afericoes")]
    public IActionResult HidrometroFluxo() => Hidrometros().GetAwaiter().GetResult();
    [HttpGet("/Saneamento/Rotas")] public IActionResult Rotas() => Tela("Rotas e setores", "faturamento", "rotas-leitura");
    [HttpGet("/Saneamento/Leituras/Create"), HttpGet("/Saneamento/Leituras/Importar"), HttpGet("/Saneamento/Leituras/Criticas")]
    public IActionResult LeituraFluxo() => Leituras().GetAwaiter().GetResult();
    [HttpGet("/Saneamento/RevisoesConsumo")] public IActionResult RevisoesConsumo() => Tela("Revisões de consumo", "faturamento", "revisoes");
    [HttpGet("/Saneamento/Tarifas")] public IActionResult Tarifas() => Tela("Tabelas tarifárias", "comercial", "tarifas");
    [HttpGet("/Saneamento/Faturas/Details"), HttpGet("/Saneamento/Faturas/SegundaVia"), HttpGet("/Saneamento/Faturas/Cancelar")]
    public IActionResult FaturaFluxo() => Faturas().GetAwaiter().GetResult();
    [HttpGet("/Saneamento/Inadimplencia")] public IActionResult Inadimplencia() => Tela("Inadimplência", "faturamento", "inadimplencia");
    [HttpGet("/Saneamento/Cobranca")] public IActionResult Cobranca() => Tela("Cobrança", "faturamento", "cobrancas");
    [HttpGet("/Saneamento/Parcelamentos/Create"), HttpGet("/Saneamento/Parcelamentos/Details")] public IActionResult ParcelamentoFluxo() => Parcelamentos().GetAwaiter().GetResult();
    [HttpGet("/Saneamento/OrdensServico/Create"), HttpGet("/Saneamento/OrdensServico/Edit"), HttpGet("/Saneamento/OrdensServico/Details"), HttpGet("/Saneamento/OrdensServico/Executar")]
    public IActionResult OrdemServicoFluxo() => OrdensServico().GetAwaiter().GetResult();
    [HttpGet("/Saneamento/MateriaisCampo")] public IActionResult MateriaisCampo() => Tela("Materiais de campo", "operacao", "materiais");
    [HttpGet("/Saneamento/Redes"), HttpGet("/Saneamento/Trechos")] public IActionResult Redes() => Rede();
    [HttpGet("/Saneamento/UnidadesOperacionais")] public IActionResult UnidadesOperacionais() => Tela("Unidades operacionais", "gis-qualidade", "unidades-operacionais");
    [HttpGet("/Saneamento/Laboratorio/Amostras"), HttpGet("/Saneamento/Laboratorio/Parametros"), HttpGet("/Saneamento/Laboratorio/Resultados"), HttpGet("/Saneamento/Laboratorio/Conformidade")]
    public IActionResult LaboratorioFluxo() => Laboratorio();

    [HttpGet("/Saneamento/LigacoesAgua")] public IActionResult LigacoesAgua() => Tela("Ligações de água", "comercial", "ligacoes");
    [HttpGet("/Saneamento/LigacoesEsgoto")] public IActionResult LigacoesEsgoto() => Tela("Ligações de esgoto", "comercial", "ligacoes");
    [HttpGet("/Saneamento/Ocorrencias")] public IActionResult Ocorrencias() => Tela("Ocorrências de saneamento", "operacao", "atendimentos");
    [HttpGet("/Saneamento/Drenagem")] public IActionResult Drenagem() => Tela("Drenagem urbana e territorial", "gis-qualidade", "gis");
    [HttpGet("/Saneamento/PontosCriticos")] public IActionResult PontosCriticos() => Tela("Pontos críticos", "gis-qualidade", "gis");
    [HttpGet("/Saneamento/Indicadores")] public IActionResult Indicadores() => Tela("Indicadores de saneamento", "comercial", "dashboard");
    [HttpGet("/Saneamento/Residuos")] public IActionResult Residuos() => Tela("Tipos de resíduos sólidos", "operacao", "residuos");
    [HttpGet("/Saneamento/Coletas")] public IActionResult Coletas() => Tela("Coletas realizadas", "operacao", "coletas");
    [HttpGet("/Saneamento/RotasColeta")] public IActionResult RotasColeta() => Tela("Rotas de coleta", "operacao", "rotas-coleta");
    [HttpGet("/Saneamento/Ecopontos")] public IActionResult Ecopontos() => Tela("Ecopontos", "operacao", "ecopontos");
    [HttpGet("/Saneamento/Destinacao")] public IActionResult Destinacao() => Tela("Destinação de resíduos", "operacao", "destinacoes");
    [HttpGet("/Saneamento/RelatoriosResiduos")] public IActionResult RelatoriosResiduos() => Tela("Relatórios de resíduos", "operacao", "relatorios");

    private IActionResult Tela(string titulo, string modulo, string recurso)
    {
        ViewData["Titulo"] = titulo;
        ViewData["Modulo"] = modulo;
        ViewData["Recurso"] = recurso;
        return View("~/Views/Saneamento/Avancado.cshtml");
    }
}
