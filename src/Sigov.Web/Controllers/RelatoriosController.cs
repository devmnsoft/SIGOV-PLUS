using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Helpers;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class RelatoriosController : Controller
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSchemaInspector _schemaInspector;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<RelatoriosController> _logger;

    public RelatoriosController(ILogger<RelatoriosController> logger, NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schemaInspector, IAuditTrailService auditTrail)
    {
        _logger = logger;
        _connectionFactory = connectionFactory;
        _schemaInspector = schemaInspector;
        _auditTrail = auditTrail;
    }

    [Route("/Relatorios")]
    public IActionResult Index()
    {
        try { return View(); }
        catch (Exception ex) { _logger.LogError(ex, "Falha relatórios"); TempData["Error"]="Relatórios indisponíveis."; return View(); }
    }

    [HttpGet("/Relatorios/UsuariosCsv")]
    public async Task<IActionResult> UsuariosCsv(CancellationToken cancellationToken)
    {
        if (!await _schemaInspector.TableExistsAsync("sigov", "usuario", cancellationToken).ConfigureAwait(false)) return Csv("mensagem\nTabela sigov.usuario indisponível; exportação não gerada.\n", "usuarios-indisponivel.csv");
        try
        {
            using var cn = _connectionFactory.CreateConnection();
            var rows = await cn.QueryAsync<UsuarioCsvRow>(new CommandDefinition("select id, coalesce(nome, login) as nome, login, coalesce(email,'') as email, ativo from sigov.usuario where coalesce(is_deleted,false)=false and ativo=true order by 2 limit 500;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            await AuditarExportacaoAsync("RELATORIO_USUARIOS_CSV", cancellationToken).ConfigureAwait(false);
            return Csv(ToCsv(new[] { "id;nome;login;email;ativo" }, rows.Select(x => $"{x.Id};{Escape(LgpdMaskingHelper.MaskName(x.Nome))};{Escape(x.Login)};{Escape(LgpdMaskingHelper.MaskEmail(x.Email))};{x.Ativo}")), "usuarios-ativos.csv");
        }
        catch (Exception ex) { _logger.LogError(ex, "Falha ao exportar usuários."); return Csv("mensagem\nExportação indisponível no momento.\n", "usuarios-erro.csv"); }
    }

    [HttpGet("/Relatorios/TenantsCsv")]
    public async Task<IActionResult> TenantsCsv(CancellationToken cancellationToken)
    {
        if (!await _schemaInspector.TableExistsAsync("sigov", "tenant", cancellationToken).ConfigureAwait(false)) return Csv("mensagem\nTabela sigov.tenant indisponível; exportação não gerada.\n", "tenants-indisponivel.csv");
        try
        {
            using var cn = _connectionFactory.CreateConnection();
            var rows = await cn.QueryAsync<TenantCsvRow>(new CommandDefinition("select id,nome,slug,coalesce(documento,'') as documento,coalesce(email,'') as email,ativo from sigov.tenant where coalesce(is_deleted,false)=false and ativo=true order by nome limit 500;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            await AuditarExportacaoAsync("RELATORIO_TENANTS_CSV", cancellationToken).ConfigureAwait(false);
            return Csv(ToCsv(new[] { "id;nome;slug;documento;email;ativo" }, rows.Select(x => $"{x.Id};{Escape(x.Nome)};{Escape(x.Slug)};{Escape(LgpdMaskingHelper.MaskDocument(x.Documento))};{Escape(LgpdMaskingHelper.MaskEmail(x.Email))};{x.Ativo}")), "tenants-ativos.csv");
        }
        catch (Exception ex) { _logger.LogError(ex, "Falha ao exportar tenants."); return Csv("mensagem\nExportação indisponível no momento.\n", "tenants-erro.csv"); }
    }

    [HttpGet("/Relatorios/ModulosCsv")]
    public async Task<IActionResult> ModulosCsv(CancellationToken cancellationToken)
    {
        if (!await _schemaInspector.TableExistsAsync("sigov", "tenant_modulo_contratado", cancellationToken).ConfigureAwait(false)) return Csv("mensagem\nTabela sigov.tenant_modulo_contratado indisponível; exportação não gerada.\n", "modulos-indisponivel.csv");
        try
        {
            using var cn = _connectionFactory.CreateConnection();
            var rows = await cn.QueryAsync<ModuloCsvRow>(new CommandDefinition("select tenant_id, modulo_codigo, status, ativo from sigov.tenant_modulo_contratado order by tenant_id, modulo_codigo limit 1000;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            await AuditarExportacaoAsync("RELATORIO_MODULOS_CSV", cancellationToken).ConfigureAwait(false);
            return Csv(ToCsv(new[] { "tenant_id;modulo;status;ativo" }, rows.Select(x => $"{x.Tenant_Id};{Escape(x.Modulo_Codigo)};{Escape(x.Status)};{x.Ativo}")), "modulos-por-tenant.csv");
        }
        catch (Exception ex) { _logger.LogError(ex, "Falha ao exportar módulos."); return Csv("mensagem\nExportação indisponível no momento.\n", "modulos-erro.csv"); }
    }

    [HttpGet("/Relatorios/AuditoriasCsv")]
    public async Task<IActionResult> AuditoriasCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("auditoria_evento", "select acao, entidade, entidade_id, created_at from sigov.auditoria_evento order by created_at desc limit 500;", "acao;entidade;entidade_id;data", "auditorias-recentes.csv", cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/ParametrosCsv")]
    public async Task<IActionResult> ParametrosCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("parametro_sistema", "select chave, escopo, categoria, case when lower(chave) like '%senha%' or lower(chave) like '%token%' then '***' else valor::text end as valor from sigov.parametro_sistema order by categoria, chave limit 500;", "chave;escopo;categoria;valor", "parametros.csv", cancellationToken).ConfigureAwait(false);

    private async Task<IActionResult> ExportSimpleAsync(string table, string sql, string header, string fileName, CancellationToken cancellationToken)
    {
        if (!await _schemaInspector.TableExistsAsync("sigov", table, cancellationToken).ConfigureAwait(false)) return Csv($"mensagem\nTabela sigov.{table} indisponível; exportação não gerada.\n", fileName);
        try { using var cn = _connectionFactory.CreateConnection(); var rows = await cn.QueryAsync<dynamic>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false); await AuditarExportacaoAsync($"RELATORIO_{table.ToUpperInvariant()}_CSV", cancellationToken).ConfigureAwait(false); return Csv(ToCsv(new[] { header }, rows.Select(r => string.Join(';', ((IDictionary<string, object>)r).Values.Select(v => Escape(Convert.ToString(v) ?? string.Empty))))), fileName); }
        catch (Exception ex) { _logger.LogError(ex, "Falha ao exportar {Table}.", table); return Csv("mensagem\nExportação indisponível no momento.\n", fileName); }
    }

    private async Task AuditarExportacaoAsync(string acao, CancellationToken ct) => await _auditTrail.RegistrarAsync(null, null, acao, "relatorios", null, null, new { usuario = User.Identity?.Name }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
    private FileContentResult Csv(string content, string fileName) => File(new UTF8Encoding(true).GetBytes(content), "text/csv; charset=utf-8", fileName);
    private static string ToCsv(IEnumerable<string> headers, IEnumerable<string> rows) => string.Join('\n', headers.Concat(rows)) + "\n";
    private static string Escape(string value) => value.Replace(";", ",", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    private sealed record UsuarioCsvRow(long Id, string Nome, string Login, string Email, bool Ativo);
    private sealed record TenantCsvRow(long Id, string Nome, string Slug, string Documento, string Email, bool Ativo);
    private sealed record ModuloCsvRow(long Tenant_Id, string Modulo_Codigo, string Status, bool Ativo);
}
