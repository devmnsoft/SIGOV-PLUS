using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Compras;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ComprasController(
    IComprasService s,
    Sigov.Application.Almoxarifado.IAlmoxarifadoService almoxarifadoService,
    ICurrentTenant tenant,
    ICurrentUser user,
    IAuthorizationEvaluator auth) : Controller
{
    [HttpGet("/Compras"), HttpGet("/Compras/Dashboard")]
    public async Task<IActionResult> Index(CancellationToken c) =>
        await ViewIf(ComprasPermissoes.Dashboard, await s.DashboardAsync(T(), E(), c), "Dashboard", c);

    // ==================== FORNECEDORES ====================

    [HttpGet("/Compras/Fornecedores")]
    public async Task<IActionResult> Fornecedores([FromQuery] CompraFiltro f, CancellationToken c) =>
        await ViewIf(ComprasPermissoes.FornecedorVer, await s.FornecedoresAsync(T(), E(), f, c), null, c);

    [HttpGet("/Compras/Fornecedores/Novo")]
    public async Task<IActionResult> NovoFornecedor(CancellationToken c) =>
        await ViewIf(ComprasPermissoes.FornecedorCriar, new FornecedorInput(E(), "", "JURIDICA", "", null, null, null, null), "FornecedorForm", c);

    [HttpPost("/Compras/Fornecedores/Novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoFornecedor(FornecedorInput i, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.FornecedorCriar, c)) return Forbid();
        try
        {
            await s.CriarFornecedorAsync(T(), U(), Trace(), i with { EntidadeId = E() }, c);
            TempData["Sucesso"] = "Fornecedor cadastrado com sucesso.";
            return Redirect("/Compras/Fornecedores");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError("", x.Message);
            return View("FornecedorForm", i);
        }
    }

    [HttpGet("/Compras/Fornecedores/Exportar")]
    public async Task<IActionResult> Exportar(CancellationToken c) =>
        await Allowed(ComprasPermissoes.Exportar, c)
            ? File(await s.ExportarFornecedoresAsync(T(), E(), U(), Trace(), c), "text/csv; charset=utf-8", "fornecedores.csv")
            : Forbid();

    // ==================== SOLICITAÇÕES ====================

    [HttpGet("/Compras/Solicitacoes")]
    public async Task<IActionResult> Solicitacoes([FromQuery] CompraFiltro f, CancellationToken c) =>
        await ViewIf(ComprasPermissoes.SolicitacaoVer, await s.SolicitacoesAsync(T(), E(), f, c), null, c);

    [HttpGet("/Compras/Solicitacoes/Nova")]
    public async Task<IActionResult> NovaSolicitacao(CancellationToken c) =>
        await ViewIf(ComprasPermissoes.SolicitacaoCriar, null, "SolicitacaoForm", c);

    [HttpPost("/Compras/Solicitacoes/Nova"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaSolicitacao(string unidadeSolicitante, string justificativa, string prioridade, string origem, string descricao, decimal quantidade, string unidade, string tipo, decimal valorEstimado, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.SolicitacaoCriar, c)) return Forbid();
        try
        {
            var id = await s.CriarSolicitacaoAsync(T(), U(), Trace(), new(E(), unidadeSolicitante, justificativa, prioridade, origem, null, null, [new(descricao, quantidade, unidade, tipo, valorEstimado, tipo == "PERMANENTE")]), c);
            TempData["Sucesso"] = $"Solicitação de compra #{id} criada com sucesso.";
            return Redirect($"/Compras/Solicitacoes/Detalhe/{id}");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError("", x.Message);
            return View("SolicitacaoForm");
        }
    }

    [HttpGet("/Compras/Solicitacoes/Detalhe/{id:long}")]
    public async Task<IActionResult> SolicitacaoDetalhe(long id, CancellationToken c)
    {
        var x = (await s.SolicitacoesAsync(T(), E(), new(), c)).SingleOrDefault(x => x.Id == id);
        return x is null ? NotFound() : await ViewIf(ComprasPermissoes.SolicitacaoVer, x, "SolicitacaoDetalhe", c);
    }

    // ==================== PROCESSOS ====================

    [HttpGet("/Compras/Processos")]
    public async Task<IActionResult> Processos([FromQuery] CompraFiltro f, CancellationToken c) =>
        await ViewIf(ComprasPermissoes.ProcessoVer, await s.ProcessosAsync(T(), E(), f, c), null, c);

    [HttpGet("/Compras/Processos/Novo")]
    public async Task<IActionResult> NovoProcesso(CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.ProcessoCriar, c)) return Forbid();
        await CarregarSelectsProcesso(c);
        return View("ProcessoForm");
    }

    [HttpPost("/Compras/Processos/Novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoProcesso(string numero, int exercicio, string modalidadeCodigo, string criterioCodigo, string objeto, string justificativa, DateOnly? dataAbertura, DateOnly? dataLimite, string itemDescricao, decimal itemQuantidade, string itemUnidade, string itemTipo, decimal itemValorEstimado, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.ProcessoCriar, c)) return Forbid();
        try
        {
            var itens = new List<ProcessoItemInput>();
            if (!string.IsNullOrWhiteSpace(itemDescricao) && itemQuantidade > 0)
            {
                itens.Add(new(itemDescricao.Trim(), itemQuantidade, itemUnidade ?? "UN", itemTipo ?? "CONSUMO", itemValorEstimado, itemTipo == "PERMANENTE"));
            }

            var input = new ProcessoInput(E(), exercicio > 0 ? exercicio : DateTime.Today.Year, numero, modalidadeCodigo, criterioCodigo, objeto, justificativa, dataAbertura, dataLimite, null, itens);
            var id = await s.CriarProcessoAsync(T(), U(), Trace(), input, c);
            TempData["Sucesso"] = $"Processo de compra #{numero} criado com sucesso.";
            return Redirect($"/Compras/Processos/Detalhe/{id}");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError("", x.Message);
            await CarregarSelectsProcesso(c);
            return View("ProcessoForm");
        }
    }

    [HttpGet("/Compras/Processos/Detalhe/{id:long}")]
    public async Task<IActionResult> ProcessoDetalhe(long id, CancellationToken c)
    {
        var x = await s.ProcessoAsync(T(), E(), id, c);
        if (x is null) return NotFound();
        ViewBag.PodeAvancar = await Allowed(ComprasPermissoes.ProcessoAvancar, c);
        ViewBag.PodeCotar = await Allowed(ComprasPermissoes.CotacaoCriar, c);
        ViewBag.PodeJulgar = await Allowed(ComprasPermissoes.Julgar, c);
        ViewBag.PodeReceber = await Allowed(ComprasPermissoes.Receber, c);
        return await ViewIf(ComprasPermissoes.ProcessoVer, x, "ProcessoDetalhe", c);
    }

    [HttpPost("/Compras/Processos/{id:long}/Avancar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Avancar(long id, [FromForm] string fase, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.ProcessoAvancar, c)) return Forbid();
        try
        {
            await s.AvancarAsync(T(), E(), U(), Trace(), id, fase, c);
            TempData["Sucesso"] = $"Fase do processo avançada para '{fase}'.";
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Erro"] = x.Message;
        }
        return Redirect($"/Compras/Processos/Detalhe/{id}");
    }

    [HttpPost("/Compras/Processos/{id:long}/Finalizar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalizar(long id, [FromForm] string statusFinal, [FromForm] string justificativa, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.ProcessoAvancar, c)) return Forbid();
        try
        {
            await s.FinalizarProcessoAsync(T(), E(), U(), Trace(), id, statusFinal, justificativa, c);
            TempData["Sucesso"] = $"Processo alterado para '{statusFinal}' com sucesso.";
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Erro"] = x.Message;
        }
        return Redirect($"/Compras/Processos/Detalhe/{id}");
    }

    // ==================== COTAÇÕES E JULGAMENTO ====================

    [HttpGet("/Compras/Processos/{id:long}/Cotacoes")]
    public async Task<IActionResult> Cotacoes(long id, CancellationToken c)
    {
        ViewBag.Id = id;
        ViewBag.Fornecedores = await s.FornecedoresAsync(T(), E(), new(Status: "ATIVO"), c);
        return await ViewIf(ComprasPermissoes.CotacaoVer, await s.ProcessoAsync(T(), E(), id, c), "Cotacoes", c);
    }

    [HttpPost("/Compras/Processos/{id:long}/Cotacoes"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaCotacao(long id, [FromForm] CotacaoInput input, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.CotacaoCriar, c)) return Forbid();
        try
        {
            await s.CotarAsync(T(), E(), U(), Trace(), id, input, c);
            TempData["Sucesso"] = "Cotação registrada com sucesso.";
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Erro"] = x.Message;
        }
        return Redirect($"/Compras/Processos/{id}/Cotacoes");
    }

    [HttpGet("/Compras/Processos/{id:long}/Julgamento")]
    public async Task<IActionResult> Julgamento(long id, CancellationToken c)
    {
        ViewBag.Id = id;
        return await ViewIf(ComprasPermissoes.Julgar, await s.ProcessoAsync(T(), E(), id, c), "Julgamento", c);
    }

    [HttpPost("/Compras/Processos/{id:long}/Julgar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecutarJulgamento(long id, [FromForm] JulgmentoInputCompat input, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.Julgar, c)) return Forbid();
        try
        {
            await s.JulgarAsync(T(), E(), U(), Trace(), id, input, c);
            TempData["Sucesso"] = "Julgamento do item homologado com sucesso.";
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Erro"] = x.Message;
        }
        return Redirect($"/Compras/Processos/{id}/Julgamento");
    }

    [HttpPost("/Compras/Processos/{id:long}/Homologar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Homologar(long id, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.Julgar, c)) return Forbid();
        try
        {
            await s.HomologarAsync(T(), E(), U(), Trace(), id, c);
            TempData["Sucesso"] = "Processo homologado com sucesso.";
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Erro"] = x.Message;
        }
        return Redirect($"/Compras/Processos/Detalhe/{id}");
    }

    // ==================== RECEBIMENTO PARCIAL (PONTE ESTOQUE / PATRIMÔNIO) ====================

    [HttpGet("/Compras/Processos/{id:long}/Recebimento")]
    public async Task<IActionResult> Recebimento(long id, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.Receber, c)) return Forbid();
        var proc = await s.ProcessoAsync(T(), E(), id, c);
        if (proc is null) return NotFound();

        ViewBag.Processo = proc.Processo;
        ViewBag.Saldos = await s.ObterSaldosRecebimentoAsync(T(), E(), id, c);
        ViewBag.Almoxarifados = await almoxarifadoService.ListarLocaisAsync(T(), E(), true, c);
        ViewBag.Contratos = await s.ContratosAsync(T(), E(), new(Status: "VIGENTE"), c);
        ViewBag.Atas = await s.AtasAsync(T(), E(), new(Status: "VIGENTE"), c);
        ViewBag.HistoricoRecebimentos = await s.RecebimentosAsync(T(), E(), id, c);

        return View("RecebimentoForm");
    }

    [HttpPost("/Compras/Processos/{id:long}/Recebimento"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarRecebimento(long id, [FromForm] string documento, [FromForm] DateOnly dataRecebimento, [FromForm] long almoxarifadoId, [FromForm] long? contratoId, [FromForm] long? ataId, [FromForm] List<RecebimentoItemInput> itens, CancellationToken c)
    {
        if (!await Allowed(ComprasPermissoes.Receber, c)) return Forbid();
        try
        {
            var itensValidos = itens.Where(x => x.Quantidade > 0).ToList();
            if (itensValidos.Count == 0)
                throw new ArgumentException("Ao menos um item deve possuir quantidade recebida maior que zero.");

            var recInput = new RecebimentoInput(id, contratoId, ataId, documento, dataRecebimento, almoxarifadoId, itensValidos);
            var recId = await s.RegistrarRecebimentoAsync(T(), E(), U(), Trace(), recInput, c);
            TempData["Sucesso"] = $"Recebimento #{recId} registrado com sucesso! Saldos de estoque atualizados no Almoxarifado.";
            return Redirect($"/Compras/Processos/{id}/Recebimento");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Erro"] = x.Message;
            return Redirect($"/Compras/Processos/{id}/Recebimento");
        }
    }

    // ==================== CONTRATOS E ATAS ====================

    [HttpGet("/Compras/Contratos")]
    public async Task<IActionResult> Contratos([FromQuery] CompraFiltro f, CancellationToken c) =>
        await ViewIf(ComprasPermissoes.ContratoVer, await s.ContratosAsync(T(), E(), f, c), null, c);

    [HttpGet("/Compras/Contratos/Detalhe/{id:long}")]
    public async Task<IActionResult> ContratoDetalhe(long id, CancellationToken c)
    {
        var x = (await s.ContratosAsync(T(), E(), new(), c)).SingleOrDefault(x => x.Id == id);
        return x is null ? NotFound() : await ViewIf(ComprasPermissoes.ContratoVer, x, "InstrumentoDetalhe", c);
    }

    [HttpGet("/Compras/Atas")]
    public async Task<IActionResult> Atas([FromQuery] CompraFiltro f, CancellationToken c) =>
        await ViewIf(ComprasPermissoes.AtaVer, await s.AtasAsync(T(), E(), f, c), null, c);

    [HttpGet("/Compras/Atas/Detalhe/{id:long}")]
    public async Task<IActionResult> AtaDetalhe(long id, CancellationToken c)
    {
        var x = (await s.AtasAsync(T(), E(), new(), c)).SingleOrDefault(x => x.Id == id);
        return x is null ? NotFound() : await ViewIf(ComprasPermissoes.AtaVer, x, "InstrumentoDetalhe", c);
    }

    // ==================== AUXILIARES ====================

    async Task CarregarSelectsProcesso(CancellationToken c)
    {
        ViewBag.Modalidades = await s.ModalidadesAsync(c);
        ViewBag.Criterios = await s.CriteriosAsync(c);
        ViewBag.Materiais = (await almoxarifadoService.ListarMateriaisAsync(T(), E(), new(Ativo: true, TamanhoPagina: 200), c)).Itens;
    }

    async Task<IActionResult> ViewIf(string p, object? m, string? v, CancellationToken c) =>
        await Allowed(p, c) ? v is null ? View(m) : View(v, m) : Forbid();

    async Task<bool> Allowed(string p, CancellationToken c)
    {
        var i = p.LastIndexOf('.');
        return (await auth.EvaluateAsync(new(U(), "compras", p[..i], p[(i + 1)..], T(), E(), tenant.ExercicioId, null, null, Trace(), "WEB_FUNC03"), c)).Permitido;
    }

    long T() => tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório.");
    long E() => tenant.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório.");
    long U() => user.UsuarioId ?? throw new InvalidOperationException("Usuário obrigatório.");
    string Trace() => HttpContext.TraceIdentifier;
}
