using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Frotas;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class FrotasController(
    IFrotasService s,
    ICurrentTenant tenant,
    ICurrentUser user,
    IAuthorizationEvaluator auth) : Controller
{
    [HttpGet("/Frotas"), HttpGet("/Frotas/Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken c) =>
        await ViewIf(FrotasPermissoes.Dashboard, await s.DashboardAsync(T(), E(), c), "Dashboard", c);

    // ==================== VEÍCULOS ====================

    [HttpGet("/Frotas/Veiculos")]
    public async Task<IActionResult> Veiculos([FromQuery] FrotaFiltro f, CancellationToken c) =>
        await ViewIf(FrotasPermissoes.VeiculoVer, await s.VeiculosAsync(T(), E(), f, c), "Veiculos", c);

    [HttpGet("/Frotas/Veiculos/Novo")]
    public async Task<IActionResult> NovoVeiculo(CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.VeiculoCriar, c)) return Forbid();
        ViewBag.BensPatrimoniais = await s.ObterBensAtivosSelectAsync(T(), E(), c);
        return View("VeiculoForm");
    }

    [HttpPost("/Frotas/Veiculos/Novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoVeiculo(VeiculoInput i, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.VeiculoCriar, c)) return Forbid();
        try
        {
            var id = await s.CriarVeiculoAsync(T(), U(), Trace(), i with { EntidadeId = E() }, c);
            TempData["Sucesso"] = $"Veículo placa {i.Placa?.ToUpperInvariant()} cadastrado com sucesso.";
            return Redirect("/Frotas/Veiculos");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError("", x.Message);
            ViewBag.BensPatrimoniais = await s.ObterBensAtivosSelectAsync(T(), E(), c);
            return View("VeiculoForm", i);
        }
    }

    [HttpGet("/Frotas/Veiculos/Editar/{id:long}")]
    public async Task<IActionResult> EditarVeiculo(long id, CancellationToken c)
    {
        var x = await s.VeiculoAsync(T(), E(), id, c);
        if (x is null) return NotFound();
        ViewBag.Id = id;
        ViewBag.BensPatrimoniais = await s.ObterBensAtivosSelectAsync(T(), E(), c);
        return await ViewIf(FrotasPermissoes.VeiculoEditar, x, "VeiculoForm", c);
    }

    [HttpPost("/Frotas/Veiculos/Editar/{id:long}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarVeiculo(long id, VeiculoInput i, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.VeiculoEditar, c)) return Forbid();
        try
        {
            await s.EditarVeiculoAsync(T(), U(), Trace(), id, i with { EntidadeId = E() }, c);
            TempData["Sucesso"] = $"Dados do veículo {i.Placa} atualizados com sucesso.";
            return Redirect("/Frotas/Veiculos");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ModelState.AddModelError("", x.Message);
            ViewBag.Id = id;
            ViewBag.BensPatrimoniais = await s.ObterBensAtivosSelectAsync(T(), E(), c);
            return View("VeiculoForm", i);
        }
    }

    [HttpGet("/Frotas/Veiculos/Detalhe/{id:long}")]
    public async Task<IActionResult> VeiculoDetalhe(long id, CancellationToken c)
    {
        var x = await s.VeiculoAsync(T(), E(), id, c);
        if (x is null) return NotFound();
        ViewBag.UtilizacoesRecentes = await s.UtilizacoesAsync(T(), E(), new(VeiculoId: id, Tamanho: 5), c);
        ViewBag.OrdensRecentes = await s.OrdensAsync(T(), E(), new(VeiculoId: id, Tamanho: 5), c);
        ViewBag.PodeEditar = await Allowed(FrotasPermissoes.VeiculoEditar, c);
        return await ViewIf(FrotasPermissoes.VeiculoVer, x, "VeiculoDetalhe", c);
    }

    // ==================== MOTORISTAS ====================

    [HttpGet("/Frotas/Motoristas")]
    public async Task<IActionResult> Motoristas([FromQuery] FrotaFiltro f, CancellationToken c) =>
        await ViewIf(FrotasPermissoes.MotoristaVer, await s.MotoristasAsync(T(), E(), f, c), "Motoristas", c);

    [HttpGet("/Frotas/Motoristas/Novo")]
    public async Task<IActionResult> NovoMotorista(CancellationToken c) =>
        await ViewIf(FrotasPermissoes.MotoristaCriar, null, "MotoristaForm", c);

    [HttpPost("/Frotas/Motoristas/Novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoMotorista(MotoristaInput i, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.MotoristaCriar, c)) return Forbid();
        try
        {
            var id = await s.CriarMotoristaAsync(T(), U(), Trace(), i with { EntidadeId = E() }, c);
            TempData["Sucesso"] = $"Motorista {i.Nome} cadastrado com sucesso.";
            return Redirect("/Frotas/Motoristas");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError("", x.Message);
            return View("MotoristaForm", i);
        }
    }

    // ==================== UTILIZAÇÕES ====================

    [HttpGet("/Frotas/Utilizacoes")]
    public async Task<IActionResult> Utilizacoes([FromQuery] FrotaFiltro f, CancellationToken c) =>
        await ViewIf(FrotasPermissoes.UtilizacaoVer, await s.UtilizacoesAsync(T(), E(), f, c), "Utilizacoes", c);

    [HttpGet("/Frotas/Utilizacoes/Nova")]
    public async Task<IActionResult> NovaUtilizacao(CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.UtilizacaoCriar, c)) return Forbid();
        ViewBag.Veiculos = await s.VeiculosAsync(T(), E(), new(Status: "ATIVO"), c);
        ViewBag.Motoristas = await s.MotoristasAsync(T(), E(), new(Status: "ATIVO"), c);
        return View("UtilizacaoForm");
    }

    [HttpPost("/Frotas/Utilizacoes/Nova"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaUtilizacao(UtilizacaoInput i, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.UtilizacaoCriar, c)) return Forbid();
        try
        {
            var id = await s.CriarUtilizacaoAsync(T(), U(), Trace(), i with { EntidadeId = E() }, c);
            TempData["Sucesso"] = $"Utilização de veículo #{id} iniciada com sucesso.";
            return Redirect("/Frotas/Utilizacoes");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ModelState.AddModelError("", x.Message);
            ViewBag.Veiculos = await s.VeiculosAsync(T(), E(), new(Status: "ATIVO"), c);
            ViewBag.Motoristas = await s.MotoristasAsync(T(), E(), new(Status: "ATIVO"), c);
            return View("UtilizacaoForm", i);
        }
    }

    [HttpGet("/Frotas/Utilizacoes/Finalizar/{id:long}")]
    public async Task<IActionResult> Finalizar(long id, CancellationToken c)
    {
        var x = (await s.UtilizacoesAsync(T(), E(), new(), c)).SingleOrDefault(u => u.Id == id);
        if (x is null) return NotFound();
        ViewBag.Id = id;
        return await ViewIf(FrotasPermissoes.UtilizacaoFinalizar, x, "FinalizarUtilizacao", c);
    }

    [HttpPost("/Frotas/Utilizacoes/Finalizar/{id:long}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalizar(long id, decimal kmRetorno, DateTimeOffset retornoEm, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.UtilizacaoFinalizar, c)) return Forbid();
        try
        {
            await s.FinalizarUtilizacaoAsync(T(), E(), U(), Trace(), id, kmRetorno, retornoEm, c);
            TempData["Sucesso"] = "Utilização de veículo finalizada com sucesso. Quilometragem atualizada.";
            return Redirect("/Frotas/Utilizacoes");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Erro"] = x.Message;
            return Redirect($"/Frotas/Utilizacoes/Finalizar/{id}");
        }
    }

    // ==================== ABASTECIMENTOS ====================

    [HttpGet("/Frotas/Abastecimentos")]
    public async Task<IActionResult> Abastecimentos([FromQuery] FrotaFiltro f, CancellationToken c) =>
        await ViewIf(FrotasPermissoes.AbastecimentoVer, await s.AbastecimentosAsync(T(), E(), f, c), "Abastecimentos", c);

    [HttpGet("/Frotas/Abastecimentos/Novo")]
    public async Task<IActionResult> NovoAbastecimento(CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.AbastecimentoCriar, c)) return Forbid();
        await CarregarSelectsAbastecimento(c);
        return View("AbastecimentoForm");
    }

    [HttpPost("/Frotas/Abastecimentos/Novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoAbastecimento(AbastecimentoInput i, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.AbastecimentoCriar, c)) return Forbid();
        try
        {
            var id = await s.CriarAbastecimentoAsync(T(), U(), Trace(), i with { EntidadeId = E() }, c);
            TempData["Sucesso"] = $"Abastecimento #{id} registrado com sucesso.";
            return Redirect("/Frotas/Abastecimentos");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ModelState.AddModelError("", x.Message);
            await CarregarSelectsAbastecimento(c);
            return View("AbastecimentoForm", i);
        }
    }

    // ==================== MANUTENÇÕES ====================

    [HttpGet("/Frotas/Manutencoes")]
    public async Task<IActionResult> Manutencoes([FromQuery] FrotaFiltro f, CancellationToken c) =>
        await ViewIf(FrotasPermissoes.ManutencaoVer, await s.ManutencoesAsync(T(), E(), f, c), "Manutencoes", c);

    [HttpGet("/Frotas/Manutencoes/Nova")]
    public async Task<IActionResult> NovaManutencao(CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.ManutencaoCriar, c)) return Forbid();
        await CarregarSelectsManutencao(c);
        return View("ManutencaoForm");
    }

    [HttpPost("/Frotas/Manutencoes/Nova"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaManutencao(ManutencaoInput i, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.ManutencaoCriar, c)) return Forbid();
        try
        {
            var id = await s.CriarManutencaoAsync(T(), U(), Trace(), i with { EntidadeId = E() }, c);
            TempData["Sucesso"] = $"Manutenção #{id} aberta. Veículo colocado em estado EM_MANUTENCAO.";
            return Redirect("/Frotas/Manutencoes");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ModelState.AddModelError("", x.Message);
            await CarregarSelectsManutencao(c);
            return View("ManutencaoForm", i);
        }
    }

    [HttpPost("/Frotas/Manutencoes/{id:long}/Concluir"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ConcluirManutencao(long id, decimal valorFinal, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.ManutencaoConcluir, c)) return Forbid();
        try
        {
            await s.ConcluirManutencaoAsync(T(), E(), U(), Trace(), id, valorFinal, c);
            TempData["Sucesso"] = "Manutenção concluída com sucesso. Veículo reativado caso não existam outras ordens pendentes.";
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Erro"] = x.Message;
        }
        return Redirect("/Frotas/Manutencoes");
    }

    // ==================== ORDENS DE SERVIÇO ====================

    [HttpGet("/Frotas/OrdensServico")]
    public async Task<IActionResult> Ordens([FromQuery] FrotaFiltro f, CancellationToken c) =>
        await ViewIf(FrotasPermissoes.OsVer, await s.OrdensAsync(T(), E(), f, c), "OrdensServico", c);

    [HttpGet("/Frotas/OrdensServico/Nova")]
    public async Task<IActionResult> NovaOrdem(CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.OsCriar, c)) return Forbid();
        await CarregarSelectsOrdem(c);
        return View("OrdemForm");
    }

    [HttpPost("/Frotas/OrdensServico/Nova"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaOrdem(OrdemServicoInput i, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.OsCriar, c)) return Forbid();
        try
        {
            var id = await s.CriarOrdemAsync(T(), U(), Trace(), i with { EntidadeId = E() }, c);
            TempData["Sucesso"] = $"Ordem de Serviço #{i.Numero} aberta com sucesso.";
            return Redirect($"/Frotas/OrdensServico/Detalhe/{id}");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ModelState.AddModelError("", x.Message);
            await CarregarSelectsOrdem(c);
            return View("OrdemForm", i);
        }
    }

    [HttpGet("/Frotas/OrdensServico/Detalhe/{id:long}")]
    public async Task<IActionResult> OrdemDetalhe(long id, CancellationToken c)
    {
        var detalhe = await s.ObterOrdemDetalheAsync(T(), E(), id, c);
        if (detalhe is null) return NotFound();

        ViewBag.PodeAprovar = await Allowed(FrotasPermissoes.OsAprovar, c);
        ViewBag.PodeConcluir = await Allowed(FrotasPermissoes.OsConcluir, c);
        return await ViewIf(FrotasPermissoes.OsVer, detalhe, "OrdemDetalhe", c);
    }

    [HttpPost("/Frotas/OrdensServico/{id:long}/Alterar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarOrdem(long id, [FromForm] string acao, [FromForm] string? justificativa, CancellationToken c)
    {
        var perm = acao?.Trim().ToLowerInvariant() switch
        {
            "aprovar" => FrotasPermissoes.OsAprovar,
            "concluir" => FrotasPermissoes.OsConcluir,
            "cancelar" => FrotasPermissoes.OsCriar,
            _ => FrotasPermissoes.OsVer
        };

        if (!await Allowed(perm, c)) return Forbid();

        try
        {
            await s.AlterarOrdemAsync(T(), E(), U(), Trace(), id, acao!, justificativa, c);
            TempData["Sucesso"] = acao?.Trim().ToLowerInvariant() switch
            {
                "concluir" => "Ordem de Serviço CONCLUÍDA com sucesso! Peças baixadas do Almoxarifado na mesma transação.",
                "aprovar" => "Ordem de Serviço APROVADA com sucesso.",
                "cancelar" => "Ordem de Serviço CANCELADA com sucesso.",
                _ => "Situação da Ordem de Serviço alterada com sucesso."
            };
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Erro"] = x.Message;
        }

        return Redirect($"/Frotas/OrdensServico/Detalhe/{id}");
    }

    // ==================== DOCUMENTOS ====================

    [HttpGet("/Frotas/Documentos")]
    public async Task<IActionResult> Documentos([FromQuery] FrotaFiltro f, CancellationToken c)
    {
        ViewBag.Veiculos = await s.VeiculosAsync(T(), E(), new(), c);
        ViewBag.PodeCriar = await Allowed(FrotasPermissoes.DocumentoCriar, c);
        return await ViewIf(FrotasPermissoes.DocumentoVer, await s.DocumentosAsync(T(), E(), f, c), "Documentos", c);
    }

    [HttpPost("/Frotas/Documentos"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Documento(DocumentoInput i, CancellationToken c)
    {
        if (!await Allowed(FrotasPermissoes.DocumentoCriar, c)) return Forbid();
        try
        {
            var id = await s.CriarDocumentoAsync(T(), U(), Trace(), i with { EntidadeId = E() }, c);
            TempData["Sucesso"] = $"Documento #{id} cadastrado com sucesso.";
            return Redirect("/Frotas/Documentos");
        }
        catch (Exception x) when (x is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError("", x.Message);
            ViewBag.Veiculos = await s.VeiculosAsync(T(), E(), new(), c);
            ViewBag.PodeCriar = true;
            return View("Documentos", await s.DocumentosAsync(T(), E(), new(), c));
        }
    }

    // ==================== EXPORTAÇÃO CSV ====================

    [HttpGet("/Frotas/{tipo}/Exportar")]
    public async Task<IActionResult> Exportar(string tipo, CancellationToken c) =>
        await Allowed(FrotasPermissoes.Exportar, c)
            ? File(await s.ExportarCsvAsync(T(), E(), tipo, U(), Trace(), c), "text/csv; charset=utf-8", $"{tipo}.csv")
            : Forbid();

    // ==================== REDIRECTS E ALIASES ====================

    [HttpGet("/Frotas/Veiculos/Create")] public IActionResult VeiculoCreate() => Redirect("/Frotas/Veiculos/Novo");
    [HttpGet("/Frotas/Veiculos/Edit/{id:long}")] public IActionResult VeiculoEdit(long id) => Redirect($"/Frotas/Veiculos/Editar/{id}");
    [HttpGet("/Frotas/Veiculos/Details/{id:long}")] public IActionResult VeiculoDetails(long id) => Redirect($"/Frotas/Veiculos/Detalhe/{id}");
    [HttpGet("/Frotas/Viagens")] public IActionResult Viagens() => Redirect("/Frotas/Utilizacoes");
    [HttpGet("/Frotas/Multas")] public IActionResult Multas() => Redirect("/Ativos/Alertas");
    [HttpGet("/Frotas/Relatorios")] public IActionResult Relatorios() => Redirect("/Ativos/Relatorios");

    // ==================== AUXILIARES ====================

    async Task CarregarSelectsAbastecimento(CancellationToken c)
    {
        ViewBag.Veiculos = await s.VeiculosAsync(T(), E(), new(Status: "ATIVO"), c);
        ViewBag.Motoristas = await s.MotoristasAsync(T(), E(), new(Status: "ATIVO"), c);
        ViewBag.Fornecedores = await s.ObterFornecedoresAtivosSelectAsync(T(), E(), c);
        ViewBag.Contratos = await s.ObterContratosAtivosSelectAsync(T(), E(), c);
    }

    async Task CarregarSelectsManutencao(CancellationToken c)
    {
        ViewBag.Veiculos = await s.VeiculosAsync(T(), E(), new(), c);
        ViewBag.Fornecedores = await s.ObterFornecedoresAtivosSelectAsync(T(), E(), c);
        ViewBag.Contratos = await s.ObterContratosAtivosSelectAsync(T(), E(), c);
    }

    async Task CarregarSelectsOrdem(CancellationToken c)
    {
        ViewBag.Veiculos = await s.VeiculosAsync(T(), E(), new(), c);
        ViewBag.Fornecedores = await s.ObterFornecedoresAtivosSelectAsync(T(), E(), c);
        ViewBag.Contratos = await s.ObterContratosAtivosSelectAsync(T(), E(), c);
        ViewBag.Materiais = await s.ObterMateriaisConsumoSelectAsync(T(), E(), c);
        ViewBag.Almoxarifados = await s.ObterAlmoxarifadosSelectAsync(T(), E(), c);
    }

    async Task<IActionResult> ViewIf(string p, object? m, string v, CancellationToken c) =>
        await Allowed(p, c) ? View(v, m) : Forbid();

    async Task<bool> Allowed(string p, CancellationToken c)
    {
        var i = p.LastIndexOf('.');
        return (await auth.EvaluateAsync(new(U(), "frotas", p[..i], p[(i + 1)..], T(), E(), tenant.ExercicioId, null, null, Trace(), "WEB_FUNC04"), c)).Permitido;
    }

    long T() => tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório.");
    long E() => tenant.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório.");
    long U() => user.UsuarioId ?? throw new InvalidOperationException("Usuário obrigatório.");
    string Trace() => HttpContext.TraceIdentifier;
}
