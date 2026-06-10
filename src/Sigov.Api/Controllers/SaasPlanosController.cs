using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saas.Comercial;
using Sigov.Infrastructure.Persistence.Dapper;
using System.Text.Json;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class SaasPlanosController : ControllerBase
{
    private readonly ISaasPlanoService _service;
    private readonly ICurrentUser _currentUser;
    private readonly DapperContext _context;
    private readonly ILogger<SaasPlanosController> _logger;

    public SaasPlanosController(ISaasPlanoService service, ICurrentUser currentUser, DapperContext context, ILogger<SaasPlanosController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _context = context;
        _logger = logger;
    }

    [HttpGet("api/publico/planos")]
    [HttpGet("api/saas/planos/publicos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>>> Publicos(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId();
        try
        {
            return Ok(ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>.Ok(await _service.ListPublicAsync(cancellationToken), correlationId: correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar planos públicos. CorrelationId={CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>.Fail("Falha ao listar planos públicos.", correlationId));
        }
    }

    [HttpGet("api/publico/planos/{codigo}")]
    [HttpGet("api/saas/planos/{codigo}")]
    public async Task<ActionResult<ApiResponse<SaasPlanoDetalheResponse>>> Detalhe(string codigo, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId();
        try
        {
            if (string.IsNullOrWhiteSpace(codigo)) return BadRequest(ApiResponse<SaasPlanoDetalheResponse>.Fail("Código do plano é obrigatório.", correlationId));
            var plano = await _service.GetByCodigoAsync(codigo, cancellationToken);
            return plano is null ? NotFound(ApiResponse<SaasPlanoDetalheResponse>.Fail("Plano não encontrado.", correlationId)) : Ok(ApiResponse<SaasPlanoDetalheResponse>.Ok(plano, correlationId: correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter plano {Codigo}. CorrelationId={CorrelationId}", codigo, correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<SaasPlanoDetalheResponse>.Fail("Falha ao obter plano.", correlationId));
        }
    }

    [HttpGet("api/saas/planos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>>> Admin([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var correlationId = CorrelationId();
        try
        {
            return Ok(ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>.Ok(await _service.ListAdminAsync(page, pageSize, cancellationToken), correlationId: correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar planos SaaS. CorrelationId={CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>.Fail("Falha ao listar planos.", correlationId));
        }
    }

    [HttpPost("api/saas/planos")]
    public async Task<ActionResult<ApiResponse<SaasPlanoResponse>>> Criar([FromBody] SaasPlanoCreateRequest request, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId();
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Nome)) return BadRequest(ApiResponse<SaasPlanoResponse>.Fail("Código e nome do plano são obrigatórios.", correlationId));
            var result = await _service.CreateAsync(request, _currentUser.UsuarioId ?? 0, cancellationToken);
            return result.IsFailure ? BadRequest(ApiResponse<SaasPlanoResponse>.Fail(result.Error ?? "Plano inválido.", correlationId)) : Ok(ApiResponse<SaasPlanoResponse>.Ok(result.Value!, correlationId: correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar plano. CorrelationId={CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<SaasPlanoResponse>.Fail("Falha ao criar plano.", correlationId));
        }
    }

    [HttpPut("api/saas/planos/{id:long}")]
    public async Task<ActionResult<ApiResponse<SaasPlanoResponse>>> Atualizar(long id, [FromBody] SaasPlanoUpdateRequest request, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId();
        try
        {
            if (id <= 0 || request is null || string.IsNullOrWhiteSpace(request.Nome)) return BadRequest(ApiResponse<SaasPlanoResponse>.Fail("Plano inválido.", correlationId));
            var result = await _service.UpdateAsync(id, request, _currentUser.UsuarioId ?? 0, cancellationToken);
            return result.IsFailure ? BadRequest(ApiResponse<SaasPlanoResponse>.Fail(result.Error ?? "Plano inválido.", correlationId)) : Ok(ApiResponse<SaasPlanoResponse>.Ok(result.Value!, correlationId: correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar plano {PlanoId}. CorrelationId={CorrelationId}", id, correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<SaasPlanoResponse>.Fail("Falha ao atualizar plano.", correlationId));
        }
    }

    [HttpPatch("api/saas/planos/{id:long}/status")]
    public async Task<ActionResult<ApiResponse<object>>> Status(long id, [FromBody] PlanoStatusRequest request, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId();
        try
        {
            if (id <= 0 || request is null) return BadRequest(ApiResponse<object>.Fail("Plano inválido.", correlationId));
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync("update sigov.saas_plano set ativo=@Ativo, updated_at=now() where id=@Id", new { Id = id, request.Ativo });
            await RegistrarEvento(connection, null, request.Ativo ? "PLANO_STATUS_ATIVADO" : "PLANO_STATUS_INATIVADO", "Status do plano alterado.", "saas_plano", new { id, request.Ativo }, correlationId, cancellationToken);
            return Ok(ApiResponse<object>.Ok(new { id, request.Ativo }, correlationId: correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar status do plano {PlanoId}. CorrelationId={CorrelationId}", id, correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Falha ao alterar status do plano.", correlationId));
        }
    }

    [HttpPut("api/saas/planos/{id:long}/modulos")]
    public async Task<ActionResult<ApiResponse<object>>> Modulos(long id, [FromBody] PlanoModulosRequest request, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId();
        try
        {
            if (id <= 0 || request?.Modulos is null) return BadRequest(ApiResponse<object>.Fail("Módulos inválidos.", correlationId));
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync("update sigov.saas_plano_modulo set incluso=false where plano_id=@Id", new { Id = id });
            foreach (var modulo in request.Modulos.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim().ToLowerInvariant()).Distinct())
            {
                await connection.ExecuteAsync("insert into sigov.saas_plano_modulo(plano_id,modulo_codigo,incluso) values(@Id,@Modulo,true) on conflict(plano_id,modulo_codigo) do update set incluso=true", new { Id = id, Modulo = modulo });
            }
            await RegistrarEvento(connection, null, "PLANO_MODULOS_ALTERADOS", "Módulos do plano alterados.", "saas_plano", new { id, request.Modulos }, correlationId, cancellationToken);
            return Ok(ApiResponse<object>.Ok(new { id, request.Modulos }, correlationId: correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar módulos do plano {PlanoId}. CorrelationId={CorrelationId}", id, correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Falha ao alterar módulos do plano.", correlationId));
        }
    }

    [HttpPut("api/saas/planos/{id:long}/limites")]
    public async Task<ActionResult<ApiResponse<object>>> Limites(long id, [FromBody] PlanoLimitesRequest request, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId();
        try
        {
            if (id <= 0 || request?.Limites is null) return BadRequest(ApiResponse<object>.Fail("Limites inválidos.", correlationId));
            using var connection = _context.CreateConnection();
            foreach (var limite in request.Limites.Where(l => !string.IsNullOrWhiteSpace(l.Codigo)))
            {
                await connection.ExecuteAsync(@"insert into sigov.saas_plano_limite(plano_id,codigo,nome,valor,unidade,ilimitado) values(@PlanoId,@Codigo,@Nome,@Valor,@Unidade,@Ilimitado)
                    on conflict(plano_id,codigo) do update set nome=excluded.nome, valor=excluded.valor, unidade=excluded.unidade, ilimitado=excluded.ilimitado", new { PlanoId = id, Codigo = limite.Codigo.Trim().ToLowerInvariant(), Nome = limite.Nome, limite.Valor, limite.Unidade, limite.Ilimitado });
            }
            await RegistrarEvento(connection, null, "PLANO_LIMITES_ALTERADOS", "Limites do plano alterados.", "saas_plano", new { id, request.Limites }, correlationId, cancellationToken);
            return Ok(ApiResponse<object>.Ok(new { id }, correlationId: correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar limites do plano {PlanoId}. CorrelationId={CorrelationId}", id, correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Falha ao alterar limites do plano.", correlationId));
        }
    }

    private string CorrelationId() => HttpContext.TraceIdentifier;

    private static Task RegistrarEvento(System.Data.IDbConnection connection, long? tenantId, string tipo, string descricao, string origem, object payload, string correlationId, CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition("insert into sigov.saas_evento_comercial(tenant_id,tipo_evento,descricao,origem,usuario_id,payload,correlation_id) values(@TenantId,@Tipo,@Descricao,@Origem,null,cast(@Payload as jsonb),cast(@CorrelationId as uuid))", new { TenantId = tenantId, Tipo = tipo, Descricao = descricao, Origem = origem, Payload = JsonSerializer.Serialize(payload), CorrelationId = Guid.TryParse(correlationId, out var g) ? g : Guid.NewGuid() }, cancellationToken: cancellationToken));
    }
}

public sealed record PlanoStatusRequest(bool Ativo);
public sealed record PlanoModulosRequest(IReadOnlyCollection<string> Modulos);
public sealed record PlanoLimitesRequest(IReadOnlyCollection<PlanoLimiteRequest> Limites);
public sealed record PlanoLimiteRequest(string Codigo, string Nome, int? Valor, string? Unidade, bool Ilimitado);
