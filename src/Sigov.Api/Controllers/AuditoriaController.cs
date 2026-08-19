using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auditoria")]
public sealed class AuditoriaController : ControllerBase
{
    private readonly DapperContext _db; private readonly ICurrentTenant _tenant; private readonly ICurrentUser _user;
    public AuditoriaController(DapperContext db,ICurrentTenant tenant,ICurrentUser user){_db=db;_tenant=tenant;_user=user;}
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<object>>> Dashboard(CancellationToken ct){using var c=_db.CreateConnection();const string q="select (select count(*) from sigov.auditoria_evento_operacional where tenant_id=@TenantId) eventos,(select count(*) from sigov.seguranca_evento where tenant_id=@TenantId and not permitido) falhas_acesso,(select count(*) from sigov.auditoria_exportacao where tenant_id=@TenantId) exportacoes";return ApiResponse<object>.Ok(await c.QuerySingleAsync<object>(new CommandDefinition(q,new{TenantId=Tenant()},cancellationToken:ct)));}
    [HttpGet("eventos")]
    public async Task<ActionResult<ApiResponse<object>>> Eventos([FromQuery]int page=1,[FromQuery]int pageSize=20,CancellationToken ct=default){using var c=_db.CreateConnection();var limit=Math.Clamp(pageSize,1,100);const string q="select id,usuario_id,modulo,recurso,acao,entidade,entidade_id,severidade,correlation_id,created_at from sigov.auditoria_evento_operacional where tenant_id=@TenantId order by created_at desc,id desc offset @Offset limit @Limit";return ApiResponse<object>.Ok(new{items=await c.QueryAsync<object>(new CommandDefinition(q,new{TenantId=Tenant(),Offset=(Math.Max(page,1)-1)*limit,Limit=limit},cancellationToken:ct)),page=Math.Max(page,1),pageSize=limit});}
    [HttpGet("timeline")]
    public async Task<ActionResult<ApiResponse<object>>> Timeline([FromQuery]string chave="",CancellationToken ct=default){using var c=_db.CreateConnection();const string q="select id,modulo,recurso,acao,entidade,entidade_id,severidade,correlation_id,created_at from sigov.auditoria_evento_operacional where tenant_id=@TenantId and (@Chave='' or entidade_id=@Chave or correlation_id=@Chave) order by created_at desc,id desc limit 100";return ApiResponse<object>.Ok(new{chave,items=await c.QueryAsync<object>(new CommandDefinition(q,new{TenantId=Tenant(),Chave=chave},cancellationToken:ct))});}
    [HttpGet("exportacoes")]
    public async Task<ActionResult<ApiResponse<object>>> Exportacoes(CancellationToken ct){using var c=_db.CreateConnection();const string q="select id,modulo,recurso,finalidade,formato,quantidade_registros,campos_mascarados,correlation_id,created_at from sigov.auditoria_exportacao where tenant_id=@TenantId order by created_at desc,id desc limit 100";return ApiResponse<object>.Ok(new{items=await c.QueryAsync<object>(new CommandDefinition(q,new{TenantId=Tenant()},cancellationToken:ct)),masked=true});}
    [HttpGet("falhas-acesso")]
    public async Task<ActionResult<ApiResponse<object>>> Falhas(CancellationToken ct){using var c=_db.CreateConnection();const string q="select id,usuario_id,modulo,recurso,acao,entidade_id,motivo,correlation_id,created_at from sigov.seguranca_evento where tenant_id=@TenantId and not permitido order by created_at desc,id desc limit 100";return ApiResponse<object>.Ok(new{items=await c.QueryAsync<object>(new CommandDefinition(q,new{TenantId=Tenant()},cancellationToken:ct))});}
    [HttpGet("relatorios/exportar-csv")]
    public async Task<IActionResult> Csv(CancellationToken ct){var tenantId=Tenant();using var c=_db.CreateConnection();const string q="select modulo,recurso,acao,severidade,correlation_id,created_at from sigov.auditoria_evento_operacional where tenant_id=@TenantId order by created_at desc,id desc limit 5000";var rows=(await c.QueryAsync<AuditCsv>(new CommandDefinition(q,new{TenantId=tenantId},cancellationToken:ct))).ToArray();var b=new StringBuilder("modulo;recurso;acao;severidade;correlation_id;data\n");foreach(var r in rows)b.Append(CsvValue(r.Modulo)).Append(';').Append(CsvValue(r.Recurso)).Append(';').Append(CsvValue(r.Acao)).Append(';').Append(CsvValue(r.Severidade)).Append(';').Append(CsvValue(r.Correlation_Id)).Append(';').Append(r.Created_At.ToString("O",System.Globalization.CultureInfo.InvariantCulture)).Append('\n');var correlationId=HttpContext.TraceIdentifier;const string a="insert into sigov.auditoria_exportacao(tenant_id,usuario_id,modulo,recurso,finalidade,formato,quantidade_registros,campos_mascarados,correlation_id) values(@TenantId,@UsuarioId,'GOVERNANCA','AUDITORIA','Relatório operacional','CSV',@Quantidade,true,@CorrelationId)";await c.ExecuteAsync(new CommandDefinition(a,new{TenantId=tenantId,UsuarioId=_user.UsuarioId,Quantidade=rows.Length,correlationId},cancellationToken:ct));return File(Encoding.UTF8.GetBytes(b.ToString()),"text/csv; charset=utf-8","auditoria-operacional.csv");}
    private long Tenant()=>_tenant.TenantId??throw new InvalidOperationException("tenant_id obrigatório.");
    private static string CsvValue(string? value)=>"\""+(value??string.Empty).Replace("\"","\"\"")+"\"";
    private sealed record AuditCsv(string Modulo,string Recurso,string Acao,string Severidade,string Correlation_Id,DateTimeOffset Created_At);
}
