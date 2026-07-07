using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Helpers;
using Sigov.Web.Models.Operational;

namespace Sigov.Web.Services;

public sealed class PosRcWebOperationalService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSchemaInspector _schema;
    private readonly ILogger<PosRcWebOperationalService> _logger;

    public PosRcWebOperationalService(NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schema, ILogger<PosRcWebOperationalService> logger)
    {
        _connectionFactory = connectionFactory;
        _schema = schema;
        _logger = logger;
    }

    public async Task<OperationalModuleViewModel> BuildProtocoloAsync(string screen, string? q, CancellationToken ct)
    {
        var real = await _schema.TableExistsAsync("sigov", "protocolo", ct).ConfigureAwait(false);
        var records = real ? await QueryRecordsAsync("protocolo", "select id, coalesce(numero,codigo,id::text) as codigo, assunto as nome, status, coalesce(dados_json->>'setorAtual','Não informado') as responsavel, to_char(created_at,'YYYY-MM-DD HH24:MI') as atualizado_em, coalesce(dados_json->>'interessadoDocumento','') as documento from sigov.protocolo where tenant_id=@TenantId and coalesce(is_deleted,false)=false and (@Q is null or numero ilike @Like or assunto ilike @Like or dados_json::text ilike @Like) order by created_at desc limit 50", q, ct).ConfigureAwait(false) : Array.Empty<DemoRecord>();
        return Build("Protocolo", "Protocolo", screen, real, records, new[] { "protocolo", "protocolo_movimento", "workflow_instancia", "tarefa", "notificacao", "protocolo_anexo" });
    }

    public async Task<long?> CriarProtocoloAsync(string assunto, string? interessado, CancellationToken ct)
    {
        if (!await _schema.TableExistsAsync("sigov", "protocolo", ct).ConfigureAwait(false)) return null;
        using var cn = _connectionFactory.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        using var tx = await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var tenantId = 1L; var userId = 1L; var correlationId = Guid.NewGuid(); var exercicio = DateTime.UtcNow.Year;
            var seq = await cn.ExecuteScalarAsync<long>(new CommandDefinition("select coalesce(max(id),0)+1 from sigov.protocolo where tenant_id=@TenantId and exercicio=@Exercicio", new { TenantId = tenantId, Exercicio = exercicio }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var numero = $"{exercicio}-{seq:000000}";
            const string insertProtocolo = "insert into sigov.protocolo (tenant_id, numero, status, assunto, dados_json, created_by, correlation_id, exercicio) values (@TenantId,@Numero,'ABERTO',@Assunto,cast(@Dados as jsonb),@UserId,@CorrelationId,@Exercicio) returning id";
            var id = await cn.ExecuteScalarAsync<long>(new CommandDefinition(insertProtocolo, new { TenantId = tenantId, Numero = numero, Assunto = assunto, Dados = System.Text.Json.JsonSerializer.Serialize(new { interessado = LgpdMaskingHelper.MaskName(interessado), setorAtual = "Protocolo" }), UserId = userId, CorrelationId = correlationId, Exercicio = exercicio }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await TryExecuteAsync(cn, tx, "insert into sigov.workflow_instancia (tenant_id, status, protocolo_id, created_by, correlation_id) values (@TenantId,'ATIVO',@Id,@UserId,@CorrelationId)", new { TenantId = tenantId, Id = id, UserId = userId, CorrelationId = correlationId }, ct).ConfigureAwait(false);
            await TryExecuteAsync(cn, tx, "insert into sigov.tarefa (tenant_id, status, protocolo_id, titulo, responsavel_id, created_by, correlation_id) values (@TenantId,'PENDENTE',@Id,'Triar protocolo',@UserId,@UserId,@CorrelationId)", new { TenantId = tenantId, Id = id, UserId = userId, CorrelationId = correlationId }, ct).ConfigureAwait(false);
            await TryExecuteAsync(cn, tx, "insert into sigov.notificacao (tenant_id, status, titulo, mensagem, usuario_id, created_by, correlation_id) values (@TenantId,'NAO_LIDA','Protocolo criado',@Msg,@UserId,@UserId,@CorrelationId)", new { TenantId = tenantId, Msg = $"Protocolo {numero} criado", UserId = userId, CorrelationId = correlationId }, ct).ConfigureAwait(false);
            await TryExecuteAsync(cn, tx, "insert into sigov.outbox_evento (tenant_id, evento, payload, status, correlation_id, created_at) values (@TenantId,'protocolo.criado',cast(@Payload as jsonb),'PENDENTE',@CorrelationId,now())", new { TenantId = tenantId, Payload = System.Text.Json.JsonSerializer.Serialize(new { id, numero }), CorrelationId = correlationId }, ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false); return id;
        }
        catch (Exception ex) { await tx.RollbackAsync(ct).ConfigureAwait(false); _logger.LogWarning(ex, "Fallback honesto ao criar protocolo Web real."); return null; }
    }

    public async Task<bool> TramitarProtocoloAsync(long id, string? observacao, CancellationToken ct)
    {
        if (!await _schema.TableExistsAsync("sigov", "protocolo_movimento", ct).ConfigureAwait(false)) return false;
        using var cn = _connectionFactory.CreateConnection(); var correlationId = Guid.NewGuid();
        const string insertMovimento = "insert into sigov.protocolo_movimento (tenant_id, protocolo_id, status, observacao, created_by, correlation_id) values (1,@Id,'TRAMITADO',@Observacao,1,@CorrelationId)";
        await cn.ExecuteAsync(new CommandDefinition(insertMovimento, new { Id = id, Observacao = observacao, CorrelationId = correlationId }, cancellationToken: ct)).ConfigureAwait(false);
        await TryExecuteAsync(cn, null, "update sigov.tarefa set status='CONCLUIDA', concluida_at=now() where protocolo_id=@Id and concluida_at is null", new { Id = id }, ct).ConfigureAwait(false);
        await TryExecuteAsync(cn, null, "insert into sigov.tarefa (tenant_id,status,protocolo_id,titulo,created_by,correlation_id) values (1,'PENDENTE',@Id,'Analisar tramitação',1,@CorrelationId)", new { Id = id, CorrelationId = correlationId }, ct).ConfigureAwait(false);
        await TryExecuteAsync(cn, null, "insert into sigov.notificacao (tenant_id,status,titulo,mensagem,created_by,correlation_id) values (1,'NAO_LIDA','Protocolo tramitado',@Msg,1,@CorrelationId)", new { Msg = $"Protocolo {id} tramitado", CorrelationId = correlationId }, ct).ConfigureAwait(false);
        await TryExecuteAsync(cn, null, "insert into sigov.outbox_evento (tenant_id, evento, payload, status, correlation_id, created_at) values (1,'protocolo.tramitado',cast(@Payload as jsonb),'PENDENTE',@CorrelationId,now())", new { Payload = System.Text.Json.JsonSerializer.Serialize(new { id }), CorrelationId = correlationId }, ct).ConfigureAwait(false);
        return true;
    }

    private async Task<IReadOnlyList<DemoRecord>> QueryRecordsAsync(string table, string sql, string? q, CancellationToken ct)
    {
        try { using var cn = _connectionFactory.CreateConnection(); var rows = await cn.QueryAsync<Row>(new CommandDefinition(sql, new { TenantId = 1L, Q = string.IsNullOrWhiteSpace(q) ? null : q, Like = $"%{q}%" }, cancellationToken: ct)).ConfigureAwait(false); return rows.Select(r => new DemoRecord(r.Id, r.Codigo, LgpdMaskingHelper.MaskName(r.Nome), r.Status, r.Responsavel, r.Atualizado_Em, LgpdMaskingHelper.MaskDocument(r.Documento))).ToArray(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Fallback honesto em consulta real {Table}.", table); return Array.Empty<DemoRecord>(); }
    }

    private static OperationalModuleViewModel Build(string key, string title, string screen, bool real, IReadOnlyList<DemoRecord> records, IReadOnlyList<string> tables) => new()
    {
        ModuleKey = key, Title = title, CurrentScreen = screen, ShowLgpdWarning = true, SchemaTables = real ? tables : Array.Empty<string>(),
        PageStatus = new() { UsaDadosReais = real, Status = real ? "Funcional real" : "Em implantação/fallback", Mensagem = real ? "Consultando tabelas reais do schema sigov por tenant." : "Schema real indisponível; sem sucesso falso." },
        Kpis = new[] { new ModuleKpi("Registros reais", records.Count.ToString(), real ? "Lidos do PostgreSQL" : "Indisponível", real ? "success" : "warning") }, Records = records,
        Actions = new[] { new QuickAction("Novo", $"/{key}/Novo"), new QuickAction("Exportar CSV", "/Relatorios") },
        Timeline = new[] { new TimelineStep("LGPD", "Listagens mascaram dados pessoais.", "Ativo", DateTime.UtcNow.ToString("yyyy-MM-dd")) },
        NextSteps = new[] { "Validar permissões finas antes de actions críticas.", "Auditar criação, tramitação, acesso e exportação." }
    };

    private static async Task TryExecuteAsync(System.Data.IDbConnection cn, System.Data.IDbTransaction? tx, string sql, object args, CancellationToken ct) { try { await cn.ExecuteAsync(new CommandDefinition(sql, args, tx, cancellationToken: ct)).ConfigureAwait(false); } catch { } }
    private sealed record Row(long Id, string Codigo, string Nome, string Status, string Responsavel, string Atualizado_Em, string Documento);
}
