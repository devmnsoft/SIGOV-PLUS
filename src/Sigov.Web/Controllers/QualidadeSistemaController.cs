using System.Globalization;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.QualidadeSistema;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("QualidadeSistema")]
public sealed class QualidadeSistemaController(NpgsqlConnectionFactory connections, IUserPermissionService permissions, IAuditTrailService audit) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        if (!Can("QUALIDADE_SISTEMA_DASHBOARD_VIEW")) return Forbid();
        var c = Contexto(); using var db = connections.CreateConnection();
        const string sql = """select count(*) filter(where status='ABERTA') as Abertas,count(*) filter(where status='EM_ANALISE') as EmAnalise,count(*) filter(where severidade='CRITICA' and status in ('ABERTA','EM_ANALISE')) as Criticas,count(*) filter(where status='CORRIGIDA' and updated_at>=now()-interval '30 days') as Corrigidas30Dias from sigov.qualidade_inconsistencia where tenant_id=@TenantId and entidade_id=@EntidadeId; select q.id,q.modulo,q.titulo,q.severidade,q.status,u.nome as Responsavel,q.created_at as CreatedAt from sigov.qualidade_inconsistencia q left join sigov.usuario u on u.id=q.responsavel_usuario_id and u.tenant_id=q.tenant_id where q.tenant_id=@TenantId and q.entidade_id=@EntidadeId order by q.created_at desc limit 10;""";
        using var multi = await db.QueryMultipleAsync(new CommandDefinition(sql, c, cancellationToken: ct));
        var model = await multi.ReadSingleAsync<QualidadeDashboardViewModel>(); model.Recentes = (await multi.ReadAsync<QualidadeInconsistenciaItem>()).AsList();
        return View(model);
    }

    [HttpGet("Inconsistencias")]
    public async Task<IActionResult> Inconsistencias([FromQuery] QualidadeFiltro filtro, CancellationToken ct)
    {
        if (!Can("QUALIDADE_SISTEMA_INCONSISTENCIA_VIEW")) return Forbid();
        var c=Contexto(); using var db=connections.CreateConnection();
        const string sql="""select q.id,q.modulo,q.titulo,q.severidade,q.status,u.nome as Responsavel,q.created_at as CreatedAt from sigov.qualidade_inconsistencia q left join sigov.usuario u on u.id=q.responsavel_usuario_id and u.tenant_id=q.tenant_id where q.tenant_id=@TenantId and q.entidade_id=@EntidadeId and (@Modulo is null or q.modulo=@Modulo) and (@Severidade is null or q.severidade=@Severidade) and (@Status is null or q.status=@Status) and (@ResponsavelId is null or q.responsavel_usuario_id=@ResponsavelId) and (@De is null or q.created_at>=@De) and (@Ate is null or q.created_at<@Ate+interval '1 day') order by q.created_at desc limit 500""";
        ViewBag.Filtro=filtro; await LoadResponsaveis(db,c,ct); return View((await db.QueryAsync<QualidadeInconsistenciaItem>(new CommandDefinition(sql,new{c.TenantId,c.EntidadeId,Modulo=Clean(filtro.Modulo),Severidade=Allowed(filtro.Severidade,"BAIXA","MEDIA","ALTA","CRITICA"),Status=Allowed(filtro.Status,"ABERTA","EM_ANALISE","CORRIGIDA","IGNORADA_COM_JUSTIFICATIVA"),filtro.ResponsavelId,filtro.De,filtro.Ate},cancellationToken:ct))).AsList());
    }

    [HttpGet("Inconsistencias/Nova")]
    public async Task<IActionResult> Nova(CancellationToken ct){if(!Can("QUALIDADE_SISTEMA_INCONSISTENCIA_MANAGE"))return Forbid();using var db=connections.CreateConnection();await LoadResponsaveis(db,Contexto(),ct);return View(new QualidadeInconsistenciaInput());}

    [HttpPost("Inconsistencias/Nova"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Nova(QualidadeInconsistenciaInput input,CancellationToken ct)
    {
        if(!Can("QUALIDADE_SISTEMA_INCONSISTENCIA_MANAGE"))return Forbid(); var c=Contexto();
        input.Modulo=input.Modulo.Trim().ToUpperInvariant(); input.Severidade=Allowed(input.Severidade,"BAIXA","MEDIA","ALTA","CRITICA")??"";
        if(input.Severidade.Length==0)ModelState.AddModelError(nameof(input.Severidade),"Severidade inválida.");
        using var db=connections.CreateConnection();
        if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.usuario where id=@Id and tenant_id=@TenantId and ativo)",new{Id=input.ResponsavelUsuarioId,c.TenantId},cancellationToken:ct)))ModelState.AddModelError(nameof(input.ResponsavelUsuarioId),"Responsável inválido para o contexto atual.");
        if(!ModelState.IsValid){await LoadResponsaveis(db,c,ct);return View(input);}
        const string sql="""insert into sigov.qualidade_inconsistencia(tenant_id,entidade_id,modulo,titulo,severidade,status,responsavel_usuario_id,evidencia,tela,rota,created_by,updated_by) values(@TenantId,@EntidadeId,@Modulo,@Titulo,@Severidade,'ABERTA',@ResponsavelUsuarioId,@Evidencia,@Tela,@Rota,@UsuarioId,@UsuarioId) returning id""";
        var id=await db.ExecuteScalarAsync<long>(new CommandDefinition(sql,new{c.TenantId,c.EntidadeId,c.UsuarioId,input.Modulo,input.Titulo,input.Severidade,input.ResponsavelUsuarioId,input.Evidencia,input.Tela,input.Rota},cancellationToken:ct));
        await Auditar("QUALIDADE_INCONSISTENCIA_CRIADA",id,ct); TempData["Success"]="Inconsistência registrada.";return RedirectToAction(nameof(Inconsistencias));
    }

    [HttpPost("Inconsistencias/{id:long}/Status"),ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarStatus(long id,QualidadeTratamentoInput input,CancellationToken ct)
    {
        if(!Can("QUALIDADE_SISTEMA_INCONSISTENCIA_MANAGE"))return Forbid();var c=Contexto();input.Status=Allowed(input.Status,"EM_ANALISE","CORRIGIDA","IGNORADA_COM_JUSTIFICATIVA")??"";if(input.Status.Length==0)ModelState.AddModelError(nameof(input.Status),"Status inválido.");if(!ModelState.IsValid){TempData["Error"]="Status ou justificativa inválidos.";return RedirectToAction(nameof(Inconsistencias));}
        using var db=connections.CreateConnection();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);
        const string update="""update sigov.qualidade_inconsistencia set status=@Status,updated_at=now(),updated_by=@UsuarioId where id=@Id and tenant_id=@TenantId and entidade_id=@EntidadeId returning status""";
        var changed=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition(update,new{Id=id,c.TenantId,c.EntidadeId,c.UsuarioId,input.Status},tx,cancellationToken:ct));if(changed is null){await tx.RollbackAsync(ct);return NotFound();}
        await db.ExecuteAsync(new CommandDefinition("insert into sigov.qualidade_inconsistencia_historico(tenant_id,entidade_id,inconsistencia_id,status_novo,justificativa,created_by) values(@TenantId,@EntidadeId,@Id,@Status,@Justificativa,@UsuarioId)",new{Id=id,c.TenantId,c.EntidadeId,c.UsuarioId,input.Status,input.Justificativa},tx,cancellationToken:ct));await tx.CommitAsync(ct);await Auditar("QUALIDADE_STATUS_ALTERADO",id,ct);return RedirectToAction(nameof(Inconsistencias));
    }

    [HttpGet("Relatorios")]
    public async Task<IActionResult> Relatorios([FromQuery] QualidadeFiltro filtro,CancellationToken ct)
    {
        if(!Can("QUALIDADE_SISTEMA_RELATORIO_EXPORT"))return Forbid();var result=await Inconsistencias(filtro,ct);if(result is not ViewResult view||view.Model is not List<QualidadeInconsistenciaItem> rows)return result;
        static string Csv(string? value){var v=value??"";if(v.Length>0&&"=+-@\t\r".Contains(v[0]))v="'"+v;return '"'+v.Replace("\"","\"\"")+'"';}
        var b=new StringBuilder("id;modulo;titulo;severidade;status;responsavel;criado_em\r\n");foreach(var x in rows)b.Append(x.Id).Append(';').Append(Csv(x.Modulo)).Append(';').Append(Csv(x.Titulo)).Append(';').Append(Csv(x.Severidade)).Append(';').Append(Csv(x.Status)).Append(';').Append(Csv(x.Responsavel)).Append(';').Append(x.CreatedAt.ToString("O",CultureInfo.InvariantCulture)).Append("\r\n");await Auditar("QUALIDADE_RELATORIO_EXPORTADO",null,ct);return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(b.ToString())).ToArray(),"text/csv; charset=utf-8",$"qualidade-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("Checklist")] public Task<IActionResult> Checklist(CancellationToken ct)=>Validacoes("QUALIDADE_SISTEMA_CHECKLIST_VIEW","qualidade_checklist_item",ct);
    [HttpGet("Rotas")] public Task<IActionResult> Rotas(CancellationToken ct)=>Validacoes("QUALIDADE_SISTEMA_ROTA_VIEW","qualidade_smoke_rota",ct);
    [HttpGet("Formularios")] public Task<IActionResult> Formularios(CancellationToken ct)=>Validacoes("QUALIDADE_SISTEMA_FORMULARIO_VIEW","qualidade_validacao_formulario",ct);
    [HttpGet("Permissoes")] public Task<IActionResult> Permissoes(CancellationToken ct)=>Validacoes("QUALIDADE_SISTEMA_PERMISSAO_VIEW","qualidade_validacao_permissao",ct);
    [HttpGet("Sql")] public Task<IActionResult> Sql(CancellationToken ct)=>Validacoes("QUALIDADE_SISTEMA_SQL_VIEW","qualidade_validacao_sql",ct);
    [HttpGet("Integracoes")] public Task<IActionResult> Integracoes(CancellationToken ct)=>Validacoes("QUALIDADE_SISTEMA_DASHBOARD_VIEW","qualidade_validacao_integracao",ct);
    private async Task<IActionResult> Validacoes(string permissao,string tabela,CancellationToken ct){if(!Can(permissao))return Forbid();var c=Contexto();using var db=connections.CreateConnection();var sql=$"select id,modulo,referencia,status,validado_em as ValidadoEm from sigov.{tabela} where tenant_id=@TenantId and entidade_id=@EntidadeId order by id desc limit 500";ViewBag.Area=tabela;return View("Validacoes",(await db.QueryAsync<QualidadeValidacaoItem>(new CommandDefinition(sql,c,cancellationToken:ct))).AsList());}
    private async Task LoadResponsaveis(System.Data.IDbConnection db,ContextoQualidade c,CancellationToken ct)=>ViewBag.Responsaveis=(await db.QueryAsync<QualidadeResponsavelItem>(new CommandDefinition("select id,nome from sigov.usuario where tenant_id=@TenantId and ativo order by nome limit 500",c,cancellationToken:ct))).AsList();
    private bool Can(string p)=>User.Identity?.IsAuthenticated==true&&permissions.HasPermission(User,p);
    private ContextoQualidade Contexto(){if(!long.TryParse(User.FindFirst("tenant_id")?.Value??User.FindFirst("contexto_tenant_id")?.Value,out var t)||t<=0||!long.TryParse(User.FindFirst("entidade_id")?.Value,out var e)||e<=0||!long.TryParse(User.FindFirst("sub")?.Value??User.FindFirstValue(ClaimTypes.NameIdentifier),out var u)||u<=0)throw new UnauthorizedAccessException("Contexto tenant, entidade e usuário é obrigatório.");return new(t,e,u);}
    private static string? Clean(string? v)=>string.IsNullOrWhiteSpace(v)?null:v.Trim().ToUpperInvariant(); private static string? Allowed(string? v,params string[] values){var n=Clean(v);return n is not null&&values.Contains(n,StringComparer.Ordinal)?n:null;}
    private Task Auditar(string acao,long? id,CancellationToken ct){var c=Contexto();return audit.RegistrarAsync(c.TenantId,c.UsuarioId,acao,"qualidade_inconsistencia",id?.ToString(),null,new{recurso="qualidade_inconsistencia",id},HttpContext.Connection.RemoteIpAddress?.ToString(),Request.Headers.UserAgent.ToString(),HttpContext.TraceIdentifier,ct);}
    private sealed record ContextoQualidade(long TenantId,long EntidadeId,long UsuarioId);
}
