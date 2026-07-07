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


    [HttpGet("/Relatorios/ContratualCsv/{table}")]
    public async Task<IActionResult> ContratualCsv(string table, CancellationToken cancellationToken)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "implantacao", "implantacao_etapa", "migracao_lote", "migracao_log", "treinamento", "treinamento_participante", "suporte_chamado", "sla_evento", "poc_requisito", "aceite_formal" };
        if (!allowed.Contains(table)) return Csv("mensagem\nRelatório contratual não permitido.\n", "contratual-indisponivel.csv");
        return await ExportSimpleAsync(table, $"select * from sigov.\"{table}\" limit 500;", "dados", $"{table}.csv", cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("/Relatorios/AuditoriasCsv")]
    public async Task<IActionResult> AuditoriasCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("auditoria_evento", "select acao, entidade, entidade_id, created_at from sigov.auditoria_evento order by created_at desc limit 500;", "acao;entidade;entidade_id;data", "auditorias-recentes.csv", cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/ParametrosCsv")]
    public async Task<IActionResult> ParametrosCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("parametro_sistema", "select chave, escopo, categoria, case when lower(chave) like '%senha%' or lower(chave) like '%token%' then '***' else valor::text end as valor from sigov.parametro_sistema order by categoria, chave limit 500;", "chave;escopo;categoria;valor", "parametros.csv", cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/ProtocolosCsv")]
    public async Task<IActionResult> ProtocolosCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("protocolo", "select numero, status, assunto, regexp_replace(coalesce(dados_json->>'interessadoDocumento',''),'([0-9]{3})[0-9]+([0-9]{2})','\\1*****\\2') as interessado_mascarado, created_at from sigov.protocolo where tenant_id=1 and coalesce(is_deleted,false)=false order by created_at desc limit 1000;", "numero;status;assunto;interessado_mascarado;data", Timestamped("protocolos"), cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/DocumentosCsv")]
    public async Task<IActionResult> DocumentosCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("documento", "select codigo, status, titulo, classificacao_lgpd, hash_sha256, created_at from sigov.documento where tenant_id=1 and coalesce(is_deleted,false)=false order by created_at desc limit 1000;", "codigo;status;titulo;classificacao_lgpd;hash_sha256;data", Timestamped("documentos"), cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/TarefasCsv")]
    public async Task<IActionResult> TarefasCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("tarefa", "select t.id, coalesce(p.numero,'') as protocolo, t.titulo, t.status, t.responsavel_id, t.created_at, t.concluida_at from sigov.tarefa t left join sigov.protocolo p on p.id=t.protocolo_id where t.tenant_id=1 and coalesce(t.is_deleted,false)=false order by t.created_at desc limit 1000;", "id;protocolo;titulo;status;responsavel_id;criada_em;concluida_em", Timestamped("tarefas"), cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/NotificacoesCsv")]
    public async Task<IActionResult> NotificacoesCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("notificacao", "select titulo, status, left(coalesce(mensagem,''),180) as mensagem, created_at from sigov.notificacao where tenant_id=1 and coalesce(is_deleted,false)=false order by created_at desc limit 1000;", "titulo;status;mensagem;data", Timestamped("notificacoes"), cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/WorkflowCsv")]
    public async Task<IActionResult> WorkflowCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("workflow_instancia", "select wi.id, coalesce(p.numero,'') as protocolo, wi.status, wi.created_at from sigov.workflow_instancia wi left join sigov.protocolo p on p.id=wi.protocolo_id where wi.tenant_id=1 and coalesce(wi.is_deleted,false)=false order by wi.created_at desc limit 1000;", "id;protocolo;status;data", Timestamped("workflow"), cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/OutboxCsv")]
    public async Task<IActionResult> OutboxCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("outbox_evento", "select evento, agregado, agregado_id, status, tentativas, proxima_tentativa_at, erro_mascarado, created_at from sigov.outbox_evento where tenant_id=1 and coalesce(is_deleted,false)=false order by created_at desc limit 1000;", "evento;agregado;agregado_id;status;tentativas;proxima_tentativa;erro_mascarado;data", Timestamped("outbox"), cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/WebhooksCsv")]
    public async Task<IActionResult> WebhooksCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("webhook_configuracao", "select nome, regexp_replace(url,'(https?://)[^/]+','\\1***') as endpoint_mascarado, eventos::text as eventos, status, created_at from sigov.webhook_configuracao where tenant_id=1 and coalesce(is_deleted,false)=false order by created_at desc limit 1000;", "nome;endpoint_mascarado;eventos;status;data", Timestamped("webhooks"), cancellationToken).ConfigureAwait(false);

    [HttpGet("/Relatorios/AuditoriaOperacionalCsv")]
    public async Task<IActionResult> AuditoriaOperacionalCsv(CancellationToken cancellationToken) => await ExportSimpleAsync("api_requisicao_log", "select endpoint, method, status, status_code, elapsed_ms, started_at from sigov.api_requisicao_log where tenant_id=1 and coalesce(is_deleted,false)=false order by started_at desc limit 1000;", "endpoint;metodo;status;status_code;ms;data", Timestamped("auditoria-operacional"), cancellationToken).ConfigureAwait(false);

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
    private static string Timestamped(string prefix) => $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
    private sealed record UsuarioCsvRow(long Id, string Nome, string Login, string Email, bool Ativo);
    private sealed record TenantCsvRow(long Id, string Nome, string Slug, string Documento, string Email, bool Ativo);
    private sealed record ModuloCsvRow(long Tenant_Id, string Modulo_Codigo, string Status, bool Ativo);
}
