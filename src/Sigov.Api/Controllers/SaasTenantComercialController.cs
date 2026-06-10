using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;
using System.Text.Json;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class SaasTenantComercialController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<SaasTenantComercialController> _logger;

    public SaasTenantComercialController(DapperContext context, ICurrentUser currentUser, ILogger<SaasTenantComercialController> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet("api/saas/tenants/{tenantId:long}/assinatura")]
    public async Task<ActionResult<ApiResponse<object>>> Assinatura(long tenantId)
    {
        var cid = CorrelationId();
        try
        {
            if (tenantId <= 0) return BadRequest(ApiResponse<object>.Fail("Tenant inválido.", cid));
            using var c = _context.CreateConnection();
            var assinatura = await c.QuerySingleOrDefaultAsync<object>(@"select a.*, p.codigo as plano_codigo, p.nome as plano_nome from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id where a.tenant_id=@TenantId order by a.created_at desc limit 1", new { TenantId = tenantId });
            var historico = await c.QueryAsync<object>("select * from sigov.saas_assinatura_historico where tenant_id=@TenantId order by created_at desc limit 50", new { TenantId = tenantId });
            return Ok(ApiResponse<object>.Ok(new { assinatura, historico }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter assinatura do tenant {TenantId}. CorrelationId={CorrelationId}", tenantId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter assinatura.", cid));
        }
    }

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura")]
    public Task<ActionResult<ApiResponse<object>>> CriarAssinatura(long tenantId, [FromBody] TenantAssinaturaRequest request) => AlterarAssinatura(tenantId, request.PlanoCodigo, "ASSINATURA_CRIADA", request.Motivo);

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura/upgrade")]
    public Task<ActionResult<ApiResponse<object>>> Upgrade(long tenantId, [FromBody] TenantAssinaturaRequest request) => AlterarAssinatura(tenantId, request.PlanoCodigo, "ASSINATURA_UPGRADE", request.Motivo);

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura/downgrade")]
    public Task<ActionResult<ApiResponse<object>>> Downgrade(long tenantId, [FromBody] TenantAssinaturaRequest request) => AlterarAssinatura(tenantId, request.PlanoCodigo, "ASSINATURA_DOWNGRADE", request.Motivo);

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura/cancelar")]
    public Task<ActionResult<ApiResponse<object>>> Cancelar(long tenantId) => StatusAssinatura(tenantId, "CANCELADA", "ASSINATURA_CANCELADA");

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura/reativar")]
    public Task<ActionResult<ApiResponse<object>>> Reativar(long tenantId) => StatusAssinatura(tenantId, "ATIVA", "ASSINATURA_REATIVADA");

    [HttpGet("api/saas/tenants/{tenantId:long}/implantacao")]
    public async Task<ActionResult<ApiResponse<object>>> Implantacao(long tenantId)
    {
        var cid = CorrelationId();
        try
        {
            using var c = _context.CreateConnection();
            await EnsureImplantacao(c, tenantId, cid);
            var implantacao = await c.QuerySingleAsync<object>("select * from sigov.saas_implantacao where tenant_id=@TenantId", new { TenantId = tenantId });
            var itens = await c.QueryAsync<object>("select * from sigov.saas_implantacao_item where implantacao_id=(select id from sigov.saas_implantacao where tenant_id=@TenantId) order by ordem", new { TenantId = tenantId });
            return Ok(ApiResponse<object>.Ok(new { implantacao, itens }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na implantação do tenant {TenantId}. CorrelationId={CorrelationId}", tenantId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter implantação.", cid));
        }
    }

    [HttpPost("api/saas/tenants/{tenantId:long}/implantacao/iniciar")]
    public async Task<ActionResult<ApiResponse<object>>> IniciarImplantacao(long tenantId)
    {
        var cid = CorrelationId();
        try
        {
            using var c = _context.CreateConnection();
            await EnsureImplantacao(c, tenantId, cid);
            return Ok(ApiResponse<object>.Ok(new { tenantId, status = "EM_ANDAMENTO" }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar implantação {TenantId}. CorrelationId={CorrelationId}", tenantId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao iniciar implantação.", cid));
        }
    }

    [HttpPut("api/saas/implantacoes/{implantacaoId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> AtualizarImplantacao(long implantacaoId, [FromBody] ImplantacaoUpdateRequest request)
    {
        var cid = CorrelationId();
        try
        {
            using var c = _context.CreateConnection();
            await c.ExecuteAsync("update sigov.saas_implantacao set responsavel_nome=@ResponsavelNome,responsavel_email=@ResponsavelEmail,data_previsao=@DataPrevisao,observacao=@Observacao,updated_at=now() where id=@Id", new { Id = implantacaoId, request.ResponsavelNome, request.ResponsavelEmail, request.DataPrevisao, request.Observacao });
            await RecalcularPercentual(c, implantacaoId);
            return Ok(ApiResponse<object>.Ok(new { implantacaoId }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar implantação {ImplantacaoId}. CorrelationId={CorrelationId}", implantacaoId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar implantação.", cid));
        }
    }

    [HttpPost("api/saas/implantacoes/{implantacaoId:long}/itens/{itemId:long}/concluir")]
    public Task<ActionResult<ApiResponse<object>>> ConcluirItem(long implantacaoId, long itemId) => StatusItem(implantacaoId, itemId, true, "IMPLANTACAO_ITEM_CONCLUIDO");

    [HttpPost("api/saas/implantacoes/{implantacaoId:long}/itens/{itemId:long}/reabrir")]
    public Task<ActionResult<ApiResponse<object>>> ReabrirItem(long implantacaoId, long itemId) => StatusItem(implantacaoId, itemId, false, "IMPLANTACAO_ITEM_REABERTO");

    [HttpPost("api/saas/implantacoes/{implantacaoId:long}/concluir")]
    public async Task<ActionResult<ApiResponse<object>>> ConcluirImplantacao(long implantacaoId)
    {
        var cid = CorrelationId();
        try
        {
            using var c = _context.CreateConnection();
            var pendentes = await c.ExecuteScalarAsync<int>("select count(*) from sigov.saas_implantacao_item where implantacao_id=@Id and obrigatorio=true and concluido=false", new { Id = implantacaoId });
            if (pendentes > 0) return BadRequest(ApiResponse<object>.Fail("Existem itens obrigatórios pendentes.", cid));
            await c.ExecuteAsync("update sigov.saas_implantacao set status='CONCLUIDA', percentual=100, data_conclusao=current_date, updated_at=now() where id=@Id", new { Id = implantacaoId });
            return Ok(ApiResponse<object>.Ok(new { implantacaoId, status = "CONCLUIDA" }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao concluir implantação {ImplantacaoId}. CorrelationId={CorrelationId}", implantacaoId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao concluir implantação.", cid));
        }
    }

    [HttpGet("api/saas/tenants/{tenantId:long}/parametros")]
    public async Task<ActionResult<ApiResponse<object>>> Parametros(long tenantId)
    {
        var cid = CorrelationId();
        try
        {
            using var c = _context.CreateConnection();
            var rows = await c.QueryAsync<object>("select id, tenant_id, chave, case when sensivel then '***' else valor end as valor, tipo, descricao, sensivel, updated_at from sigov.tenant_parametro where tenant_id=@TenantId order by chave", new { TenantId = tenantId });
            return Ok(ApiResponse<object>.Ok(rows, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar parâmetros {TenantId}. CorrelationId={CorrelationId}", tenantId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar parâmetros.", cid));
        }
    }

    [HttpPut("api/saas/tenants/{tenantId:long}/parametros")]
    public async Task<ActionResult<ApiResponse<object>>> AtualizarParametros(long tenantId, [FromBody] TenantParametrosRequest request)
    {
        var cid = CorrelationId();
        try
        {
            if (tenantId <= 0 || request?.Parametros is null) return BadRequest(ApiResponse<object>.Fail("Parâmetros inválidos.", cid));
            using var c = _context.CreateConnection();
            foreach (var p in request.Parametros.Where(x => !string.IsNullOrWhiteSpace(x.Chave)))
            {
                await c.ExecuteAsync("insert into sigov.tenant_parametro(tenant_id,chave,valor,tipo,descricao,sensivel,updated_at) values(@TenantId,@Chave,@Valor,@Tipo,@Descricao,@Sensivel,now()) on conflict(tenant_id,chave) do update set valor=excluded.valor,tipo=excluded.tipo,descricao=excluded.descricao,sensivel=excluded.sensivel,updated_at=now()", new { TenantId = tenantId, p.Chave, p.Valor, Tipo = p.Tipo ?? "string", p.Descricao, p.Sensivel });
            }
            return Ok(ApiResponse<object>.Ok(new { tenantId }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar parâmetros {TenantId}. CorrelationId={CorrelationId}", tenantId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar parâmetros.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> AlterarAssinatura(long tenantId, string planoCodigo, string acao, string? motivo)
    {
        var cid = CorrelationId();
        try
        {
            if (tenantId <= 0 || string.IsNullOrWhiteSpace(planoCodigo)) return BadRequest(ApiResponse<object>.Fail("Tenant e plano são obrigatórios.", cid));
            using var c = _context.CreateConnection();
            var planoId = await c.ExecuteScalarAsync<long?>("select id from sigov.saas_plano where codigo=@Codigo and ativo=true", new { Codigo = planoCodigo.Trim().ToUpperInvariant() });
            if (planoId is null) return NotFound(ApiResponse<object>.Fail("Plano não encontrado.", cid));
            var atual = await c.QuerySingleOrDefaultAsync<(long Id, long PlanoId)>("select id, plano_id as PlanoId from sigov.saas_assinatura where tenant_id=@TenantId and status='ATIVA' order by created_at desc limit 1", new { TenantId = tenantId });
            long assinaturaId;
            long? planoAnterior = null;
            if (atual.Id > 0)
            {
                assinaturaId = atual.Id;
                planoAnterior = atual.PlanoId;
                await c.ExecuteAsync("update sigov.saas_assinatura set plano_id=@PlanoId, updated_at=now(), observacao=@Motivo where id=@Id", new { PlanoId = planoId.Value, Id = assinaturaId, Motivo = motivo });
            }
            else
            {
                assinaturaId = await c.ExecuteScalarAsync<long>("insert into sigov.saas_assinatura(tenant_id,plano_id,status,data_inicio,usuarios_contratados,periodicidade,observacao) values(@TenantId,@PlanoId,'ATIVA',current_date,1,'MENSAL',@Motivo) returning id", new { TenantId = tenantId, PlanoId = planoId.Value, Motivo = motivo });
            }
            await c.ExecuteAsync("insert into sigov.saas_assinatura_historico(assinatura_id,tenant_id,plano_anterior_id,plano_novo_id,acao,motivo,usuario_id,correlation_id) values(@AssinaturaId,@TenantId,@PlanoAnterior,@PlanoNovo,@Acao,@Motivo,@UsuarioId,@CorrelationId)", new { AssinaturaId = assinaturaId, TenantId = tenantId, PlanoAnterior = planoAnterior, PlanoNovo = planoId.Value, Acao = acao, Motivo = motivo, UsuarioId = _currentUser.UsuarioId, CorrelationId = Guid.NewGuid() });
            await c.ExecuteAsync("insert into sigov.saas_assinatura_modulo(tenant_id,assinatura_id,modulo_codigo,status,habilitado) select @TenantId,@AssinaturaId,modulo_codigo,'ATIVO',true from sigov.saas_plano_modulo where plano_id=@PlanoId and incluso=true on conflict(tenant_id,assinatura_id,modulo_codigo) do update set status='ATIVO', habilitado=true", new { TenantId = tenantId, AssinaturaId = assinaturaId, PlanoId = planoId.Value });
            return Ok(ApiResponse<object>.Ok(new { tenantId, assinaturaId, planoId }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar assinatura do tenant {TenantId}. CorrelationId={CorrelationId}", tenantId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao alterar assinatura.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> StatusAssinatura(long tenantId, string status, string acao)
    {
        var cid = CorrelationId();
        try
        {
            using var c = _context.CreateConnection();
            var id = await c.ExecuteScalarAsync<long?>("select id from sigov.saas_assinatura where tenant_id=@TenantId order by created_at desc limit 1", new { TenantId = tenantId });
            if (id is null) return NotFound(ApiResponse<object>.Fail("Assinatura não encontrada.", cid));
            await c.ExecuteAsync("update sigov.saas_assinatura set status=@Status, updated_at=now() where id=@Id", new { Id = id.Value, Status = status });
            await c.ExecuteAsync("insert into sigov.saas_assinatura_historico(assinatura_id,tenant_id,acao,usuario_id,correlation_id) values(@Id,@TenantId,@Acao,@UsuarioId,@CorrelationId)", new { Id = id.Value, TenantId = tenantId, Acao = acao, UsuarioId = _currentUser.UsuarioId, CorrelationId = Guid.NewGuid() });
            return Ok(ApiResponse<object>.Ok(new { tenantId, status }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar status da assinatura {TenantId}. CorrelationId={CorrelationId}", tenantId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao alterar status da assinatura.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> StatusItem(long implantacaoId, long itemId, bool concluido, string evento)
    {
        var cid = CorrelationId();
        try
        {
            using var c = _context.CreateConnection();
            await c.ExecuteAsync("update sigov.saas_implantacao_item set concluido=@Concluido, concluido_at=case when @Concluido then now() else null end, concluido_por=case when @Concluido then @UsuarioId else null end where id=@ItemId and implantacao_id=@ImplantacaoId", new { Concluido = concluido, UsuarioId = _currentUser.UsuarioId, ItemId = itemId, ImplantacaoId = implantacaoId });
            await RecalcularPercentual(c, implantacaoId);
            return Ok(ApiResponse<object>.Ok(new { implantacaoId, itemId, concluido, evento }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar item de implantação {ItemId}. CorrelationId={CorrelationId}", itemId, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao alterar item.", cid));
        }
    }

    private static async Task EnsureImplantacao(System.Data.IDbConnection c, long tenantId, string cid)
    {
        var id = await c.ExecuteScalarAsync<long?>("insert into sigov.saas_implantacao(tenant_id,status) values(@TenantId,'EM_ANDAMENTO') on conflict(tenant_id) do nothing returning id", new { TenantId = tenantId });
        var implantacaoId = id ?? await c.ExecuteScalarAsync<long>("select id from sigov.saas_implantacao where tenant_id=@TenantId", new { TenantId = tenantId });
        var itens = new[] { "DADOS_CLIENTE", "PLANO_SELECIONADO", "MODULOS_CONTRATADOS", "WHITE_LABEL", "ADMIN_TENANT", "PERFIS", "PERMISSOES", "MIGRACAO_DADOS", "PARAMETRIZACOES", "VALIDACAO_CLIENTE", "TREINAMENTO", "AMBIENTE_LIBERADO" };
        for (var i = 0; i < itens.Length; i++)
        {
            await c.ExecuteAsync("insert into sigov.saas_implantacao_item(implantacao_id,codigo,titulo,categoria,ordem) values(@ImplantacaoId,@Codigo,@Titulo,'ONBOARDING',@Ordem) on conflict(implantacao_id,codigo) do nothing", new { ImplantacaoId = implantacaoId, Codigo = itens[i], Titulo = ToTitulo(itens[i]), Ordem = i + 1 });
        }
    }

    private static async Task RecalcularPercentual(System.Data.IDbConnection c, long implantacaoId)
    {
        await c.ExecuteAsync(@"update sigov.saas_implantacao set percentual = coalesce((select round(100.0 * count(*) filter(where concluido) / nullif(count(*),0), 2) from sigov.saas_implantacao_item where implantacao_id=@Id),0), updated_at=now() where id=@Id", new { Id = implantacaoId });
    }

    private static string ToTitulo(string codigo) => codigo.Replace('_', ' ').ToLowerInvariant();
    private string CorrelationId() => HttpContext.TraceIdentifier;
}

public sealed record TenantAssinaturaRequest(string PlanoCodigo, string? Motivo);
public sealed record ImplantacaoUpdateRequest(string? ResponsavelNome, string? ResponsavelEmail, DateOnly? DataPrevisao, string? Observacao);
public sealed record TenantParametrosRequest(IReadOnlyCollection<TenantParametroRequest> Parametros);
public sealed record TenantParametroRequest(string Chave, string? Valor, string? Tipo, string? Descricao, bool Sensivel);
