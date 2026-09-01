using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Authorization;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/seguranca")]
public sealed class SegurancaController : ControllerBase
{
    private readonly DapperContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuthorizationEvaluator _authorization;

    public SegurancaController(DapperContext db, ICurrentTenant tenant, ICurrentUser user, IAuthorizationEvaluator authorization)
    {
        _db = db; _tenant = tenant; _user = user; _authorization = authorization;
    }

    [HttpGet("usuarios")]
    public async Task<ActionResult<ApiResponse<object>>> Usuarios(CancellationToken ct)
    {
        using var c = _db.CreateConnection();
        const string sql = "select id, login, nome, email, ativo from sigov.usuario where tenant_id=@TenantId and not is_deleted order by nome, login, id limit 500";
        return ApiResponse<object>.Ok(new { items = await c.QueryAsync<object>(new CommandDefinition(sql, new { TenantId = TenantId() }, cancellationToken: ct)) });
    }

    [HttpGet("permissoes")]
    public async Task<ActionResult<ApiResponse<object>>> Permissoes(CancellationToken ct)
    {
        using var c = _db.CreateConnection();
        const string sql = "select p.id, r.modulo, r.codigo recurso, p.acao, p.escopo, p.entidade_id from sigov.seguranca_permissao_granular p join sigov.seguranca_recurso r on r.id=p.recurso_id where p.ativo and r.ativo and (p.tenant_id is null or p.tenant_id=@TenantId) order by r.modulo,r.codigo,p.acao,p.id limit 500";
        return ApiResponse<object>.Ok(new { items = await c.QueryAsync<object>(new CommandDefinition(sql, new { TenantId = TenantId() }, cancellationToken: ct)) });
    }

    [HttpGet("permissoes/dashboard")]
    public async Task<ActionResult<ApiResponse<object>>> Dashboard(CancellationToken ct)
    {
        using var c = _db.CreateConnection();
        const string sql = "select count(distinct recurso_id) recursos, count(*) permissoes from sigov.seguranca_permissao_granular where ativo and (tenant_id is null or tenant_id=@TenantId)";
        return ApiResponse<object>.Ok(await c.QuerySingleAsync<object>(new CommandDefinition(sql, new { TenantId = TenantId() }, cancellationToken: ct)));
    }

    [HttpGet("recursos")]
    public async Task<ActionResult<ApiResponse<object>>> Recursos(CancellationToken ct)
    {
        using var c = _db.CreateConnection();
        const string sql = "select id, modulo, codigo, nome, entidade from sigov.seguranca_recurso where ativo and (tenant_id is null or tenant_id=@TenantId) order by modulo, nome, id limit 500";
        return ApiResponse<object>.Ok(new { items = await c.QueryAsync<object>(new CommandDefinition(sql, new { TenantId = TenantId() }, cancellationToken: ct)) });
    }

    [HttpGet("perfis")]
    public async Task<ActionResult<ApiResponse<object>>> Perfis(CancellationToken ct)
    {
        using var c = _db.CreateConnection();
        const string sql = "select id, nome, descricao, ativo from sigov.perfil_acesso where not is_deleted and (entidade_id is null or entidade_id=@EntidadeId) order by nome, id limit 500";
        return ApiResponse<object>.Ok(new { items = await c.QueryAsync<object>(new CommandDefinition(sql, new { EntidadeId = _tenant.EntidadeId }, cancellationToken: ct)) });
    }

    [HttpGet("perfis/{id:long}/permissoes")]
    public async Task<ActionResult<ApiResponse<object>>> PermissoesPerfil(long id, CancellationToken ct) => await ListarVinculos("perfil", id, ct);

    [HttpPost("perfis/{id:long}/permissoes")]
    public async Task<IActionResult> ConcederPerfil(long id, [FromBody] PermissaoRequest request, CancellationToken ct) => await Alterar("perfil", id, request, true, ct);

    [HttpPost("perfis/{id:long}/permissoes/remover")]
    public async Task<IActionResult> RemoverPerfil(long id, [FromBody] PermissaoRequest request, CancellationToken ct) => await Alterar("perfil", id, request, false, ct);

    [HttpGet("usuarios/{id:long}/permissoes")]
    public async Task<ActionResult<ApiResponse<object>>> PermissoesUsuario(long id, CancellationToken ct) => await ListarVinculos("usuario", id, ct);

    [HttpPost("usuarios/{id:long}/permissoes")]
    public async Task<IActionResult> ConcederUsuario(long id, [FromBody] PermissaoRequest request, CancellationToken ct) => await Alterar("usuario", id, request, true, ct);

    [HttpPost("usuarios/{id:long}/permissoes/remover")]
    public async Task<IActionResult> RemoverUsuario(long id, [FromBody] PermissaoRequest request, CancellationToken ct) => await Alterar("usuario", id, request, false, ct);

    [HttpPost("validar-permissao")]
    public async Task<ActionResult<ApiResponse<object>>> Validar([FromBody] ValidarPermissaoRequest request, CancellationToken ct)
    {
        if (!_user.UsuarioId.HasValue) return Unauthorized(ApiResponse<object>.Fail("Identidade autenticada inválida."));
        var correlationId = CorrelationId();
        var decision = await _authorization.EvaluateAsync(new AuthorizationRequest(
            _user.UsuarioId.Value, request.Modulo, request.Recurso, request.Acao, TenantId(),
            request.EntidadeId, _tenant.ExercicioId, request.UnidadeId, request.Valor,
            correlationId, "API_DIAGNOSTICO"), ct).ConfigureAwait(false);
        return ApiResponse<object>.Ok(new { request.Modulo, request.Recurso, request.Acao, decisao = decision, correlationId });
    }

    private async Task<ActionResult<ApiResponse<object>>> ListarVinculos(string tipo, long id, CancellationToken ct)
    {
        using var c = _db.CreateConnection();
        var table = tipo == "perfil" ? "seguranca_perfil_permissao" : "seguranca_usuario_permissao";
        var key = tipo == "perfil" ? "perfil_id" : "usuario_id";
        var sql = $"select p.id, r.modulo, r.codigo recurso, p.acao, p.escopo, v.concedida from sigov.{table} v join sigov.seguranca_permissao_granular p on p.id=v.permissao_id join sigov.seguranca_recurso r on r.id=p.recurso_id where v.tenant_id=@TenantId and v.{key}=@Id and p.ativo order by r.modulo, r.codigo, p.acao, p.id limit 500";
        return ApiResponse<object>.Ok(new { id, items = await c.QueryAsync<object>(new CommandDefinition(sql, new { TenantId = TenantId(), Id = id }, cancellationToken: ct)) });
    }

    private async Task<IActionResult> Alterar(string tipo, long id, PermissaoRequest request, bool concedida, CancellationToken ct)
    {
        if (request.PermissaoIds.Length == 0 || request.PermissaoIds.Length > 500) return BadRequest(ApiResponse<object>.Fail("Informe entre 1 e 500 permissões."));
        var tenantId = TenantId(); using var c = _db.CreateConnection();
        var entityTable = tipo == "perfil" ? "perfil_acesso" : "usuario";
        var existsSql = $"select exists(select 1 from sigov.{entityTable} where id=@Id and ativo and not is_deleted)";
        if (!await c.ExecuteScalarAsync<bool>(new CommandDefinition(existsSql, new { Id = id }, cancellationToken: ct))) return NotFound(ApiResponse<object>.Fail($"{tipo} não encontrado."));
        const string validSql = "select count(*) from sigov.seguranca_permissao_granular where id=any(@Ids) and ativo and (tenant_id is null or tenant_id=@TenantId)";
        var valid = await c.ExecuteScalarAsync<int>(new CommandDefinition(validSql, new { Ids = request.PermissaoIds.Distinct().ToArray(), TenantId = tenantId }, cancellationToken: ct));
        if (valid != request.PermissaoIds.Distinct().Count()) return BadRequest(ApiResponse<object>.Fail("Uma ou mais permissões são inválidas para o tenant."));
        var table = tipo == "perfil" ? "seguranca_perfil_permissao" : "seguranca_usuario_permissao";
        var key = tipo == "perfil" ? "perfil_id" : "usuario_id";
        var sql = $"insert into sigov.{table}(tenant_id,{key},permissao_id,concedida,created_by) select @TenantId,@Id,x,@Concedida,@UsuarioId from unnest(@Ids::bigint[]) x on conflict(tenant_id,{key},permissao_id) do update set concedida=excluded.concedida, created_by=excluded.created_by, created_at=now()";
        await c.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id, Ids = request.PermissaoIds.Distinct().ToArray(), Concedida = concedida, UsuarioId = _user.UsuarioId }, cancellationToken: ct));
        return Ok(ApiResponse<object>.Ok(new { id, alteradas = valid, concedida, correlationId = CorrelationId() }));
    }

    private long TenantId() => _tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório.");
    private string CorrelationId() => HttpContext.TraceIdentifier;
}

public sealed record PermissaoRequest(long[] PermissaoIds);
public sealed record ValidarPermissaoRequest(string Modulo, string Recurso, string Acao, long? EntidadeId, long? UnidadeId, decimal? Valor);
