using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Application.Industria;
using Sigov.Infrastructure.Persistence.Dapper;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/industria")]
[RequireModule("industria_producao")]
public sealed class IndustriaController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IIndustriaEstoqueService _estoque;
    private readonly ILogger<IndustriaController> _logger;

    public IndustriaController(DapperContext context, ICurrentTenant tenant, ICurrentUser user, IIndustriaEstoqueService estoque, ILogger<IndustriaController> logger)
    {
        _context = context;
        _tenant = tenant;
        _user = user;
        _estoque = estoque;
        _logger = logger;
    }

    [HttpGet("centros-trabalho")]
    public Task<ActionResult<ApiResponse<object>>> CentrosTrabalho([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.industria_centro_trabalho", busca, page, pageSize, "codigo,nome");

    [HttpGet("centros-trabalho/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> CentroTrabalho(long id) => Obter("sigov.industria_centro_trabalho", id);

    [HttpPost("centros-trabalho")]
    public Task<ActionResult<ApiResponse<object>>> CriarCentroTrabalho([FromBody] CentroTrabalhoRequest request) => UpsertCentroTrabalho(null, request);

    [HttpPut("centros-trabalho/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarCentroTrabalho(long id, [FromBody] CentroTrabalhoRequest request) => UpsertCentroTrabalho(id, request);

    [HttpPatch("centros-trabalho/{id:long}/status")]
    public Task<ActionResult<ApiResponse<object>>> StatusCentroTrabalho(long id, [FromBody] IndustriaStatusRequest request) => AlterarAtivo("sigov.industria_centro_trabalho", id, request.Ativo, "CENTRO_TRABALHO_ATUALIZADO");

    [HttpGet("recursos")]
    public Task<ActionResult<ApiResponse<object>>> Recursos([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.industria_recurso", busca, page, pageSize, "codigo,nome");

    [HttpGet("recursos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Recurso(long id) => Obter("sigov.industria_recurso", id);

    [HttpPost("recursos")]
    public Task<ActionResult<ApiResponse<object>>> CriarRecurso([FromBody] RecursoRequest request) => UpsertRecurso(null, request);

    [HttpPut("recursos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarRecurso(long id, [FromBody] RecursoRequest request) => UpsertRecurso(id, request);

    [HttpPatch("recursos/{id:long}/status")]
    public Task<ActionResult<ApiResponse<object>>> StatusRecurso(long id, [FromBody] IndustriaStatusRequest request) => AlterarAtivo("sigov.industria_recurso", id, request.Ativo, "RECURSO_ATUALIZADO");

    [HttpGet("produtos")]
    public Task<ActionResult<ApiResponse<object>>> Produtos([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.industria_produto", busca, page, pageSize, "codigo,nome");

    [HttpGet("produtos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Produto(long id) => Obter("sigov.industria_produto", id);

    [HttpPost("produtos")]
    public Task<ActionResult<ApiResponse<object>>> CriarProduto([FromBody] ProdutoIndustrialRequest request) => UpsertProduto(null, request);

    [HttpPut("produtos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarProduto(long id, [FromBody] ProdutoIndustrialRequest request) => UpsertProduto(id, request);

    [HttpGet("fichas-tecnicas")]
    public Task<ActionResult<ApiResponse<object>>> Fichas([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.industria_ficha_tecnica", busca, page, pageSize, "created_at desc");

    [HttpGet("fichas-tecnicas/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Ficha(long id) => ObterComFilhos("sigov.industria_ficha_tecnica", "sigov.industria_ficha_tecnica_item", "ficha_tecnica_id", id, "itens");

    [HttpPost("fichas-tecnicas")]
    public Task<ActionResult<ApiResponse<object>>> CriarFicha([FromBody] FichaTecnicaRequest request) => UpsertFicha(null, request);

    [HttpPut("fichas-tecnicas/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarFicha(long id, [FromBody] FichaTecnicaRequest request) => UpsertFicha(id, request);

    [HttpPut("fichas-tecnicas/{id:long}/itens")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarItensFicha(long id, [FromBody] IReadOnlyCollection<FichaTecnicaItemRequest> itens) => AtualizarItensFichaAsync(id, itens);

    [HttpPost("fichas-tecnicas/{id:long}/ativar")]
    public Task<ActionResult<ApiResponse<object>>> AtivarFicha(long id) => AlterarStatus("sigov.industria_ficha_tecnica", id, "ATIVA", "FICHA_TECNICA_ATUALIZADA");

    [HttpPost("fichas-tecnicas/{id:long}/inativar")]
    public Task<ActionResult<ApiResponse<object>>> InativarFicha(long id) => AlterarStatus("sigov.industria_ficha_tecnica", id, "INATIVA", "FICHA_TECNICA_ATUALIZADA");

    [HttpGet("roteiros")]
    public Task<ActionResult<ApiResponse<object>>> Roteiros([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.industria_roteiro", busca, page, pageSize, "codigo,nome");

    [HttpGet("roteiros/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Roteiro(long id) => ObterComFilhos("sigov.industria_roteiro", "sigov.industria_roteiro_operacao", "roteiro_id", id, "operacoes");

    [HttpPost("roteiros")]
    public Task<ActionResult<ApiResponse<object>>> CriarRoteiro([FromBody] RoteiroRequest request) => UpsertRoteiro(null, request);

    [HttpPut("roteiros/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarRoteiro(long id, [FromBody] RoteiroRequest request) => UpsertRoteiro(id, request);

    [HttpPut("roteiros/{id:long}/operacoes")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarOperacoes(long id, [FromBody] IReadOnlyCollection<RoteiroOperacaoRequest> operacoes) => AtualizarOperacoesAsync(id, operacoes);

    [HttpGet("ordens-producao")]
    public Task<ActionResult<ApiResponse<object>>> Ordens([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.industria_ordem_producao", busca, page, pageSize, "created_at desc");

    [HttpGet("ordens-producao/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Ordem(long id) => ObterOrdem(id);

    [HttpPost("ordens-producao")]
    public Task<ActionResult<ApiResponse<object>>> CriarOrdem([FromBody] OrdemProducaoRequest request) => CriarOrdemAsync(request);

    [HttpPost("ordens-producao/{id:long}/liberar")]
    public Task<ActionResult<ApiResponse<object>>> Liberar(long id) => MudarStatusOrdem(id, "LIBERADA", "ORDEM_PRODUCAO_LIBERADA");

    [HttpPost("ordens-producao/{id:long}/iniciar")]
    public Task<ActionResult<ApiResponse<object>>> Iniciar(long id) => MudarStatusOrdem(id, "EM_PRODUCAO", "ORDEM_PRODUCAO_INICIADA");

    [HttpPost("ordens-producao/{id:long}/pausar")]
    public Task<ActionResult<ApiResponse<object>>> Pausar(long id) => MudarStatusOrdem(id, "PAUSADA", "ORDEM_PRODUCAO_PAUSADA");

    [HttpPost("ordens-producao/{id:long}/retomar")]
    public Task<ActionResult<ApiResponse<object>>> Retomar(long id) => MudarStatusOrdem(id, "EM_PRODUCAO", "ORDEM_PRODUCAO_INICIADA");

    [HttpPost("ordens-producao/{id:long}/concluir")]
    public Task<ActionResult<ApiResponse<object>>> Concluir(long id) => MudarStatusOrdem(id, "CONCLUIDA", "ORDEM_PRODUCAO_CONCLUIDA");

    [HttpPost("ordens-producao/{id:long}/cancelar")]
    public Task<ActionResult<ApiResponse<object>>> Cancelar(long id) => MudarStatusOrdem(id, "CANCELADA", "ORDEM_PRODUCAO_CANCELADA");

    [HttpPost("ordens-producao/{id:long}/apontamentos")]
    public Task<ActionResult<ApiResponse<object>>> Apontar(long id, [FromBody] ApontamentoRequest request) => ApontarAsync(id, request);

    [HttpGet("ordens-producao/{id:long}/apontamentos")]
    public Task<ActionResult<ApiResponse<object>>> Apontamentos(long id) => ListarPorOrdem("sigov.industria_apontamento", id);

    [HttpPost("ordens-producao/{id:long}/consumir-material")]
    public Task<ActionResult<ApiResponse<object>>> Consumir(long id, [FromBody] ConsumoMaterialRequest request) => ConsumirAsync(id, request);

    [HttpPost("ordens-producao/{id:long}/produzir")]
    public Task<ActionResult<ApiResponse<object>>> Produzir(long id, [FromBody] ProducaoAcabadaRequest request) => ProduzirAsync(id, request);

    [HttpPost("ordens-producao/{id:long}/registrar-refugo")]
    public Task<ActionResult<ApiResponse<object>>> Refugo(long id, [FromBody] RefugoRequest request) => RefugoAsync(id, request);

    [HttpGet("qualidade/inspecoes")]
    public Task<ActionResult<ApiResponse<object>>> Inspecoes([FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.industria_inspecao_qualidade", null, page, pageSize, "created_at desc");

    [HttpPost("ordens-producao/{id:long}/inspecao")]
    public Task<ActionResult<ApiResponse<object>>> CriarInspecao(long id, [FromBody] InspecaoRequest request) => InspecaoAsync(id, request);

    [HttpPost("inspecoes/{id:long}/aprovar")]
    public Task<ActionResult<ApiResponse<object>>> Aprovar(long id) => JulgarInspecao(id, "APROVADO");

    [HttpPost("inspecoes/{id:long}/reprovar")]
    public Task<ActionResult<ApiResponse<object>>> Reprovar(long id) => JulgarInspecao(id, "REPROVADO");

    [HttpGet("ordens-producao/{id:long}/custos")]
    public Task<ActionResult<ApiResponse<object>>> Custos(long id) => ListarPorOrdem("sigov.industria_custo_ordem", id);

    [HttpPost("ordens-producao/{id:long}/calcular-custos")]
    public Task<ActionResult<ApiResponse<object>>> CalcularCustos(long id) => CalcularCustosAsync(id);

    [HttpPost("paradas")]
    public Task<ActionResult<ApiResponse<object>>> CriarParada([FromBody] ParadaRequest request) => ParadaAsync(request);

    [HttpPost("paradas/{id:long}/gerar-os")]
    public Task<ActionResult<ApiResponse<object>>> GerarOs(long id) => GerarOsAsync(id);

    [HttpGet("dashboard")]
    public Task<ActionResult<ApiResponse<object>>> Dashboard() => DashboardAsync();

    private async Task<ActionResult<ApiResponse<object>>> UpsertCentroTrabalho(long? id, CentroTrabalhoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission(id.HasValue ? "industria.centros.editar" : "industria.centros.criar")) return Forbid();
            if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome)) return BadRequest(ApiResponse<object>.Fail("Código e nome são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var resultId = id.HasValue
                ? await c.ExecuteScalarAsync<long>("update sigov.industria_centro_trabalho set codigo=@Codigo,nome=@Nome,descricao=@Descricao,ativo=@Ativo,updated_at=now() where id=@Id and tenant_id=@TenantId returning id", new { Id = id, TenantId = tenantId, r.Codigo, r.Nome, r.Descricao, r.Ativo })
                : await c.ExecuteScalarAsync<long>("insert into sigov.industria_centro_trabalho(tenant_id,codigo,nome,descricao,ativo) values(@TenantId,@Codigo,@Nome,@Descricao,@Ativo) returning id", new { TenantId = tenantId, r.Codigo, r.Nome, r.Descricao, r.Ativo });
            await Auditar(c, tenantId, id.HasValue ? "CENTRO_TRABALHO_ATUALIZADO" : "CENTRO_TRABALHO_CRIADO", "industria_centro_trabalho", resultId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = resultId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao salvar centro de trabalho. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao salvar centro de trabalho.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertRecurso(long? id, RecursoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission(id.HasValue ? "industria.recursos.editar" : "industria.recursos.criar")) return Forbid();
            if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome)) return BadRequest(ApiResponse<object>.Fail("Código e nome são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var resultId = id.HasValue
                ? await c.ExecuteScalarAsync<long>("update sigov.industria_recurso set centro_trabalho_id=@CentroTrabalhoId,codigo=@Codigo,nome=@Nome,tipo=@Tipo,custo_hora=@CustoHora,capacidade_hora=@CapacidadeHora,ativo=@Ativo,updated_at=now() where id=@Id and tenant_id=@TenantId returning id", new { Id = id, TenantId = tenantId, r.CentroTrabalhoId, r.Codigo, r.Nome, r.Tipo, r.CustoHora, r.CapacidadeHora, r.Ativo })
                : await c.ExecuteScalarAsync<long>("insert into sigov.industria_recurso(tenant_id,centro_trabalho_id,codigo,nome,tipo,custo_hora,capacidade_hora,ativo) values(@TenantId,@CentroTrabalhoId,@Codigo,@Nome,@Tipo,@CustoHora,@CapacidadeHora,@Ativo) returning id", new { TenantId = tenantId, r.CentroTrabalhoId, r.Codigo, r.Nome, r.Tipo, r.CustoHora, r.CapacidadeHora, r.Ativo });
            await Auditar(c, tenantId, id.HasValue ? "RECURSO_ATUALIZADO" : "RECURSO_CRIADO", "industria_recurso", resultId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = resultId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao salvar recurso. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao salvar recurso.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertProduto(long? id, ProdutoIndustrialRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission(id.HasValue ? "industria.produtos.editar" : "industria.produtos.criar")) return Forbid();
            if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome)) return BadRequest(ApiResponse<object>.Fail("Código e nome são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var resultId = id.HasValue
                ? await c.ExecuteScalarAsync<long>("update sigov.industria_produto set produto_id=@ProdutoId,codigo=@Codigo,nome=@Nome,tipo=@Tipo,unidade=@Unidade,controla_lote=@ControlaLote,controla_validade=@ControlaValidade,exige_ficha_tecnica=@ExigeFichaTecnica,inspecao_obrigatoria=@InspecaoObrigatoria,ativo=@Ativo,updated_at=now() where id=@Id and tenant_id=@TenantId returning id", new { Id = id, TenantId = tenantId, r.ProdutoId, r.Codigo, r.Nome, r.Tipo, r.Unidade, r.ControlaLote, r.ControlaValidade, r.ExigeFichaTecnica, r.InspecaoObrigatoria, r.Ativo })
                : await c.ExecuteScalarAsync<long>("insert into sigov.industria_produto(tenant_id,produto_id,codigo,nome,tipo,unidade,controla_lote,controla_validade,exige_ficha_tecnica,inspecao_obrigatoria,ativo) values(@TenantId,@ProdutoId,@Codigo,@Nome,@Tipo,@Unidade,@ControlaLote,@ControlaValidade,@ExigeFichaTecnica,@InspecaoObrigatoria,@Ativo) returning id", new { TenantId = tenantId, r.ProdutoId, r.Codigo, r.Nome, r.Tipo, r.Unidade, r.ControlaLote, r.ControlaValidade, r.ExigeFichaTecnica, r.InspecaoObrigatoria, r.Ativo });
            await Auditar(c, tenantId, id.HasValue ? "PRODUTO_INDUSTRIAL_ATUALIZADO" : "PRODUTO_INDUSTRIAL_CRIADO", "industria_produto", resultId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = resultId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao salvar produto industrial. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao salvar produto industrial.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertFicha(long? id, FichaTecnicaRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission(id.HasValue ? "industria.fichas.editar" : "industria.fichas.criar")) return Forbid();
            if (string.IsNullOrWhiteSpace(r.Codigo) || r.ProdutoId <= 0) return BadRequest(ApiResponse<object>.Fail("Produto e código são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            if (!await ExisteProduto(c, tenantId, r.ProdutoId)) return BadRequest(ApiResponse<object>.Fail("Produto industrial inválido para o tenant.", cid));
            var resultId = id.HasValue
                ? await c.ExecuteScalarAsync<long>("update sigov.industria_ficha_tecnica set produto_id=@ProdutoId,codigo=@Codigo,versao=@Versao,status=@Status,rendimento=@Rendimento,observacao=@Observacao,updated_at=now() where id=@Id and tenant_id=@TenantId returning id", new { Id = id, TenantId = tenantId, r.ProdutoId, r.Codigo, r.Versao, r.Status, r.Rendimento, r.Observacao })
                : await c.ExecuteScalarAsync<long>("insert into sigov.industria_ficha_tecnica(tenant_id,produto_id,codigo,versao,status,rendimento,observacao) values(@TenantId,@ProdutoId,@Codigo,@Versao,@Status,@Rendimento,@Observacao) returning id", new { TenantId = tenantId, r.ProdutoId, r.Codigo, r.Versao, r.Status, r.Rendimento, r.Observacao });
            await Auditar(c, tenantId, id.HasValue ? "FICHA_TECNICA_ATUALIZADA" : "FICHA_TECNICA_CRIADA", "industria_ficha_tecnica", resultId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = resultId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao salvar ficha técnica. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao salvar ficha técnica.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AtualizarItensFichaAsync(long id, IReadOnlyCollection<FichaTecnicaItemRequest> itens)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.fichas.editar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            if (!await Existe(c, "sigov.industria_ficha_tecnica", tenantId, id)) return NotFound(ApiResponse<object>.Fail("Ficha técnica não encontrada.", cid));
            foreach (var item in itens) if (!await ExisteProduto(c, tenantId, item.ComponenteProdutoId)) return BadRequest(ApiResponse<object>.Fail("Componente inválido para o tenant.", cid));
            await c.ExecuteAsync("delete from sigov.industria_ficha_tecnica_item where ficha_tecnica_id=@Id", new { Id = id });
            foreach (var item in itens) await c.ExecuteAsync("insert into sigov.industria_ficha_tecnica_item(ficha_tecnica_id,componente_produto_id,quantidade,perda_percentual,unidade,obrigatorio,ordem) values(@Id,@ComponenteProdutoId,@Quantidade,@PerdaPercentual,@Unidade,@Obrigatorio,@Ordem)", new { Id = id, item.ComponenteProdutoId, item.Quantidade, item.PerdaPercentual, item.Unidade, item.Obrigatorio, item.Ordem });
            await Auditar(c, tenantId, "FICHA_TECNICA_ATUALIZADA", "industria_ficha_tecnica", id, itens, cid);
            return Ok(ApiResponse<object>.Ok(new { id, itens = itens.Count }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao atualizar itens da ficha. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar itens da ficha.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertRoteiro(long? id, RoteiroRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission(id.HasValue ? "industria.roteiros.editar" : "industria.roteiros.criar")) return Forbid();
            if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome) || r.ProdutoId <= 0) return BadRequest(ApiResponse<object>.Fail("Produto, código e nome são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            if (!await ExisteProduto(c, tenantId, r.ProdutoId)) return BadRequest(ApiResponse<object>.Fail("Produto industrial inválido.", cid));
            var resultId = id.HasValue
                ? await c.ExecuteScalarAsync<long>("update sigov.industria_roteiro set produto_id=@ProdutoId,codigo=@Codigo,nome=@Nome,versao=@Versao,status=@Status,updated_at=now() where id=@Id and tenant_id=@TenantId returning id", new { Id = id, TenantId = tenantId, r.ProdutoId, r.Codigo, r.Nome, r.Versao, r.Status })
                : await c.ExecuteScalarAsync<long>("insert into sigov.industria_roteiro(tenant_id,produto_id,codigo,nome,versao,status) values(@TenantId,@ProdutoId,@Codigo,@Nome,@Versao,@Status) returning id", new { TenantId = tenantId, r.ProdutoId, r.Codigo, r.Nome, r.Versao, r.Status });
            await Auditar(c, tenantId, id.HasValue ? "ROTEIRO_ATUALIZADO" : "ROTEIRO_CRIADO", "industria_roteiro", resultId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = resultId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao salvar roteiro. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao salvar roteiro.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AtualizarOperacoesAsync(long id, IReadOnlyCollection<RoteiroOperacaoRequest> operacoes)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.roteiros.editar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            if (!await Existe(c, "sigov.industria_roteiro", tenantId, id)) return NotFound(ApiResponse<object>.Fail("Roteiro não encontrado.", cid));
            await c.ExecuteAsync("delete from sigov.industria_roteiro_operacao where roteiro_id=@Id", new { Id = id });
            foreach (var op in operacoes) await c.ExecuteAsync("insert into sigov.industria_roteiro_operacao(roteiro_id,centro_trabalho_id,recurso_id,codigo,descricao,tempo_setup_min,tempo_execucao_min,ordem) values(@Id,@CentroTrabalhoId,@RecursoId,@Codigo,@Descricao,@TempoSetupMin,@TempoExecucaoMin,@Ordem)", new { Id = id, op.CentroTrabalhoId, op.RecursoId, op.Codigo, op.Descricao, op.TempoSetupMin, op.TempoExecucaoMin, op.Ordem });
            await Auditar(c, tenantId, "ROTEIRO_ATUALIZADO", "industria_roteiro", id, operacoes, cid);
            return Ok(ApiResponse<object>.Ok(new { id, operacoes = operacoes.Count }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao atualizar operações. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar operações.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> CriarOrdemAsync(OrdemProducaoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.ordens.criar")) return Forbid();
            if (r.ProdutoId <= 0 || r.QuantidadePlanejada <= 0) return BadRequest(ApiResponse<object>.Fail("Produto e quantidade planejada são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var produto = await c.QuerySingleOrDefaultAsync<dynamic>("select ativo, exige_ficha_tecnica from sigov.industria_produto where id=@ProdutoId and tenant_id=@TenantId", new { r.ProdutoId, TenantId = tenantId });
            if (produto is null || produto.ativo == false) return UnprocessableEntity(ApiResponse<object>.Fail("Não é permitido criar ordem para produto inativo ou inexistente.", cid));
            if (produto.exige_ficha_tecnica == true && !r.FichaTecnicaId.HasValue) return UnprocessableEntity(ApiResponse<object>.Fail("Ficha técnica obrigatória para este produto.", cid));
            var numero = string.IsNullOrWhiteSpace(r.Numero) ? $"OP-{DateTime.UtcNow:yyyyMMddHHmmss}" : r.Numero;
            var id = await c.ExecuteScalarAsync<long>("insert into sigov.industria_ordem_producao(tenant_id,numero,produto_id,ficha_tecnica_id,roteiro_id,pedido_id,quantidade_planejada,data_previsao_inicio,data_previsao_fim,observacao) values(@TenantId,@Numero,@ProdutoId,@FichaTecnicaId,@RoteiroId,@PedidoId,@QuantidadePlanejada,@DataPrevisaoInicio,@DataPrevisaoFim,@Observacao) returning id", new { TenantId = tenantId, Numero = numero, r.ProdutoId, r.FichaTecnicaId, r.RoteiroId, r.PedidoId, r.QuantidadePlanejada, r.DataPrevisaoInicio, r.DataPrevisaoFim, r.Observacao });
            await Historico(c, tenantId, id, null, "PLANEJADA", "API", "ORDEM_PRODUCAO_CRIADA", cid);
            await Auditar(c, tenantId, "ORDEM_PRODUCAO_CRIADA", "industria_ordem_producao", id, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id, numero }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao criar OP. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar ordem de produção.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> MudarStatusOrdem(long id, string status, string evento)
    {
        var cid = CorrelationId();
        try
        {
            if (status is "LIBERADA" && !HasPermission("industria.ordens.liberar")) return Forbid();
            if (status is "EM_PRODUCAO" && !HasPermission("industria.ordens.iniciar")) return Forbid();
            if (status is "CONCLUIDA" && !HasPermission("industria.ordens.concluir")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var ordem = await c.QuerySingleOrDefaultAsync<dynamic>("select op.status, op.ficha_tecnica_id, p.exige_ficha_tecnica, p.inspecao_obrigatoria from sigov.industria_ordem_producao op join sigov.industria_produto p on p.id=op.produto_id where op.id=@Id and op.tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            if (ordem is null) return NotFound(ApiResponse<object>.Fail("Ordem não encontrada.", cid));
            string anterior = ordem.status;
            if ((anterior == "CANCELADA" || anterior == "CONCLUIDA") && status != anterior) return UnprocessableEntity(ApiResponse<object>.Fail("Ordem cancelada ou concluída não pode mudar de status.", cid));
            if (status == "LIBERADA" && ordem.exige_ficha_tecnica == true && ordem.ficha_tecnica_id is null) return UnprocessableEntity(ApiResponse<object>.Fail("Não é possível liberar ordem sem ficha técnica.", cid));
            if (status == "EM_PRODUCAO" && anterior is not ("LIBERADA" or "PAUSADA" or "EM_PRODUCAO")) return UnprocessableEntity(ApiResponse<object>.Fail("Ordem precisa estar liberada para iniciar.", cid));
            if (status == "CONCLUIDA")
            {
                var apontamentos = await c.ExecuteScalarAsync<int>("select count(*) from sigov.industria_apontamento where ordem_id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
                if (apontamentos == 0) return UnprocessableEntity(ApiResponse<object>.Fail("Não é possível concluir OP sem apontamento.", cid));
                var pendente = await c.ExecuteScalarAsync<bool>("select exists(select 1 from sigov.industria_inspecao_qualidade where ordem_id=@Id and tenant_id=@TenantId and status='PENDENTE')", new { Id = id, TenantId = tenantId });
                if (ordem.inspecao_obrigatoria == true && pendente) return UnprocessableEntity(ApiResponse<object>.Fail("Inspeção obrigatória pendente bloqueia conclusão.", cid));
            }
            var dataColumn = status == "EM_PRODUCAO" ? ", inicio_at=coalesce(inicio_at,now())" : status == "CONCLUIDA" ? ", fim_at=now()" : string.Empty;
            await c.ExecuteAsync($"update sigov.industria_ordem_producao set status=@Status, updated_at=now(){dataColumn} where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, Status = status });
            await Historico(c, tenantId, id, anterior, status, "API", evento, cid);
            await Auditar(c, tenantId, evento, "industria_ordem_producao", id, new { statusAnterior = anterior, statusNovo = status }, cid);
            return Ok(ApiResponse<object>.Ok(new { id, status }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao mudar status da OP. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao mudar status da ordem.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ApontarAsync(long id, ApontamentoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.apontamentos.criar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            if (!await Existe(c, "sigov.industria_ordem_producao", tenantId, id)) return NotFound(ApiResponse<object>.Fail("OP inválida para apontamento.", cid));
            var apontamentoId = await c.ExecuteScalarAsync<long>("insert into sigov.industria_apontamento(tenant_id,ordem_id,ordem_operacao_id,usuario_id,tipo,origem,inicio_at,fim_at,quantidade_boas,quantidade_refugo,observacao) values(@TenantId,@OrdemId,@OrdemOperacaoId,@UsuarioId,@Tipo,@Origem,@InicioAt,@FimAt,@QuantidadeBoas,@QuantidadeRefugo,@Observacao) returning id", new { TenantId = tenantId, OrdemId = id, r.OrdemOperacaoId, UsuarioId = _user.UsuarioId, r.Tipo, Origem = r.Origem ?? "CHAO_FABRICA", InicioAt = r.InicioAt ?? DateTimeOffset.UtcNow, r.FimAt, r.QuantidadeBoas, r.QuantidadeRefugo, r.Observacao });
            await c.ExecuteAsync("update sigov.industria_ordem_producao set quantidade_produzida=quantidade_produzida+@Boas, quantidade_refugada=quantidade_refugada+@Refugo, updated_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, Boas = r.QuantidadeBoas, Refugo = r.QuantidadeRefugo });
            await Auditar(c, tenantId, "APONTAMENTO_PRODUCAO_REGISTRADO", "industria_apontamento", apontamentoId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = apontamentoId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao apontar produção. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao registrar apontamento.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ConsumirAsync(long id, ConsumoMaterialRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.materiais.consumir")) return Forbid();
            if (r.ProdutoId <= 0 || r.Quantidade <= 0) return BadRequest(ApiResponse<object>.Fail("Produto e quantidade são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            if (!await Existe(c, "sigov.industria_ordem_producao", tenantId, id)) return NotFound(ApiResponse<object>.Fail("OP inválida.", cid));
            var custo = await _estoque.ObterCustoMedioAsync(tenantId, r.ProdutoId, HttpContext.RequestAborted);
            var estoque = await _estoque.ConsumirMaterialAsync(tenantId, id, r.ProdutoId, r.AlmoxarifadoId, r.Quantidade, _user.UsuarioId, cid, HttpContext.RequestAborted);
            var consumoId = await c.ExecuteScalarAsync<long>("insert into sigov.industria_consumo_material(tenant_id,ordem_id,produto_id,almoxarifado_id,quantidade,custo_unitario,origem,usuario_id) values(@TenantId,@OrdemId,@ProdutoId,@AlmoxarifadoId,@Quantidade,@Custo,'OP',@UsuarioId) returning id", new { TenantId = tenantId, OrdemId = id, r.ProdutoId, r.AlmoxarifadoId, r.Quantidade, Custo = custo, UsuarioId = _user.UsuarioId });
            await c.ExecuteAsync("update sigov.industria_ordem_material set quantidade_consumida=quantidade_consumida+@Quantidade where ordem_id=@OrdemId and produto_id=@ProdutoId", new { OrdemId = id, r.ProdutoId, r.Quantidade });
            await Auditar(c, tenantId, "MATERIAL_CONSUMIDO", "industria_consumo_material", consumoId, new { r, estoque }, cid);
            return Ok(ApiResponse<object>.Ok(new { id = consumoId, estoque }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao consumir material. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail(ex.Message, cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ProduzirAsync(long id, ProducaoAcabadaRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.producao.registrar")) return Forbid();
            if (r.ProdutoId <= 0 || r.Quantidade <= 0) return BadRequest(ApiResponse<object>.Fail("Produto e quantidade são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            if (!await Existe(c, "sigov.industria_ordem_producao", tenantId, id)) return NotFound(ApiResponse<object>.Fail("OP inválida.", cid));
            var estoque = await _estoque.RegistrarProdutoAcabadoAsync(tenantId, id, r.ProdutoId, r.AlmoxarifadoId, r.Quantidade, r.Lote, r.Validade, _user.UsuarioId, cid, HttpContext.RequestAborted);
            var producaoId = await c.ExecuteScalarAsync<long>("insert into sigov.industria_producao_acabada(tenant_id,ordem_id,produto_id,almoxarifado_id,quantidade,lote,validade,usuario_id) values(@TenantId,@OrdemId,@ProdutoId,@AlmoxarifadoId,@Quantidade,@Lote,@Validade,@UsuarioId) returning id", new { TenantId = tenantId, OrdemId = id, r.ProdutoId, r.AlmoxarifadoId, r.Quantidade, r.Lote, r.Validade, UsuarioId = _user.UsuarioId });
            await c.ExecuteAsync("update sigov.industria_ordem_producao set quantidade_produzida=quantidade_produzida+@Quantidade, updated_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, r.Quantidade });
            await Auditar(c, tenantId, "PRODUCAO_ACABADA_REGISTRADA", "industria_producao_acabada", producaoId, new { r, estoque }, cid);
            return Ok(ApiResponse<object>.Ok(new { id = producaoId, estoque }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao registrar produção acabada. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao registrar produção acabada.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> RefugoAsync(long id, RefugoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.refugo.registrar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var refugoId = await c.ExecuteScalarAsync<long>("insert into sigov.industria_refugo(tenant_id,ordem_id,produto_id,quantidade,motivo,causa,usuario_id) values(@TenantId,@OrdemId,@ProdutoId,@Quantidade,@Motivo,@Causa,@UsuarioId) returning id", new { TenantId = tenantId, OrdemId = id, r.ProdutoId, r.Quantidade, r.Motivo, r.Causa, UsuarioId = _user.UsuarioId });
            await c.ExecuteAsync("update sigov.industria_ordem_producao set quantidade_refugada=quantidade_refugada+@Quantidade, quantidade_produzida=greatest(quantidade_produzida-@Quantidade,0), updated_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, r.Quantidade });
            await Auditar(c, tenantId, "REFUGO_REGISTRADO", "industria_refugo", refugoId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = refugoId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao registrar refugo. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao registrar refugo.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> InspecaoAsync(long id, InspecaoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.qualidade.inspecionar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var inspecaoId = await c.ExecuteScalarAsync<long>("insert into sigov.industria_inspecao_qualidade(tenant_id,ordem_id,produto_id,status,resultado,observacao,inspecionado_por,inspecionado_at) values(@TenantId,@OrdemId,@ProdutoId,@Status,@Resultado,@Observacao,@UsuarioId,@InspecionadoAt) returning id", new { TenantId = tenantId, OrdemId = id, r.ProdutoId, Status = r.Status ?? "PENDENTE", r.Resultado, r.Observacao, UsuarioId = _user.UsuarioId, InspecionadoAt = r.Resultado is null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow });
            await Auditar(c, tenantId, "INSPECAO_QUALIDADE_REGISTRADA", "industria_inspecao_qualidade", inspecaoId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = inspecaoId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao registrar inspeção. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao registrar inspeção.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> JulgarInspecao(long id, string resultado)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync("update sigov.industria_inspecao_qualidade set status='CONCLUIDA', resultado=@Resultado, inspecionado_por=@UsuarioId, inspecionado_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, Resultado = resultado, UsuarioId = _user.UsuarioId }); await Auditar(c, tenantId, "INSPECAO_QUALIDADE_REGISTRADA", "industria_inspecao_qualidade", id, new { resultado }, cid); return Ok(ApiResponse<object>.Ok(new { id, resultado }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao julgar inspeção. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao julgar inspeção.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ParadaAsync(ParadaRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.paradas.criar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var paradaId = await c.ExecuteScalarAsync<long>("insert into sigov.industria_parada_producao(tenant_id,ordem_id,recurso_id,motivo,inicio_at,fim_at,impacto_minutos) values(@TenantId,@OrdemId,@RecursoId,@Motivo,@InicioAt,@FimAt,@ImpactoMinutos) returning id", new { TenantId = tenantId, r.OrdemId, r.RecursoId, r.Motivo, r.InicioAt, r.FimAt, r.ImpactoMinutos });
            await Auditar(c, tenantId, "PARADA_PRODUCAO_REGISTRADA", "industria_parada_producao", paradaId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = paradaId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao registrar parada. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao registrar parada.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> GerarOsAsync(long id)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var osAtivo = await c.ExecuteScalarAsync<bool>("select exists(select 1 from sigov.tenant_modulo_contratado where tenant_id=@TenantId and modulo_codigo='ordem_servico' and ativo=true and status in ('CONTRATADO','HABILITADO','EM_IMPLANTACAO','BETA'))", new { TenantId = tenantId });
            if (!osAtivo) return StatusCode(403, ApiResponse<object>.Fail("Módulo ordem_servico não contratado.", cid));
            var osId = await c.ExecuteScalarAsync<long>("select nextval(pg_get_serial_sequence('sigov.industria_ordem_producao','id'))");
            await c.ExecuteAsync("update sigov.industria_parada_producao set gerou_os=true, os_id=@OsId where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, OsId = osId });
            await Auditar(c, tenantId, "PARADA_GEROU_OS", "industria_parada_producao", id, new { osId }, cid);
            return Ok(ApiResponse<object>.Ok(new { paradaId = id, osId }, "OS corretiva sinalizada para integração.", cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao gerar OS. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao gerar OS.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> CalcularCustosAsync(long id)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.custos.calcular")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var custoMaterial = await c.ExecuteScalarAsync<decimal?>("select coalesce(sum(quantidade*coalesce(custo_unitario,0)),0) from sigov.industria_consumo_material where tenant_id=@TenantId and ordem_id=@Id", new { TenantId = tenantId, Id = id }) ?? 0m;
            var custoMaquina = await c.ExecuteScalarAsync<decimal?>(@"select coalesce(sum(extract(epoch from (coalesce(a.fim_at, now())-a.inicio_at))/3600 * coalesce(r.custo_hora,0)),0) from sigov.industria_apontamento a left join sigov.industria_ordem_operacao oo on oo.id=a.ordem_operacao_id left join sigov.industria_recurso r on r.id=oo.recurso_id where a.tenant_id=@TenantId and a.ordem_id=@Id", new { TenantId = tenantId, Id = id }) ?? 0m;
            var custoRefugo = await c.ExecuteScalarAsync<decimal?>("select coalesce(sum(quantidade),0) from sigov.industria_refugo where tenant_id=@TenantId and ordem_id=@Id", new { TenantId = tenantId, Id = id }) ?? 0m;
            var quantidade = await c.ExecuteScalarAsync<decimal?>("select nullif(quantidade_produzida,0) from sigov.industria_ordem_producao where tenant_id=@TenantId and id=@Id", new { TenantId = tenantId, Id = id }) ?? 0m;
            var total = custoMaterial + custoMaquina + custoRefugo;
            var custoId = await c.ExecuteScalarAsync<long>("insert into sigov.industria_custo_ordem(tenant_id,ordem_id,custo_material,custo_mao_obra,custo_maquina,custo_refugo,custo_total,custo_unitario) values(@TenantId,@OrdemId,@Material,0,@Maquina,@Refugo,@Total,@Unitario) returning id", new { TenantId = tenantId, OrdemId = id, Material = custoMaterial, Maquina = custoMaquina, Refugo = custoRefugo, Total = total, Unitario = quantidade > 0 ? total / quantidade : (decimal?)null });
            await Auditar(c, tenantId, "CUSTO_ORDEM_CALCULADO", "industria_custo_ordem", custoId, new { id, custoMaterial, custoMaquina, custoRefugo, total }, cid);
            return Ok(ApiResponse<object>.Ok(new { id = custoId, custoMaterial, custoMaquina, custoRefugo, custoTotal = total }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao calcular custos. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao calcular custos.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> DashboardAsync()
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("industria.dashboard.visualizar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var cards = await c.QuerySingleAsync<object>(@"select
count(*) filter(where status='PLANEJADA') as ops_planejadas,
count(*) filter(where status='EM_PRODUCAO') as ops_em_producao,
count(*) filter(where data_previsao_fim < now() and status not in ('CONCLUIDA','CANCELADA')) as ops_atrasadas,
coalesce(sum(quantidade_produzida) filter(where updated_at::date=current_date),0) as quantidade_produzida_hoje,
coalesce(sum(quantidade_refugada) filter(where updated_at::date=current_date),0) as refugo_dia,
(select count(*) from sigov.industria_parada_producao pp where pp.tenant_id=@TenantId and pp.fim_at is null) as recursos_parados,
(select coalesce(avg(custo_unitario),0) from sigov.industria_custo_ordem co where co.tenant_id=@TenantId) as custo_medio_ops,
count(*) filter(where pedido_id is not null and status in ('PLANEJADA','LIBERADA')) as pedidos_aguardando_producao
from sigov.industria_ordem_producao where tenant_id=@TenantId", new { TenantId = tenantId });
            var status = await c.QueryAsync<object>("select status, count(*) quantidade from sigov.industria_ordem_producao where tenant_id=@TenantId group by status order by status", new { TenantId = tenantId });
            var producaoDia = await c.QueryAsync<object>("select created_at::date dia, sum(quantidade) quantidade from sigov.industria_producao_acabada where tenant_id=@TenantId group by created_at::date order by dia desc limit 14", new { TenantId = tenantId });
            return Ok(ApiResponse<object>.Ok(new { cards, status, producaoDia }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro no dashboard indústria. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao carregar dashboard industrial.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> Listar(string tabela, string? busca, int page, int pageSize, string order)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var rows = await c.QueryAsync<object>($"select * from {tabela} t where t.tenant_id=@TenantId and (@Busca is null or t::text ilike '%'||@Busca||'%') order by {order} offset @Offset limit @Limit", new { TenantId = tenantId, Busca = busca, Offset = Offset(page, pageSize), Limit = Limit(pageSize) }); return Ok(ApiResponse<object>.Ok(rows, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar indústria. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar registros.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> Obter(string tabela, long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var row = await c.QuerySingleOrDefaultAsync<object>($"select * from {tabela} where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); return row is null ? NotFound(ApiResponse<object>.Fail("Registro não encontrado.", cid)) : Ok(ApiResponse<object>.Ok(row, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao obter indústria. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter registro.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ObterComFilhos(string tabela, string tabelaFilho, string fk, long id, string nomeFilho)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var row = await c.QuerySingleOrDefaultAsync<object>($"select * from {tabela} where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); if (row is null) return NotFound(ApiResponse<object>.Fail("Registro não encontrado.", cid)); var filhos = await c.QueryAsync<object>($"select * from {tabelaFilho} where {fk}=@Id order by id", new { Id = id }); return Ok(ApiResponse<object>.Ok(new Dictionary<string, object?> { ["registro"] = row, [nomeFilho] = filhos }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao obter composição indústria. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter registro.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ObterOrdem(long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var ordem = await c.QuerySingleOrDefaultAsync<object>("select * from sigov.industria_ordem_producao where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); if (ordem is null) return NotFound(ApiResponse<object>.Fail("OP não encontrada.", cid)); var materiais = await c.QueryAsync<object>("select * from sigov.industria_ordem_material where ordem_id=@Id", new { Id = id }); var operacoes = await c.QueryAsync<object>("select * from sigov.industria_ordem_operacao where ordem_id=@Id", new { Id = id }); var historico = await c.QueryAsync<object>("select * from sigov.industria_ordem_historico where ordem_id=@Id and tenant_id=@TenantId order by created_at", new { Id = id, TenantId = tenantId }); return Ok(ApiResponse<object>.Ok(new { ordem, materiais, operacoes, historico }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao obter OP. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter OP.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ListarPorOrdem(string tabela, long ordemId)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var rows = await c.QueryAsync<object>($"select * from {tabela} where tenant_id=@TenantId and ordem_id=@OrdemId order by id", new { TenantId = tenantId, OrdemId = ordemId }); return Ok(ApiResponse<object>.Ok(rows, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar por OP. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar por OP.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AlterarAtivo(string tabela, long id, bool ativo, string evento)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync($"update {tabela} set ativo=@Ativo, updated_at=now() where id=@Id and tenant_id=@TenantId", new { Ativo = ativo, Id = id, TenantId = tenantId }); await Auditar(c, tenantId, evento, tabela.Split('.').Last(), id, new { ativo }, cid); return Ok(ApiResponse<object>.Ok(new { id, ativo }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao alterar ativo. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao alterar status.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AlterarStatus(string tabela, long id, string status, string evento)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync($"update {tabela} set status=@Status, updated_at=now() where id=@Id and tenant_id=@TenantId", new { Status = status, Id = id, TenantId = tenantId }); await Auditar(c, tenantId, evento, tabela.Split('.').Last(), id, new { status }, cid); return Ok(ApiResponse<object>.Ok(new { id, status }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao alterar status. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao alterar status.", cid)); }
    }

    private static Task<bool> Existe(System.Data.IDbConnection c, string tabela, long tenantId, long id) => c.ExecuteScalarAsync<bool>($"select exists(select 1 from {tabela} where id=@Id and tenant_id=@TenantId)", new { Id = id, TenantId = tenantId });
    private static Task<bool> ExisteProduto(System.Data.IDbConnection c, long tenantId, long id) => c.ExecuteScalarAsync<bool>("select exists(select 1 from sigov.industria_produto where id=@Id and tenant_id=@TenantId and ativo=true)", new { Id = id, TenantId = tenantId });
    private Task Historico(System.Data.IDbConnection c, long tenantId, long ordemId, string? anterior, string novo, string origem, string observacao, string cid) => c.ExecuteAsync("insert into sigov.industria_ordem_historico(tenant_id,ordem_id,status_anterior,status_novo,usuario_id,origem,observacao,correlation_id) values(@TenantId,@OrdemId,@Anterior,@Novo,@UsuarioId,@Origem,@Observacao,cast(@CorrelationId as uuid))", new { TenantId = tenantId, OrdemId = ordemId, Anterior = anterior, Novo = novo, UsuarioId = _user.UsuarioId, Origem = origem, Observacao = observacao, CorrelationId = Guid.TryParse(cid, out var parsed) ? parsed : Guid.NewGuid() });
    private Task Auditar(System.Data.IDbConnection c, long tenantId, string evento, string entidade, long entityId, object payload, string cid) => c.ExecuteAsync("insert into sigov.auditoria_evento(tenant_id,usuario_id,acao,entidade,entidade_id,correlation_id,depois,created_at) values(@TenantId,@UsuarioId,@Evento,@Entidade,@RegistroId,cast(@CorrelationId as uuid),cast(@Payload as jsonb),now())", new { TenantId = tenantId, UsuarioId = _user.UsuarioId, Evento = evento, Entidade = entidade, RegistroId = entityId.ToString(CultureInfo.InvariantCulture), CorrelationId = Guid.TryParse(cid, out var parsed) ? parsed : Guid.NewGuid(), Payload = JsonSerializer.Serialize(payload) });
    private long RequireTenant() => _tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório para operação industrial.");
    private string CorrelationId() => HttpContext.TraceIdentifier;
    private static int Limit(int pageSize) => Math.Clamp(pageSize, 1, 100);
    private static int Offset(int page, int pageSize) => (Math.Max(1, page) - 1) * Limit(pageSize);
    private bool HasPermission(string permission) => User.Identity?.IsAuthenticated != true || User.IsInRole("ADMIN_GERAL") || User.IsInRole("ADMIN_TENANT") || User.Claims.Any(c => (c.Type == "permission" || c.Type == ClaimTypes.Role) && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
}

public sealed record IndustriaStatusRequest(bool Ativo);
public sealed record CentroTrabalhoRequest(string Codigo, string Nome, string? Descricao, bool Ativo = true);
public sealed record RecursoRequest(long? CentroTrabalhoId, string Codigo, string Nome, string Tipo, decimal? CustoHora, decimal? CapacidadeHora, bool Ativo = true);
public sealed record ProdutoIndustrialRequest(long? ProdutoId, string Codigo, string Nome, string Tipo, string Unidade, bool ControlaLote, bool ControlaValidade, bool ExigeFichaTecnica = true, bool InspecaoObrigatoria = false, bool Ativo = true);
public sealed record FichaTecnicaRequest(long ProdutoId, string Codigo, string Versao, string Status, decimal Rendimento, string? Observacao);
public sealed record FichaTecnicaItemRequest(long ComponenteProdutoId, decimal Quantidade, decimal PerdaPercentual, string Unidade, bool Obrigatorio, int Ordem);
public sealed record RoteiroRequest(long ProdutoId, string Codigo, string Nome, string Versao, string Status);
public sealed record RoteiroOperacaoRequest(long? CentroTrabalhoId, long? RecursoId, string Codigo, string Descricao, decimal TempoSetupMin, decimal TempoExecucaoMin, int Ordem);
public sealed record OrdemProducaoRequest(string? Numero, long ProdutoId, long? FichaTecnicaId, long? RoteiroId, long? PedidoId, decimal QuantidadePlanejada, DateTimeOffset? DataPrevisaoInicio, DateTimeOffset? DataPrevisaoFim, string? Observacao);
public sealed record ApontamentoRequest(long? OrdemOperacaoId, string Tipo, DateTimeOffset? InicioAt, DateTimeOffset? FimAt, decimal QuantidadeBoas, decimal QuantidadeRefugo, string? Origem, string? Observacao);
public sealed record ConsumoMaterialRequest(long ProdutoId, long? AlmoxarifadoId, decimal Quantidade);
public sealed record ProducaoAcabadaRequest(long ProdutoId, long? AlmoxarifadoId, decimal Quantidade, string? Lote, DateTime? Validade);
public sealed record RefugoRequest(long? ProdutoId, decimal Quantidade, string? Motivo, string? Causa);
public sealed record InspecaoRequest(long? ProdutoId, string? Status, string? Resultado, string? Observacao);
public sealed record ParadaRequest(long? OrdemId, long? RecursoId, string Motivo, DateTimeOffset InicioAt, DateTimeOffset? FimAt, decimal? ImpactoMinutos);
