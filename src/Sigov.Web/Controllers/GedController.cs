using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class GedController : Controller
{
    private readonly GedOperationalService _operationalDemo;
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSchemaInspector _schema;
    private readonly IUserPermissionService _permissions;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<GedController> _logger;
    private readonly ITenantContextAccessor _tenantContext;
    public GedController(GedOperationalService operationalDemo, NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schema, IUserPermissionService permissions, IAuditTrailService auditTrail, ITenantContextAccessor tenantContext, ILogger<GedController> logger)
    { _operationalDemo = operationalDemo; _connectionFactory = connectionFactory; _schema = schema; _permissions = permissions; _auditTrail = auditTrail; _tenantContext = tenantContext; _logger = logger; }

    [HttpGet("/Ged")]
    [HttpGet("/Ged/Pendentes")]
    [HttpGet("/Ged/Lixeira")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    { if (!Can("ged.visualizar")) return Forbid(); return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "Documentos reais", q, cancellationToken)); }

    [HttpGet("/GED/Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        if (!CanAny("GED_DASHBOARD_VIEW", "ged.visualizar")) return Forbid();
        var tenantId = TenantId();
        using var cn = _connectionFactory.CreateConnection();
        const string sql = @"select
count(*) filter(where d.ativo) as Documentos,
count(*) filter(where d.ativo and d.classificacao_id is null) as PendentesClassificacao,
(select count(*) from sigov.ged_ocr_job j where j.tenant_id=@TenantId and j.status='PENDENTE') as OcrPendente,
(select count(*) from sigov.ged_ocr_resultado r where r.tenant_id=@TenantId and coalesce(r.confianca,0)<70 and not r.revisado) as OcrBaixaConfianca,
(select count(*) from sigov.ged_protocolo p where p.tenant_id=@TenantId and p.status='EM_TRAMITACAO') as ProtocolosTramitacao,
(select count(*) from sigov.ged_workflow_etapa e where e.tenant_id=@TenantId and e.status='ATIVA' and e.created_at + make_interval(hours=>coalesce(e.prazo_horas,0))<now()) as FluxosAtrasados,
(select count(*) from sigov.ged_assinatura_solicitacao a where a.tenant_id=@TenantId and a.status in ('PENDENTE','ENVIADA')) as AssinaturasPendentes,
(select count(*) from sigov.ged_emprestimo x where x.tenant_id=@TenantId and x.devolvido_em is null and x.previsto_para<now()) as EmprestimosVencidos,
(select count(*) from sigov.ged_acervo_caixa c where c.tenant_id=@TenantId and c.ativo) as Caixas,
(select count(*) from sigov.ged_evento_temporalidade t where t.tenant_id=@TenantId and not t.suspenso and t.prazo_final between current_date and current_date+90) as TemporalidadeProxima,
(select count(*) from sigov.ged_eliminacao_lote l where l.tenant_id=@TenantId and l.status='AGUARDANDO_APROVACAO') as EliminacoesPendentes,
(select count(*) from sigov.ged_auditoria_acesso a where a.tenant_id=@TenantId and a.dado_sensivel and a.ocorrido_em>now()-interval '7 days') as AcessosSensiveis
from sigov.ged_documento d where d.tenant_id=@TenantId";
        var model = await cn.QuerySingleAsync<Sigov.Web.Models.Ged360.GedDashboardViewModel>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return View("~/Views/Ged/Dashboard360.cshtml", model);
    }

    [HttpGet("/GED/Documentos")]
    [HttpGet("/GED/Busca")]
    public async Task<IActionResult> Documentos(string? q, string? status, string? sigilo, CancellationToken cancellationToken)
    {
        if (!CanAny("GED_DOCUMENTO_VIEW", "ged.visualizar")) return Forbid();
        using var cn = _connectionFactory.CreateConnection();
        const string sql = @"select d.id,d.titulo,d.status,d.confidencialidade,d.created_at as CriadoEm,
coalesce(t.nome,'Não classificado') as Tipo,coalesce(c.codigo||' — '||c.titulo,'Pendente') as Classificacao
from sigov.ged_documento d left join sigov.ged_tipo_documental t on t.id=d.tipo_documental_id and t.tenant_id=d.tenant_id
left join sigov.ged_plano_classificacao c on c.id=d.classificacao_id and c.tenant_id=d.tenant_id
where d.tenant_id=@TenantId and d.ativo and (@Q is null or d.titulo ilike '%'||@Q||'%' or d.texto_busca @@ websearch_to_tsquery('portuguese',@Q))
and (@Status is null or d.status=@Status) and (@Sigilo is null or d.confidencialidade=@Sigilo) order by d.created_at desc limit 200";
        var rows = await cn.QueryAsync<Sigov.Web.Models.Ged360.GedDocumentoListItem>(new CommandDefinition(sql, new { TenantId = TenantId(), Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(), Status = status, Sigilo = sigilo }, cancellationToken: cancellationToken));
        ViewBag.Query = q; return View("~/Views/Ged/Documentos360.cshtml", rows.AsList());
    }

    [HttpGet("/GED/{area:regex(Importacoes|OCR|Classificacao|Temporalidade|Protocolos|Tramitacoes|Workflows|Assinaturas|AcervoFisico|Caixas|Emprestimos|Eliminacoes|Integracoes|Auditoria|Relatorios)}")]
    [HttpGet("/GED/OCR/Revisao")]
    public IActionResult Area(string area) { if (!CanAny("GED_DOCUMENTO_VIEW", "ged.visualizar")) return Forbid(); ViewBag.Area = area; return View("~/Views/Ged/Area360.cshtml"); }

    [HttpGet("/Ged/NovoDocumento")]
    [HttpGet("/Ged/Novo")]
    [HttpGet("/GED/Documentos/Create")]
    public async Task<IActionResult> NovoDocumento(string? q = null, CancellationToken cancellationToken = default)
    { if (!Can("ged.upload")) return Forbid(); return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "NovoDocumento real", q, cancellationToken)); }

    [HttpPost("/Ged/NovoDocumento"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoDocumentoPost(IFormFile? arquivo, string? titulo, string? classificacaoLgpd, long? protocolo_id, CancellationToken cancellationToken)
    {
        if (!Can("ged.upload")) return Forbid();
        if (arquivo is null || arquivo.Length == 0) { TempData["Warning"] = "Arquivo obrigatório; upload real não executado."; return RedirectToAction(nameof(NovoDocumento)); }
        if (arquivo.Length > 25 * 1024 * 1024) { TempData["Warning"] = "Arquivo acima do limite configurado."; return RedirectToAction(nameof(NovoDocumento)); }
        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (!new[] { ".pdf", ".png", ".jpg", ".jpeg", ".txt", ".csv", ".docx" }.Contains(ext)) { TempData["Warning"] = "Extensão não permitida."; return RedirectToAction(nameof(NovoDocumento)); }
        if (!await _schema.TableExistsAsync("sigov", "documento", cancellationToken)) { TempData["Warning"] = "Schema GED real indisponível; upload não foi simulado."; return RedirectToAction(nameof(Index)); }
        try
        {
            var storage = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "ged", DateTime.UtcNow.ToString("yyyyMMdd")); Directory.CreateDirectory(storage);
            var safeName = $"{Guid.NewGuid():N}{ext}"; var path = Path.Combine(storage, safeName);
            await using (var fs = System.IO.File.Create(path)) await arquivo.CopyToAsync(fs, cancellationToken);
            var hash = Convert.ToHexString(SHA256.HashData(await System.IO.File.ReadAllBytesAsync(path, cancellationToken))).ToLowerInvariant();
            using var cn = _connectionFactory.CreateConnection(); var correlationId = Guid.NewGuid(); var lgpd = string.IsNullOrWhiteSpace(classificacaoLgpd) ? "INTERNO" : classificacaoLgpd.ToUpperInvariant();
            const string insertDocumento = "insert into sigov.documento (tenant_id, titulo, nome_arquivo, content_type, tamanho_bytes, hash_sha256, classificacao_lgpd, storage_path, status, created_by, correlation_id) values (1,@Titulo,@Nome,@ContentType,@Tamanho,@Hash,@Lgpd,@Path,'ATIVO',1,@CorrelationId) returning id";
            var id = await cn.ExecuteScalarAsync<long>(new CommandDefinition(insertDocumento, new { Titulo = string.IsNullOrWhiteSpace(titulo) ? Path.GetFileNameWithoutExtension(arquivo.FileName) : titulo, Nome = Path.GetFileName(arquivo.FileName), arquivo.ContentType, Tamanho = arquivo.Length, Hash = hash, Lgpd = lgpd, Path = path, CorrelationId = correlationId }, cancellationToken: cancellationToken));
            await TryExecuteAsync(cn, "insert into sigov.documento_versao (tenant_id, documento_id, versao, hash_sha256, storage_path, tamanho_bytes, created_by, correlation_id) values (1,@Id,1,@Hash,@Path,@Tamanho,1,@CorrelationId)", new { Id = id, Hash = hash, Path = path, Tamanho = arquivo.Length, CorrelationId = correlationId }, cancellationToken);
            if (protocolo_id.HasValue) await TryExecuteAsync(cn, "insert into sigov.protocolo_anexo (tenant_id, protocolo_id, documento_id, created_by, correlation_id) values (1,@ProtocoloId,@Id,1,@CorrelationId)", new { ProtocoloId = protocolo_id, Id = id, CorrelationId = correlationId }, cancellationToken);
            if (lgpd == "PUBLICO") await TryExecuteAsync(cn, "insert into sigov.portal_validacao_documento (tenant_id, documento_id, codigo, hash_sha256, status, created_at) values (1,@Id,@Codigo,@Hash,'VALIDO',now())", new { Id = id, Codigo = hash[..12], Hash = hash }, cancellationToken);
            await TryExecuteAsync(cn, "insert into sigov.outbox_evento (tenant_id, evento, payload, status, correlation_id, created_at) values (1,'documento.criado',cast(@Payload as jsonb),'PENDENTE',@CorrelationId,now())", new { Payload = System.Text.Json.JsonSerializer.Serialize(new { id, hash }), CorrelationId = correlationId }, cancellationToken);
            await Audit("ged.upload", id.ToString(), cancellationToken); return Redirect($"/Ged/Detalhes/{id}");
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Upload GED real indisponível."); TempData["Warning"] = "Upload não persistido; fallback honesto registrado."; return RedirectToAction(nameof(Index)); }
    }

    [HttpGet("/Ged/Detalhes/{id:long}")]
    [HttpGet("/GED/Documentos/Details/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) { if (!Can("ged.visualizar")) return Forbid(); await Audit("ged.visualizar", id.ToString(), cancellationToken); return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", $"Detalhes #{id}", null, cancellationToken)); }
    [HttpGet("/GED/Documentos/Edit/{id:long}")]
    public async Task<IActionResult> Editar(long id, CancellationToken cancellationToken) { if (!CanAny("GED_DOCUMENTO_MANAGE", "ged.upload")) return Forbid(); return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", $"Editar documento #{id}", null, cancellationToken)); }
    [HttpGet("/Ged/Download/{id:long}")]
    [HttpGet("/Ged/{id:long}/Download")]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken) => await AcessarArquivo(id, true, cancellationToken);
    [HttpGet("/Ged/Visualizar/{id:long}")]
    [HttpGet("/Ged/{id:long}/Visualizar")]
    public async Task<IActionResult> Visualizar(long id, CancellationToken cancellationToken) => await AcessarArquivo(id, false, cancellationToken);
    private async Task<IActionResult> AcessarArquivo(long id, bool download, CancellationToken ct)
    { if (!Can(download ? "ged.download" : "ged.visualizar")) return Forbid(); await Audit(download ? "ged.download" : "ged.visualizar", id.ToString(), ct); TempData["Warning"] = "Arquivo protegido: o path físico nunca é exposto; stream real depende do registro GED."; return Redirect($"/Ged/Detalhes/{id}"); }
    private bool Can(string permission) => User.Identity?.IsAuthenticated == true && _permissions.HasPermission(User, permission);
    private bool CanAny(params string[] permissions) => permissions.Any(Can);
    private long TenantId() => _tenantContext.Resolve().TenantId ?? throw new InvalidOperationException("tenant_id obrigatório para acessar o GED360.");
    private Task Audit(string acao, string? id, CancellationToken ct) => _auditTrail.RegistrarAsync(null, null, acao, "documento", id, null, new { acao, id }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct);
    private async Task TryExecuteAsync(System.Data.IDbConnection cn, string sql, object args, CancellationToken ct)
    {
        try
        {
            await cn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Persistência complementar do GED indisponível. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
        }
    }
}
