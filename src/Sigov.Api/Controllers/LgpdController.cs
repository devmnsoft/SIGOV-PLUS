using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lgpd")]
public sealed class LgpdController : ControllerBase
{
    private readonly DapperContext _db; private readonly ICurrentTenant _tenant; private readonly ICurrentUser _user;
    public LgpdController(DapperContext db, ICurrentTenant tenant, ICurrentUser user) { _db=db; _tenant=tenant; _user=user; }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<object>>> Dashboard(CancellationToken ct) { using var c=_db.CreateConnection(); const string q="select (select count(*) from sigov.solicitacao_titular where tenant_id=@TenantId and status not in ('ENCERRADA','CANCELADA') and not is_deleted) solicitacoes_abertas,(select count(*) from sigov.lgpd_incidente where tenant_id=@TenantId and status<>'ENCERRADO') incidentes_abertos"; return ApiResponse<object>.Ok(await c.QuerySingleAsync<object>(new CommandDefinition(q,new{TenantId=Tenant()},cancellationToken:ct))); }

    [HttpGet("solicitacoes")]
    public async Task<ActionResult<ApiResponse<object>>> Solicitacoes([FromQuery]int page=1,[FromQuery]int pageSize=20,CancellationToken ct=default) { using var c=_db.CreateConnection(); const string q="select id, protocolo, tipo, status, descricao, created_at, updated_at from sigov.solicitacao_titular where tenant_id=@TenantId and not is_deleted order by created_at desc,id desc offset @Offset limit @Limit"; var limit=Math.Clamp(pageSize,1,100); return ApiResponse<object>.Ok(new{items=await c.QueryAsync<object>(new CommandDefinition(q,new{TenantId=Tenant(),Offset=(Math.Max(page,1)-1)*limit,Limit=limit},cancellationToken:ct)),page=Math.Max(page,1),pageSize=limit}); }

    [HttpPost("solicitacoes")]
    public async Task<IActionResult> CriarSolicitacao([FromBody]SolicitacaoLgpdRequest r,CancellationToken ct) { if(string.IsNullOrWhiteSpace(r.Tipo)||string.IsNullOrWhiteSpace(r.Descricao)||r.PessoaId<=0)return BadRequest(ApiResponse<object>.Fail("Pessoa, tipo e descrição são obrigatórios.")); using var c=_db.CreateConnection(); const string q="insert into sigov.solicitacao_titular(tenant_id,entidade_id,exercicio_id,pessoa_id,tipo,status,descricao,protocolo,created_by,correlation_id) select @TenantId,@EntidadeId,@ExercicioId,@PessoaId,@Tipo,'ABERTA',@Descricao,'LGPD-'||to_char(current_date,'YYYY')||'-'||nextval('sigov.lgpd_protocolo_seq')::text,@UsuarioId,cast(@CorrelationId as uuid) from sigov.pessoa where id=@PessoaId returning id,protocolo,status"; var row=await c.QuerySingleOrDefaultAsync<object>(new CommandDefinition(q,new{TenantId=Tenant(),_tenant.EntidadeId,_tenant.ExercicioId,r.PessoaId,Tipo=r.Tipo.Trim().ToUpperInvariant(),Descricao=r.Descricao.Trim(),UsuarioId=_user.UsuarioId,CorrelationId=Correlation()},cancellationToken:ct)); return row is null?NotFound(ApiResponse<object>.Fail("Titular não encontrado.")):Created("",ApiResponse<object>.Ok(row)); }

    [HttpPost("solicitacoes/{id:long}/responder")]
    public async Task<IActionResult> Responder(long id,[FromBody]RespostaLgpdRequest r,CancellationToken ct) => await AtualizarSolicitacao(id,"RESPONDIDA",r.Resposta,ct);
    [HttpPost("solicitacoes/{id:long}/encerrar")]
    public async Task<IActionResult> Encerrar(long id,[FromBody]RespostaLgpdRequest r,CancellationToken ct) => await AtualizarSolicitacao(id,"ENCERRADA",r.Resposta,ct);

    [HttpGet("incidentes")]
    public async Task<ActionResult<ApiResponse<object>>> Incidentes(CancellationToken ct) { using var c=_db.CreateConnection(); const string q="select id,severidade,descoberto_em,descricao,status,plano_acao,encerrado_em,created_at from sigov.lgpd_incidente where tenant_id=@TenantId order by descoberto_em desc,id desc limit 100"; return ApiResponse<object>.Ok(new{items=await c.QueryAsync<object>(new CommandDefinition(q,new{TenantId=Tenant()},cancellationToken:ct))}); }
    [HttpPost("incidentes")]
    public async Task<IActionResult> CriarIncidente([FromBody]IncidenteLgpdRequest r,CancellationToken ct) { var s=r.Severidade.Trim().ToUpperInvariant(); if(!new[]{"BAIXA","MEDIA","ALTA","CRITICA"}.Contains(s)||string.IsNullOrWhiteSpace(r.Descricao))return BadRequest(ApiResponse<object>.Fail("Severidade ou descrição inválida.")); using var c=_db.CreateConnection(); const string q="insert into sigov.lgpd_incidente(tenant_id,severidade,descoberto_em,descricao,dados_afetados,created_by,correlation_id) values(@TenantId,@Severidade,coalesce(@DescobertoEm,now()),@Descricao,cast(@DadosAfetados as jsonb),@UsuarioId,@CorrelationId) returning id,status"; var row=await c.QuerySingleAsync<object>(new CommandDefinition(q,new{TenantId=Tenant(),Severidade=s,r.DescobertoEm,Descricao=r.Descricao.Trim(),DadosAfetados=JsonSerializer.Serialize(r.DadosAfetados??Array.Empty<string>()),UsuarioId=_user.UsuarioId,CorrelationId=HttpContext.TraceIdentifier},cancellationToken:ct)); return Created("",ApiResponse<object>.Ok(row)); }
    [HttpPost("incidentes/{id:long}/registrar-evento")]
    public async Task<IActionResult> Evento(long id,[FromBody]EventoIncidenteRequest r,CancellationToken ct) { if(string.IsNullOrWhiteSpace(r.Descricao))return BadRequest(ApiResponse<object>.Fail("Descrição obrigatória.")); using var c=_db.CreateConnection(); const string q="insert into sigov.lgpd_incidente_evento(tenant_id,incidente_id,descricao,created_by,correlation_id) select @TenantId,id,@Descricao,@UsuarioId,@CorrelationId from sigov.lgpd_incidente where tenant_id=@TenantId and id=@Id and status<>'ENCERRADO' returning id"; var eid=await c.ExecuteScalarAsync<long?>(new CommandDefinition(q,new{TenantId=Tenant(),Id=id,Descricao=r.Descricao.Trim(),UsuarioId=_user.UsuarioId,CorrelationId=HttpContext.TraceIdentifier},cancellationToken:ct)); return eid.HasValue?Ok(ApiResponse<object>.Ok(new{id=eid.Value})):NotFound(ApiResponse<object>.Fail("Incidente aberto não encontrado.")); }
    [HttpPost("incidentes/{id:long}/encerrar")]
    public async Task<IActionResult> EncerrarIncidente(long id,[FromBody]EventoIncidenteRequest r,CancellationToken ct) { using var c=_db.CreateConnection(); const string q="update sigov.lgpd_incidente set status='ENCERRADO',plano_acao=@Descricao,encerrado_em=now() where tenant_id=@TenantId and id=@Id and status<>'ENCERRADO'"; var n=await c.ExecuteAsync(new CommandDefinition(q,new{TenantId=Tenant(),Id=id,Descricao=r.Descricao?.Trim()},cancellationToken:ct)); return n==1?Ok(ApiResponse<object>.Ok(new{id,status="ENCERRADO"})):NotFound(ApiResponse<object>.Fail("Incidente aberto não encontrado.")); }

    [HttpGet("acessos-dados-pessoais")]
    public async Task<ActionResult<ApiResponse<object>>> Acessos(CancellationToken ct) { using var c=_db.CreateConnection(); const string q="select id,modulo,recurso,finalidade,base_legal,operacao,exportacao,correlation_id,created_at from sigov.lgpd_acesso_dado_pessoal where tenant_id=@TenantId order by created_at desc,id desc limit 100"; return ApiResponse<object>.Ok(new{items=await c.QueryAsync<object>(new CommandDefinition(q,new{TenantId=Tenant()},cancellationToken:ct)),masked=true}); }

    private async Task<IActionResult> AtualizarSolicitacao(long id,string status,string resposta,CancellationToken ct) { if(string.IsNullOrWhiteSpace(resposta))return BadRequest(ApiResponse<object>.Fail("Resposta obrigatória.")); using var c=_db.CreateConnection(); const string q="update sigov.solicitacao_titular set status=@Status,resposta=@Resposta,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and not is_deleted and status not in ('ENCERRADA','CANCELADA')"; var n=await c.ExecuteAsync(new CommandDefinition(q,new{TenantId=Tenant(),Id=id,Status=status,Resposta=resposta.Trim(),UsuarioId=_user.UsuarioId},cancellationToken:ct)); return n==1?Ok(ApiResponse<object>.Ok(new{id,status})):NotFound(ApiResponse<object>.Fail("Solicitação aberta não encontrada.")); }
    private long Tenant()=>_tenant.TenantId??throw new InvalidOperationException("tenant_id obrigatório.");
    private Guid Correlation()=>Guid.TryParse(HttpContext.TraceIdentifier,out var id)?id:Guid.NewGuid();
}
public sealed record SolicitacaoLgpdRequest(long PessoaId,string Tipo,string Descricao);
public sealed record RespostaLgpdRequest(string Resposta);
public sealed record IncidenteLgpdRequest(string Severidade,string Descricao,DateTimeOffset? DescobertoEm,string[]? DadosAfetados);
public sealed record EventoIncidenteRequest(string Descricao);
