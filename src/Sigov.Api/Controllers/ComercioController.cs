using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Application.Comercio;
using Sigov.Infrastructure.Persistence.Dapper;
using System.Globalization;
using System.Security.Claims;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/comercio")]
[RequireModule("comercial")]
public sealed class ComercioController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IComercioEstoqueService _estoque;
    private readonly ILogger<ComercioController> _logger;

    public ComercioController(DapperContext context, ICurrentTenant tenant, ICurrentUser user, IComercioEstoqueService estoque, ILogger<ComercioController> logger)
    {
        _context = context;
        _tenant = tenant;
        _user = user;
        _estoque = estoque;
        _logger = logger;
    }

    [HttpGet("clientes")]
    public Task<ActionResult<ApiResponse<object>>> Clientes([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarClientes(busca, page, pageSize);

    [HttpGet("clientes/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Cliente(long id) => Obter("sigov.comercio_cliente", id);

    [HttpPost("clientes")]
    public Task<ActionResult<ApiResponse<object>>> CriarCliente([FromBody] ClienteRequest request) => UpsertCliente(null, request);

    [HttpPut("clientes/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarCliente(long id, [FromBody] ClienteRequest request) => UpsertCliente(id, request);

    [HttpPatch("clientes/{id:long}/status")]
    public Task<ActionResult<ApiResponse<object>>> StatusCliente(long id, [FromBody] StatusRequest request) => AlterarStatus("sigov.comercio_cliente", id, request.Ativo, "CLIENTE_ATUALIZADO");

    [HttpGet("produtos")]
    public Task<ActionResult<ApiResponse<object>>> Produtos([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastro("sigov.comercio_produto", busca, page, pageSize, "codigo,nome");

    [HttpGet("produtos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Produto(long id) => Obter("sigov.comercio_produto", id);

    [HttpPost("produtos")]
    public Task<ActionResult<ApiResponse<object>>> CriarProduto([FromBody] ProdutoRequest request) => UpsertProduto(null, request);

    [HttpPut("produtos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarProduto(long id, [FromBody] ProdutoRequest request) => UpsertProduto(id, request);

    [HttpPatch("produtos/{id:long}/status")]
    public Task<ActionResult<ApiResponse<object>>> StatusProduto(long id, [FromBody] StatusRequest request) => AlterarStatus("sigov.comercio_produto", id, request.Ativo, "PRODUTO_ATUALIZADO");

    [HttpGet("orcamentos")]
    public Task<ActionResult<ApiResponse<object>>> Orcamentos([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastro("sigov.comercio_orcamento", busca, page, pageSize, "created_at desc");

    [HttpGet("orcamentos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Orcamento(long id) => ObterComItens("sigov.comercio_orcamento", "sigov.comercio_orcamento_item", "orcamento_id", id);

    [HttpPost("orcamentos")]
    public Task<ActionResult<ApiResponse<object>>> CriarOrcamento([FromBody] DocumentoComercialRequest request) => CriarDocumento("orcamento", request, "ORCAMENTO_CRIADO");

    [HttpPut("orcamentos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarOrcamento(long id, [FromBody] DocumentoComercialRequest request) => AtualizarTotais("sigov.comercio_orcamento", id, request, "ORCAMENTO_CRIADO");

    [HttpPost("orcamentos/{id:long}/aprovar")]
    public Task<ActionResult<ApiResponse<object>>> AprovarOrcamento(long id) => AlterarStatusDocumento("sigov.comercio_orcamento", id, "APROVADO", "aprovado_at", "ORCAMENTO_APROVADO");

    [HttpPost("orcamentos/{id:long}/reprovar")]
    public Task<ActionResult<ApiResponse<object>>> ReprovarOrcamento(long id) => AlterarStatusDocumento("sigov.comercio_orcamento", id, "REPROVADO", "reprovado_at", "ORCAMENTO_REPROVADO");

    [HttpPost("orcamentos/{id:long}/gerar-pedido")]
    public Task<ActionResult<ApiResponse<object>>> GerarPedido(long id) => GerarPedidoDeOrcamento(id);

    [HttpGet("pedidos")]
    public Task<ActionResult<ApiResponse<object>>> Pedidos([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastro("sigov.comercio_pedido", busca, page, pageSize, "created_at desc");

    [HttpGet("pedidos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Pedido(long id) => ObterComItens("sigov.comercio_pedido", "sigov.comercio_pedido_item", "pedido_id", id);

    [HttpPost("pedidos")]
    public Task<ActionResult<ApiResponse<object>>> CriarPedido([FromBody] DocumentoComercialRequest request) => CriarDocumento("pedido", request, "PEDIDO_CRIADO");

    [HttpPut("pedidos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarPedido(long id, [FromBody] DocumentoComercialRequest request) => AtualizarTotais("sigov.comercio_pedido", id, request, "PEDIDO_CRIADO");

    [HttpPost("pedidos/{id:long}/confirmar")]
    public Task<ActionResult<ApiResponse<object>>> ConfirmarPedido(long id) => ConfirmarPedidoAsync(id);

    [HttpPost("pedidos/{id:long}/separar")]
    public Task<ActionResult<ApiResponse<object>>> SepararPedido(long id) => SepararPedidoAsync(id);

    [HttpPost("pedidos/{id:long}/faturar")]
    public Task<ActionResult<ApiResponse<object>>> FaturarPedido(long id) => FaturarPedidoAsync(id);

    [HttpPost("pedidos/{id:long}/cancelar")]
    public Task<ActionResult<ApiResponse<object>>> CancelarPedido(long id) => AlterarStatusDocumento("sigov.comercio_pedido", id, "CANCELADO", "cancelado_at", "PEDIDO_CANCELADO");

    [HttpPost("pedidos/{id:long}/gerar-os")]
    [RequireModule("ordem_servico")]
    public Task<ActionResult<ApiResponse<object>>> PedidoGerarOs(long id) => GerarOsPedido(id);

    [HttpGet("vendas")]
    public Task<ActionResult<ApiResponse<object>>> Vendas([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastro("sigov.comercio_venda", busca, page, pageSize, "created_at desc");

    [HttpGet("vendas/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Venda(long id) => ObterComItens("sigov.comercio_venda", "sigov.comercio_venda_item", "venda_id", id);

    [HttpPost("vendas")]
    public Task<ActionResult<ApiResponse<object>>> CriarVenda([FromBody] VendaRequest request) => CriarVendaAsync(request);

    [HttpPost("vendas/{id:long}/itens")]
    public Task<ActionResult<ApiResponse<object>>> AdicionarItemVenda(long id, [FromBody] ItemRequest request) => AdicionarItem("venda", id, request);

    [HttpDelete("vendas/{id:long}/itens/{itemId:long}")]
    public Task<ActionResult<ApiResponse<object>>> RemoverItemVenda(long id, long itemId) => RemoverItemVendaAsync(id, itemId);

    [HttpPost("vendas/{id:long}/recebimentos")]
    public Task<ActionResult<ApiResponse<object>>> RecebimentoVenda(long id, [FromBody] RecebimentoRequest request) => RegistrarRecebimento(id, null, request);

    [HttpPost("vendas/{id:long}/finalizar")]
    public Task<ActionResult<ApiResponse<object>>> FinalizarVenda(long id) => FinalizarVendaAsync(id);

    [HttpPost("vendas/{id:long}/cancelar")]
    public Task<ActionResult<ApiResponse<object>>> CancelarVenda(long id) => CancelarVendaAsync(id);

    [HttpGet("caixas")]
    public Task<ActionResult<ApiResponse<object>>> Caixas([FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastro("sigov.comercio_caixa", null, page, pageSize, "aberto_at desc");

    [HttpGet("caixas/aberto")]
    public Task<ActionResult<ApiResponse<object>>> CaixaAberto() => ObterCaixaAberto();

    [HttpPost("caixas/abrir")]
    public Task<ActionResult<ApiResponse<object>>> AbrirCaixa([FromBody] CaixaAbrirRequest request) => AbrirCaixaAsync(request);

    [HttpPost("caixas/{id:long}/suprimento")]
    public Task<ActionResult<ApiResponse<object>>> Suprimento(long id, [FromBody] CaixaMovimentoRequest request) => MovimentoCaixa(id, "SUPRIMENTO", request, "CAIXA_SUPRIMENTO");

    [HttpPost("caixas/{id:long}/sangria")]
    public Task<ActionResult<ApiResponse<object>>> Sangria(long id, [FromBody] CaixaMovimentoRequest request) => MovimentoCaixa(id, "SANGRIA", request, "CAIXA_SANGRIA");

    [HttpPost("caixas/{id:long}/fechar")]
    public Task<ActionResult<ApiResponse<object>>> FecharCaixa(long id, [FromBody] CaixaFecharRequest request) => FecharCaixaAsync(id, request);

    [HttpGet("tabelas-preco")]
    public Task<ActionResult<ApiResponse<object>>> TabelasPreco([FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastro("sigov.comercio_tabela_preco", null, page, pageSize, "nome");

    [HttpPost("tabelas-preco")]
    public Task<ActionResult<ApiResponse<object>>> CriarTabela([FromBody] TabelaPrecoRequest request) => UpsertTabela(null, request);

    [HttpPut("tabelas-preco/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarTabela(long id, [FromBody] TabelaPrecoRequest request) => UpsertTabela(id, request);

    [HttpPut("tabelas-preco/{id:long}/itens")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarItensTabela(long id, [FromBody] IReadOnlyCollection<TabelaPrecoItemRequest> itens) => AtualizarItensTabelaAsync(id, itens);

    [HttpGet("comissoes")]
    public Task<ActionResult<ApiResponse<object>>> Comissoes([FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastro("sigov.comercio_comissao", null, page, pageSize, "created_at desc");

    [HttpPost("comissoes/calcular")]
    public Task<ActionResult<ApiResponse<object>>> CalcularComissoes([FromBody] CalcularComissaoRequest request) => CalcularComissoesAsync(request);

    [HttpPost("comissoes/{id:long}/marcar-paga")]
    public Task<ActionResult<ApiResponse<object>>> MarcarComissaoPaga(long id) => AlterarStatusDocumento("sigov.comercio_comissao", id, "PAGA", "paga_at", "COMISSAO_PAGA");

    private async Task<ActionResult<ApiResponse<object>>> ListarClientes(string? busca, int page, int pageSize)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var rows = await c.QueryAsync<object>(@"select id, tenant_id, nome, tipo_pessoa,
case when documento is null then null else concat('***', right(documento,4)) end as documento,
case when email is null then null else concat(left(email,1),'***@', split_part(email,'@',2)) end as email,
case when telefone is null then null else concat('***', right(telefone,4)) end as telefone,
limite_credito, ativo, created_at, updated_at
from sigov.comercio_cliente where tenant_id=@TenantId and (@Busca is null or nome ilike '%'||@Busca||'%' or documento ilike '%'||@Busca||'%')
order by nome offset @Offset limit @Limit", new { TenantId = tenantId, Busca = busca, Offset = Offset(page, pageSize), Limit = Limit(pageSize) });
            return Ok(ApiResponse<object>.Ok(rows, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar clientes. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar clientes.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertCliente(long? id, ClienteRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission(id.HasValue ? "comercio.clientes.editar" : "comercio.clientes.criar")) return Forbid();
            if (string.IsNullOrWhiteSpace(r.Nome)) return BadRequest(ApiResponse<object>.Fail("Nome é obrigatório.", cid));
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var resultId = id.HasValue
                ? await c.ExecuteScalarAsync<long>("update sigov.comercio_cliente set nome=@Nome,tipo_pessoa=@TipoPessoa,documento=@Documento,email=@Email,telefone=@Telefone,endereco_json=cast(@EnderecoJson as jsonb),limite_credito=@LimiteCredito,updated_at=now() where id=@Id and tenant_id=@TenantId returning id", new { Id = id, TenantId = tenantId, r.Nome, r.TipoPessoa, r.Documento, r.Email, r.Telefone, EnderecoJson = r.EnderecoJson ?? "null", r.LimiteCredito })
                : await c.ExecuteScalarAsync<long>("insert into sigov.comercio_cliente(tenant_id,nome,tipo_pessoa,documento,email,telefone,endereco_json,limite_credito) values(@TenantId,@Nome,@TipoPessoa,@Documento,@Email,@Telefone,cast(@EnderecoJson as jsonb),@LimiteCredito) returning id", new { TenantId = tenantId, r.Nome, r.TipoPessoa, r.Documento, r.Email, r.Telefone, EnderecoJson = r.EnderecoJson ?? "null", r.LimiteCredito });
            await Auditar(c, tenantId, id.HasValue ? "CLIENTE_ATUALIZADO" : "CLIENTE_CRIADO", resultId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = resultId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao salvar cliente. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao salvar cliente.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertProduto(long? id, ProdutoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission(id.HasValue ? "comercio.produtos.editar" : "comercio.produtos.criar")) return Forbid();
            if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome)) return BadRequest(ApiResponse<object>.Fail("Código e nome são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var resultId = id.HasValue
                ? await c.ExecuteScalarAsync<long>("update sigov.comercio_produto set codigo=@Codigo,nome=@Nome,descricao=@Descricao,unidade=@Unidade,codigo_barras=@CodigoBarras,preco_venda=@PrecoVenda,preco_custo=@PrecoCusto,controla_estoque=@ControlaEstoque,gera_os=@GeraOs,updated_at=now() where id=@Id and tenant_id=@TenantId returning id", new { Id = id, TenantId = tenantId, r.Codigo, r.Nome, r.Descricao, Unidade = r.Unidade ?? "UN", r.CodigoBarras, r.PrecoVenda, r.PrecoCusto, r.ControlaEstoque, r.GeraOs })
                : await c.ExecuteScalarAsync<long>("insert into sigov.comercio_produto(tenant_id,codigo,nome,descricao,unidade,codigo_barras,preco_venda,preco_custo,controla_estoque,gera_os) values(@TenantId,@Codigo,@Nome,@Descricao,@Unidade,@CodigoBarras,@PrecoVenda,@PrecoCusto,@ControlaEstoque,@GeraOs) returning id", new { TenantId = tenantId, r.Codigo, r.Nome, r.Descricao, Unidade = r.Unidade ?? "UN", r.CodigoBarras, r.PrecoVenda, r.PrecoCusto, r.ControlaEstoque, r.GeraOs });
            await Auditar(c, tenantId, id.HasValue ? "PRODUTO_ATUALIZADO" : "PRODUTO_CRIADO", resultId, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id = resultId }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao salvar produto. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao salvar produto.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> CriarVendaAsync(VendaRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("comercio.vendas.criar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            if (string.Equals(r.Tipo, "PDV", StringComparison.OrdinalIgnoreCase))
            {
                var caixaAberto = await c.ExecuteScalarAsync<long>("select count(1) from sigov.comercio_caixa where tenant_id=@TenantId and status='ABERTO' and (@CaixaId is null or id=@CaixaId)", new { TenantId = tenantId, r.CaixaId });
                if (caixaAberto == 0) return UnprocessableEntity(ApiResponse<object>.Fail("PDV exige caixa aberto.", cid));
            }

            var numero = string.IsNullOrWhiteSpace(r.Numero) ? $"V-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}" : r.Numero.Trim();
            var id = await c.ExecuteScalarAsync<long>("insert into sigov.comercio_venda(tenant_id,caixa_id,cliente_id,vendedor_id,numero,tipo,subtotal,desconto,acrescimo,total,observacao) values(@TenantId,@CaixaId,@ClienteId,@VendedorId,@Numero,@Tipo,0,0,0,0,@Observacao) returning id", new { TenantId = tenantId, r.CaixaId, r.ClienteId, r.VendedorId, Numero = numero, Tipo = r.Tipo ?? "BALCAO", r.Observacao });
            foreach (var item in r.Itens ?? Array.Empty<ItemRequest>()) await InserirItemVenda(c, tenantId, id, item);
            await RecalcularVenda(c, tenantId, id);
            await Auditar(c, tenantId, "VENDA_CRIADA", id, r, cid);
            return Ok(ApiResponse<object>.Ok(new { id, numero }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao criar venda. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar venda.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> FinalizarVendaAsync(long id)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("comercio.vendas.finalizar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var resumo = await c.QuerySingleOrDefaultAsync<VendaResumo>("select id,total,status,tipo,caixa_id as CaixaId, estoque_baixado as EstoqueBaixado from sigov.comercio_venda where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            if (resumo is null) return NotFound(ApiResponse<object>.Fail("Venda não encontrada.", cid));
            var itens = (await c.QueryAsync<ComercioEstoqueItem>("select produto_id as ProdutoId, quantidade as Quantidade, 'VENDA' as Origem, id as OrigemId from sigov.comercio_venda_item where venda_id=@Id", new { Id = id })).AsList();
            if (itens.Count == 0) return UnprocessableEntity(ApiResponse<object>.Fail("Não é permitido finalizar venda sem item.", cid));
            var recebido = await c.ExecuteScalarAsync<decimal>("select coalesce(sum(valor),0) from sigov.comercio_recebimento where tenant_id=@TenantId and venda_id=@Id", new { Id = id, TenantId = tenantId });
            if (recebido < resumo.Total) return UnprocessableEntity(ApiResponse<object>.Fail("Não é permitido finalizar venda sem pagamento total.", cid));
            if (string.Equals(resumo.Tipo, "PDV", StringComparison.OrdinalIgnoreCase) && resumo.CaixaId is null) return UnprocessableEntity(ApiResponse<object>.Fail("PDV exige caixa aberto.", cid));
            await _estoque.BaixarEstoqueVendaAsync(tenantId, id, itens, HttpContext.RequestAborted);
            await c.ExecuteAsync("update sigov.comercio_venda set status='FINALIZADA', estoque_baixado=true, finalizada_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            await Auditar(c, tenantId, "ESTOQUE_BAIXADO_VENDA", id, itens, cid);
            await Auditar(c, tenantId, "VENDA_FINALIZADA", id, resumo, cid);
            return Ok(ApiResponse<object>.Ok(new { id, status = "FINALIZADA" }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao finalizar venda. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao finalizar venda.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> CancelarVendaAsync(long id)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("comercio.vendas.cancelar")) return Forbid();
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var baixado = await c.ExecuteScalarAsync<bool>("select estoque_baixado from sigov.comercio_venda where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            if (baixado)
            {
                var itens = (await c.QueryAsync<ComercioEstoqueItem>("select produto_id as ProdutoId, quantidade as Quantidade, 'VENDA' as Origem, id as OrigemId from sigov.comercio_venda_item where venda_id=@Id", new { Id = id })).AsList();
                await _estoque.EstornarEstoqueVendaAsync(tenantId, id, itens, HttpContext.RequestAborted);
                await Auditar(c, tenantId, "ESTOQUE_ESTORNADO_VENDA", id, itens, cid);
            }

            await c.ExecuteAsync("update sigov.comercio_venda set status='CANCELADA', cancelada_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            await Auditar(c, tenantId, "VENDA_CANCELADA", id, new { id }, cid);
            return Ok(ApiResponse<object>.Ok(new { id, status = "CANCELADA" }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao cancelar venda. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao cancelar venda.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ConfirmarPedidoAsync(long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync("update sigov.comercio_pedido set status='CONFIRMADO', estoque_reservado=true, confirmado_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); var itens = (await c.QueryAsync<ComercioEstoqueItem>("select produto_id as ProdutoId, quantidade as Quantidade, 'PEDIDO' as Origem, id as OrigemId from sigov.comercio_pedido_item where pedido_id=@Id", new { Id = id })).AsList(); await _estoque.ReservarEstoqueAsync(tenantId, id, itens, HttpContext.RequestAborted); await Auditar(c, tenantId, "PEDIDO_CONFIRMADO", id, itens, cid); return Ok(ApiResponse<object>.Ok(new { id, status = "CONFIRMADO" }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao confirmar pedido. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao confirmar pedido.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> SepararPedidoAsync(long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var separacaoId = await c.ExecuteScalarAsync<long>("insert into sigov.comercio_separacao(tenant_id,pedido_id,status,responsavel_id) values(@TenantId,@Id,'ABERTA',@UsuarioId) on conflict(tenant_id,pedido_id) do update set status='ABERTA' returning id", new { TenantId = tenantId, Id = id, UsuarioId = _user.UsuarioId }); await c.ExecuteAsync("insert into sigov.comercio_separacao_item(separacao_id,pedido_item_id,produto_id,quantidade_solicitada) select @SeparacaoId,id,produto_id,quantidade from sigov.comercio_pedido_item where pedido_id=@Id on conflict do nothing", new { SeparacaoId = separacaoId, Id = id }); await c.ExecuteAsync("update sigov.comercio_pedido set status='EM_SEPARACAO', separado_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); return Ok(ApiResponse<object>.Ok(new { id, separacaoId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao separar pedido. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao separar pedido.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> FaturarPedidoAsync(long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var pedido = await c.QuerySingleAsync<dynamic>("select id, cliente_id, numero, total from sigov.comercio_pedido where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); await c.ExecuteAsync("update sigov.comercio_pedido set status='FATURADO', faturado_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); var contaId = await GerarContaReceber(c, tenantId, "PEDIDO", id, (long?)pedido.cliente_id, (string?)pedido.numero, (decimal)pedido.total, cid); await Auditar(c, tenantId, "CONTA_RECEBER_GERADA", contaId, new { pedidoId = id }, cid); return Ok(ApiResponse<object>.Ok(new { id, contaId, status = "FATURADO" }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao faturar pedido. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao faturar pedido.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> RegistrarRecebimento(long? vendaId, long? pedidoId, RecebimentoRequest r)
    {
        var cid = CorrelationId();
        try { if (r.Valor <= 0) return BadRequest(ApiResponse<object>.Fail("Valor deve ser maior que zero.", cid)); var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var id = await c.ExecuteScalarAsync<long>("insert into sigov.comercio_recebimento(tenant_id,venda_id,pedido_id,forma_pagamento_id,valor,status,vencimento,recebido_at) values(@TenantId,@VendaId,@PedidoId,@FormaPagamentoId,@Valor,@Status,@Vencimento,case when @Status='RECEBIDO' then now() else null end) returning id", new { TenantId = tenantId, VendaId = vendaId, PedidoId = pedidoId, r.FormaPagamentoId, r.Valor, Status = r.Status ?? "RECEBIDO", r.Vencimento }); return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao registrar recebimento. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao registrar recebimento.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AbrirCaixaAsync(CaixaAbrirRequest r)
    {
        var cid = CorrelationId();
        try { if (!HasPermission("comercio.caixa.abrir")) return Forbid(); var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var aberto = await c.ExecuteScalarAsync<long>("select count(1) from sigov.comercio_caixa where tenant_id=@TenantId and status='ABERTO'", new { TenantId = tenantId }); if (aberto > 0) return UnprocessableEntity(ApiResponse<object>.Fail("Já existe caixa aberto para o tenant.", cid)); var id = await c.ExecuteScalarAsync<long>("insert into sigov.comercio_caixa(tenant_id,usuario_abertura_id,valor_abertura,observacao) values(@TenantId,@UsuarioId,@ValorAbertura,@Observacao) returning id", new { TenantId = tenantId, UsuarioId = _user.UsuarioId, r.ValorAbertura, r.Observacao }); await Auditar(c, tenantId, "CAIXA_ABERTO", id, r, cid); return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao abrir caixa. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao abrir caixa.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> FecharCaixaAsync(long id, CaixaFecharRequest r)
    {
        var cid = CorrelationId();
        try { if (!HasPermission("comercio.caixa.fechar")) return Forbid(); var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var resumo = await c.QueryAsync<object>("select forma_pagamento_id, sum(valor) valor from sigov.comercio_recebimento where tenant_id=@TenantId and venda_id in (select id from sigov.comercio_venda where caixa_id=@Id) group by forma_pagamento_id", new { TenantId = tenantId, Id = id }); await c.ExecuteAsync("update sigov.comercio_caixa set status='FECHADO', usuario_fechamento_id=@UsuarioId, valor_fechamento=@ValorFechamento, fechado_at=now(), observacao=coalesce(@Observacao,observacao) where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, UsuarioId = _user.UsuarioId, r.ValorFechamento, r.Observacao }); await Auditar(c, tenantId, "CAIXA_FECHADO", id, r, cid); return Ok(ApiResponse<object>.Ok(new { id, resumo }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao fechar caixa. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao fechar caixa.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> MovimentoCaixa(long id, string tipo, CaixaMovimentoRequest r, string evento)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var movimentoId = await c.ExecuteScalarAsync<long>("insert into sigov.comercio_caixa_movimento(tenant_id,caixa_id,tipo,valor,observacao) values(@TenantId,@CaixaId,@Tipo,@Valor,@Observacao) returning id", new { TenantId = tenantId, CaixaId = id, Tipo = tipo, r.Valor, r.Observacao }); await Auditar(c, tenantId, evento, movimentoId, r, cid); return Ok(ApiResponse<object>.Ok(new { id = movimentoId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro no movimento de caixa. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha no movimento de caixa.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ObterCaixaAberto()
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var row = await c.QuerySingleOrDefaultAsync<object>("select id, tenant_id, usuario_abertura_id, usuario_fechamento_id, status, valor_abertura, valor_fechamento, aberto_at, fechado_at, observacao from sigov.comercio_caixa where tenant_id=@TenantId and status='ABERTO' order by aberto_at desc limit 1", new { TenantId = tenantId }); return row is null ? NotFound(ApiResponse<object>.Fail("Nenhum caixa aberto.", cid)) : Ok(ApiResponse<object>.Ok(row, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao obter caixa aberto. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter caixa.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> CriarDocumento(string tipo, DocumentoComercialRequest r, string evento)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var numero = string.IsNullOrWhiteSpace(r.Numero) ? $"{tipo[..1].ToUpperInvariant()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}" : r.Numero.Trim(); var tabela = tipo == "pedido" ? "sigov.comercio_pedido" : "sigov.comercio_orcamento"; var id = await c.ExecuteScalarAsync<long>($"insert into {tabela}(tenant_id,cliente_id,vendedor_id,tabela_preco_id,numero,subtotal,desconto,acrescimo,total,observacao) values(@TenantId,@ClienteId,@VendedorId,@TabelaPrecoId,@Numero,@Subtotal,@Desconto,@Acrescimo,@Total,@Observacao) returning id", new { TenantId = tenantId, r.ClienteId, r.VendedorId, r.TabelaPrecoId, Numero = numero, r.Subtotal, r.Desconto, r.Acrescimo, r.Total, r.Observacao }); foreach (var item in r.Itens ?? Array.Empty<ItemRequest>()) await InserirItemDocumento(c, tipo, id, item); await Auditar(c, tenantId, evento, id, r, cid); return Ok(ApiResponse<object>.Ok(new { id, numero }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao criar documento comercial. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar documento comercial.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> GerarPedidoDeOrcamento(long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var pedidoId = await c.ExecuteScalarAsync<long>(@"insert into sigov.comercio_pedido(tenant_id,cliente_id,vendedor_id,tabela_preco_id,orcamento_id,numero,subtotal,desconto,acrescimo,total,observacao)
select tenant_id,cliente_id,vendedor_id,tabela_preco_id,id,concat('P-',numero),subtotal,desconto,acrescimo,total,observacao from sigov.comercio_orcamento where id=@Id and tenant_id=@TenantId returning id", new { Id = id, TenantId = tenantId }); await c.ExecuteAsync("insert into sigov.comercio_pedido_item(pedido_id,produto_id,descricao,quantidade,valor_unitario,desconto,total) select @PedidoId,produto_id,descricao,quantidade,valor_unitario,desconto,total from sigov.comercio_orcamento_item where orcamento_id=@Id", new { PedidoId = pedidoId, Id = id }); return Ok(ApiResponse<object>.Ok(new { id = pedidoId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao gerar pedido. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao gerar pedido.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> GerarOsPedido(long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var osId = await c.ExecuteScalarAsync<Guid>("insert into sigov.os_ordem_servico(tenant_id,numero,status,descricao) values('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid,@Numero,'ABERTA','OS gerada por pedido comercial') returning id", new { Numero = $"OS-PED-{id}" }); await Auditar(c, tenantId, "PEDIDO_GEROU_OS", id, new { osId }, cid); return Ok(ApiResponse<object>.Ok(new { id, osId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao gerar OS do pedido. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao gerar OS.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> CalcularComissoesAsync(CalcularComissaoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var venda = await c.QuerySingleOrDefaultAsync<ComissaoVenda>("select id as Id,total as Total,vendedor_id as VendedorId,status as Status from sigov.comercio_venda where id=@VendaId and tenant_id=@TenantId", new { r.VendaId, TenantId = tenantId });

            if (venda is null)
            {
                return NotFound(ApiResponse<object>.Fail("Venda não encontrada.", cid));
            }

            if (!string.Equals(venda.Status, "FINALIZADA", StringComparison.OrdinalIgnoreCase))
            {
                return UnprocessableEntity(ApiResponse<object>.Fail("Comissão calculada apenas sobre venda finalizada.", cid));
            }

            var percentual = r.Percentual <= 0 ? 1 : r.Percentual;
            var valor = venda.Total * percentual / 100m;
            var id = await c.ExecuteScalarAsync<long>("insert into sigov.comercio_comissao(tenant_id,venda_id,vendedor_id,base_calculo,percentual,valor) values(@TenantId,@VendaId,@VendedorId,@Base,@Percentual,@Valor) returning id", new { TenantId = tenantId, r.VendaId, VendedorId = venda.VendedorId, Base = venda.Total, Percentual = percentual, Valor = valor });
            return Ok(ApiResponse<object>.Ok(new { id, valor }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao calcular comissão. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao calcular comissão.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ListarCadastro(string tabela, string? busca, int page, int pageSize, string order)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var rows = await c.QueryAsync<object>($"select {Projection(tabela)} from {tabela} where tenant_id=@TenantId order by {order} offset @Offset limit @Limit", new { TenantId = tenantId, Busca = busca, Offset = Offset(page, pageSize), Limit = Limit(pageSize) }); return Ok(ApiResponse<object>.Ok(rows, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar cadastro comércio. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar cadastro.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> Obter(string tabela, long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var row = await c.QuerySingleOrDefaultAsync<object>($"select {Projection(tabela)} from {tabela} where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); return row is null ? NotFound(ApiResponse<object>.Fail("Registro não encontrado.", cid)) : Ok(ApiResponse<object>.Ok(row, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao obter comércio. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter registro.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ObterComItens(string tabela, string itensTabela, string fk, long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var row = await c.QuerySingleOrDefaultAsync<object>($"select {Projection(tabela)} from {tabela} where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); var itens = await c.QueryAsync<object>($"select {Projection(itensTabela)} from {itensTabela} where {fk}=@Id", new { Id = id }); return Ok(ApiResponse<object>.Ok(new { row, itens }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao obter documento comércio. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter documento.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AlterarStatus(string tabela, long id, bool ativo, string evento)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync($"update {tabela} set ativo=@Ativo, updated_at=now() where id=@Id and tenant_id=@TenantId", new { Ativo = ativo, Id = id, TenantId = tenantId }); await Auditar(c, tenantId, evento, id, new { ativo }, cid); return Ok(ApiResponse<object>.Ok(new { id, ativo }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao alterar status. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao alterar status.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AlterarStatusDocumento(string tabela, long id, string status, string dataColumn, string evento)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync($"update {tabela} set status=@Status, {dataColumn}=now() where id=@Id and tenant_id=@TenantId", new { Status = status, Id = id, TenantId = tenantId }); await Auditar(c, tenantId, evento, id, new { status }, cid); return Ok(ApiResponse<object>.Ok(new { id, status }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao alterar documento. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao alterar documento.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AtualizarTotais(string tabela, long id, DocumentoComercialRequest r, string evento)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync($"update {tabela} set cliente_id=@ClienteId,vendedor_id=@VendedorId,tabela_preco_id=@TabelaPrecoId,subtotal=@Subtotal,desconto=@Desconto,acrescimo=@Acrescimo,total=@Total,observacao=@Observacao where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, r.ClienteId, r.VendedorId, r.TabelaPrecoId, r.Subtotal, r.Desconto, r.Acrescimo, r.Total, r.Observacao }); await Auditar(c, tenantId, evento, id, r, cid); return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao atualizar totais. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar totais.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AdicionarItem(string tipo, long id, ItemRequest item)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var itemId = await InserirItemVenda(c, tenantId, id, item); await RecalcularVenda(c, tenantId, id); return Ok(ApiResponse<object>.Ok(new { id = itemId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao adicionar item. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao adicionar item.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> RemoverItemVendaAsync(long id, long itemId)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync("delete from sigov.comercio_venda_item where id=@ItemId and venda_id=@Id", new { Id = id, ItemId = itemId }); await RecalcularVenda(c, tenantId, id); return Ok(ApiResponse<object>.Ok(new { id, itemId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao remover item. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao remover item.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertTabela(long? id, TabelaPrecoRequest r)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var resultId = id.HasValue ? await c.ExecuteScalarAsync<long>("update sigov.comercio_tabela_preco set codigo=@Codigo,nome=@Nome,tipo=@Tipo,ativo=@Ativo,updated_at=now() where id=@Id and tenant_id=@TenantId returning id", new { Id = id, TenantId = tenantId, r.Codigo, r.Nome, r.Tipo, r.Ativo }) : await c.ExecuteScalarAsync<long>("insert into sigov.comercio_tabela_preco(tenant_id,codigo,nome,tipo,ativo) values(@TenantId,@Codigo,@Nome,@Tipo,@Ativo) returning id", new { TenantId = tenantId, r.Codigo, r.Nome, r.Tipo, r.Ativo }); return Ok(ApiResponse<object>.Ok(new { id = resultId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro na tabela de preço. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha na tabela de preço.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AtualizarItensTabelaAsync(long id, IReadOnlyCollection<TabelaPrecoItemRequest> itens)
    {
        var cid = CorrelationId();
        try { using var c = _context.CreateConnection(); await c.ExecuteAsync("delete from sigov.comercio_tabela_preco_item where tabela_preco_id=@Id", new { Id = id }); foreach (var item in itens) await c.ExecuteAsync("insert into sigov.comercio_tabela_preco_item(tabela_preco_id,produto_id,preco,desconto_maximo_percentual) values(@Id,@ProdutoId,@Preco,@DescontoMaximoPercentual)", new { Id = id, item.ProdutoId, item.Preco, item.DescontoMaximoPercentual }); return Ok(ApiResponse<object>.Ok(new { id, itens = itens.Count }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro nos itens da tabela. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha nos itens da tabela.", cid)); }
    }

    private async Task<long> InserirItemVenda(System.Data.IDbConnection c, long tenantId, long vendaId, ItemRequest item)
    {
        var produto = await c.QuerySingleOrDefaultAsync<dynamic>("select nome, preco_venda, ativo from sigov.comercio_produto where id=@ProdutoId and tenant_id=@TenantId", new { item.ProdutoId, TenantId = tenantId });
        if (produto is not null && produto.ativo == false) throw new InvalidOperationException("Não é permitido vender produto inativo.");
        var descricao = string.IsNullOrWhiteSpace(item.Descricao) ? (string?)produto?.nome ?? "Item" : item.Descricao;
        var valor = item.ValorUnitario <= 0 ? (decimal?)produto?.preco_venda ?? 0 : item.ValorUnitario;
        return await c.ExecuteScalarAsync<long>("insert into sigov.comercio_venda_item(venda_id,produto_id,descricao,quantidade,valor_unitario,desconto,total) values(@VendaId,@ProdutoId,@Descricao,@Quantidade,@ValorUnitario,@Desconto,@Total) returning id", new { VendaId = vendaId, item.ProdutoId, Descricao = descricao, item.Quantidade, ValorUnitario = valor, item.Desconto, Total = (item.Quantidade * valor) - item.Desconto });
    }

    private static async Task InserirItemDocumento(System.Data.IDbConnection c, string tipo, long documentoId, ItemRequest item)
    {
        var tabela = tipo == "pedido" ? "sigov.comercio_pedido_item" : "sigov.comercio_orcamento_item";
        var fk = tipo == "pedido" ? "pedido_id" : "orcamento_id";
        await c.ExecuteAsync($"insert into {tabela}({fk},produto_id,descricao,quantidade,valor_unitario,desconto,total) values(@DocumentoId,@ProdutoId,@Descricao,@Quantidade,@ValorUnitario,@Desconto,@Total)", new { DocumentoId = documentoId, item.ProdutoId, item.Descricao, item.Quantidade, item.ValorUnitario, item.Desconto, item.Total });
    }

    private static async Task RecalcularVenda(System.Data.IDbConnection c, long tenantId, long id)
    {
        await c.ExecuteAsync("update sigov.comercio_venda set subtotal=coalesce((select sum(quantidade*valor_unitario) from sigov.comercio_venda_item where venda_id=@Id),0), desconto=coalesce((select sum(desconto) from sigov.comercio_venda_item where venda_id=@Id),0), total=coalesce((select sum(total) from sigov.comercio_venda_item where venda_id=@Id),0) where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
    }

    private async Task<long> GerarContaReceber(System.Data.IDbConnection c, long tenantId, string origem, long origemId, long? clienteId, string? numero, decimal valor, string cid)
    {
        var id = await c.ExecuteScalarAsync<long>("insert into sigov.financeiro_conta_receber(tenant_id,origem,origem_id,cliente_id,numero_documento,valor_original,valor_aberto,vencimento) values(@TenantId,@Origem,@OrigemId,@ClienteId,@Numero,@Valor,@Valor,current_date) returning id", new { TenantId = tenantId, Origem = origem, OrigemId = origemId, ClienteId = clienteId, Numero = numero, Valor = valor });
        await Auditar(c, tenantId, "CONTA_RECEBER_GERADA", id, new { origem, origemId, valor }, cid);
        return id;
    }

    private async Task Auditar(System.Data.IDbConnection c, long tenantId, string evento, long entityId, object payload, string cid)
    {
        await c.ExecuteAsync("insert into sigov.auditoria_evento(tenant_id,usuario_id,acao,entidade,entidade_id,correlation_id,depois,created_at) values(@TenantId,@UsuarioId,@Evento,@Tabela,@RegistroId,cast(@CorrelationId as uuid),cast(@Payload as jsonb),now())", new { TenantId = tenantId, Evento = evento, Tabela = "comercio", RegistroId = entityId.ToString(CultureInfo.InvariantCulture), UsuarioId = _user.UsuarioId, CorrelationId = Guid.TryParse(cid, out var parsedCid) ? parsedCid : Guid.NewGuid(), Payload = System.Text.Json.JsonSerializer.Serialize(payload) });
    }


    private static string Projection(string table) => table switch
    {
        "sigov.comercio_caixa" => "id, tenant_id, usuario_abertura_id, usuario_fechamento_id, status, valor_abertura, valor_fechamento, aberto_at, fechado_at, observacao",
        "sigov.comercio_cliente" => "id, tenant_id, nome, tipo_pessoa, documento, email, telefone, endereco_json, limite_credito, ativo, created_at, updated_at",
        "sigov.comercio_comissao" => "id, tenant_id, venda_id, pedido_id, vendedor_id, representante_id, base_calculo, percentual, valor, status, created_at, paga_at",
        "sigov.comercio_orcamento" => "id, tenant_id, cliente_id, vendedor_id, tabela_preco_id, numero, status, subtotal, desconto, acrescimo, total, observacao, created_at, aprovado_at, reprovado_at",
        "sigov.comercio_orcamento_item" => "id, orcamento_id, produto_id, descricao, quantidade, valor_unitario, desconto, total",
        "sigov.comercio_pedido" => "id, tenant_id, cliente_id, vendedor_id, representante_id, tabela_preco_id, orcamento_id, numero, status, subtotal, desconto, acrescimo, total, observacao, estoque_reservado, estoque_baixado, created_at, confirmado_at, separado_at, faturado_at, cancelado_at",
        "sigov.comercio_pedido_item" => "id, pedido_id, produto_id, descricao, quantidade, valor_unitario, desconto, total, gera_os",
        "sigov.comercio_produto" => "id, tenant_id, codigo, nome, descricao, unidade, codigo_barras, preco_venda, preco_custo, controla_estoque, gera_os, estoque_minimo, ativo, created_at, updated_at",
        "sigov.comercio_tabela_preco" => "id, tenant_id, codigo, nome, tipo, ativo, vigencia_inicio, vigencia_fim, created_at, updated_at",
        "sigov.comercio_venda" => "id, tenant_id, caixa_id, cliente_id, vendedor_id, numero, tipo, status, subtotal, desconto, acrescimo, total, observacao, estoque_baixado, created_at, finalizada_at, cancelada_at",
        "sigov.comercio_venda_item" => "id, venda_id, produto_id, descricao, quantidade, valor_unitario, desconto, total",
        _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Tabela fora da allowlist de projeções.")
    };
    private long RequireTenant() => _tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório para operação comercial.");
    private string CorrelationId() => HttpContext.TraceIdentifier;
    private static int Limit(int pageSize) => Math.Clamp(pageSize, 1, 100);
    private static int Offset(int page, int pageSize) => (Math.Max(1, page) - 1) * Limit(pageSize);

    private bool HasPermission(string permission)
    {
        if (User.Identity?.IsAuthenticated != true) return true;
        return User.IsInRole("ADMIN_GERAL") || User.IsInRole("ADMIN_TENANT") || User.Claims.Any(c => (c.Type == "permission" || c.Type == ClaimTypes.Role) && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record VendaResumo(long Id, decimal Total, string Status, string Tipo, long? CaixaId, bool EstoqueBaixado);
    private sealed record ComissaoVenda(long Id, decimal Total, long? VendedorId, string Status);
}

public sealed record ClienteRequest(string Nome, string? TipoPessoa, string? Documento, string? Email, string? Telefone, string? EnderecoJson, decimal? LimiteCredito);
public sealed record ProdutoRequest(string Codigo, string Nome, string? Descricao, string? Unidade, string? CodigoBarras, decimal PrecoVenda, decimal? PrecoCusto, bool ControlaEstoque, bool GeraOs);
public sealed record StatusRequest(bool Ativo);
public sealed record ItemRequest(long ProdutoId, string? Descricao, decimal Quantidade, decimal ValorUnitario, decimal Desconto, decimal Total);
public sealed record DocumentoComercialRequest(string? Numero, long? ClienteId, long? VendedorId, long? TabelaPrecoId, decimal Subtotal, decimal Desconto, decimal Acrescimo, decimal Total, string? Observacao, IReadOnlyCollection<ItemRequest>? Itens);
public sealed record VendaRequest(string? Numero, string? Tipo, long? CaixaId, long? ClienteId, long? VendedorId, string? Observacao, IReadOnlyCollection<ItemRequest>? Itens);
public sealed record RecebimentoRequest(long? FormaPagamentoId, decimal Valor, string? Status, DateTime? Vencimento);
public sealed record CaixaAbrirRequest(decimal ValorAbertura, string? Observacao);
public sealed record CaixaFecharRequest(decimal ValorFechamento, string? Observacao);
public sealed record CaixaMovimentoRequest(decimal Valor, string? Observacao);
public sealed record TabelaPrecoRequest(string Codigo, string Nome, string? Tipo, bool Ativo);
public sealed record TabelaPrecoItemRequest(long ProdutoId, decimal Preco, decimal DescontoMaximoPercentual);
public sealed record CalcularComissaoRequest(long VendaId, decimal Percentual);
