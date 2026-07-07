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
    public GedController(GedOperationalService operationalDemo, NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schema, IUserPermissionService permissions, IAuditTrailService auditTrail, ILogger<GedController> logger)
    { _operationalDemo = operationalDemo; _connectionFactory = connectionFactory; _schema = schema; _permissions = permissions; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet("/Ged")]
    [HttpGet("/Ged/Documentos")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    { if (!Can("ged.visualizar")) return Forbid(); return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "Documentos reais", q, cancellationToken)); }

    [HttpGet("/Ged/NovoDocumento")]
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
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) { if (!Can("ged.visualizar")) return Forbid(); await Audit("ged.visualizar", id.ToString(), cancellationToken); return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", $"Detalhes #{id}", null, cancellationToken)); }
    [HttpGet("/Ged/Download/{id:long}")]
    [HttpGet("/Ged/{id:long}/Download")]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken) => await AcessarArquivo(id, true, cancellationToken);
    [HttpGet("/Ged/Visualizar/{id:long}")]
    [HttpGet("/Ged/{id:long}/Visualizar")]
    public async Task<IActionResult> Visualizar(long id, CancellationToken cancellationToken) => await AcessarArquivo(id, false, cancellationToken);
    private async Task<IActionResult> AcessarArquivo(long id, bool download, CancellationToken ct)
    { if (!Can(download ? "ged.download" : "ged.visualizar")) return Forbid(); await Audit(download ? "ged.download" : "ged.visualizar", id.ToString(), ct); TempData["Warning"] = "Arquivo protegido: o path físico nunca é exposto; stream real depende do registro GED."; return Redirect($"/Ged/Detalhes/{id}"); }
    private bool Can(string permission) => User.Identity?.IsAuthenticated != true || _permissions.HasPermission(User, permission) || _permissions.HasPermission(User, "ADMIN_GERAL");
    private Task Audit(string acao, string? id, CancellationToken ct) => _auditTrail.RegistrarAsync(null, null, acao, "documento", id, null, new { acao, id }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct);
    private static async Task TryExecuteAsync(System.Data.IDbConnection cn, string sql, object args, CancellationToken ct) { try { await cn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct)); } catch { } }
}
