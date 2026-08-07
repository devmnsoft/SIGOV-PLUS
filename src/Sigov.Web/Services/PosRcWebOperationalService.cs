using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Helpers;
using Sigov.Web.Models.Operational;
using Sigov.Web.Models.Protocolo;
using System.Security.Claims;

namespace Sigov.Web.Services;

public sealed class PosRcWebOperationalService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSchemaInspector _schema;
    private readonly ILogger<PosRcWebOperationalService> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PosRcWebOperationalService(NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schema, ILogger<PosRcWebOperationalService> logger, ITenantContextAccessor tenantContextAccessor, IHttpContextAccessor httpContextAccessor)
    {
        _connectionFactory = connectionFactory;
        _schema = schema;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<OperationalModuleViewModel> BuildProtocoloAsync(string screen, string? q, CancellationToken ct)
    {
        var real = await _schema.TableExistsAsync("sigov", "protocolo", ct).ConfigureAwait(false);
        var records = real ? await QueryRecordsAsync("protocolo", "select id, coalesce(numero,codigo,id::text) as codigo, assunto as nome, status, coalesce(dados_json->>'setorAtual','Não informado') as responsavel, to_char(created_at,'YYYY-MM-DD HH24:MI') as atualizado_em, coalesce(dados_json->>'interessadoDocumento','') as documento from sigov.protocolo where tenant_id=@TenantId and coalesce(is_deleted,false)=false and (@Q is null or numero ilike @Like or assunto ilike @Like or dados_json::text ilike @Like) order by created_at desc limit 50", q, ct).ConfigureAwait(false) : Array.Empty<DemoRecord>();
        return Build("Protocolo", "Protocolo", screen, real, records, new[] { "protocolo", "protocolo_movimento", "workflow_instancia", "tarefa", "notificacao", "protocolo_anexo" });
    }

    public async Task<long?> CriarProtocoloAsync(ProtocoloFormViewModel model, CancellationToken ct)
    {
        if (!await _schema.TableExistsAsync("sigov", "protocolo", ct).ConfigureAwait(false)) return null;
        using var cn = _connectionFactory.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        using var tx = await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var tenantId = _tenantContextAccessor.Resolve().TenantId; if (!tenantId.HasValue) return null; var userId = CurrentUserId(); var correlationId = CurrentCorrelationId(); var exercicio = CurrentExercise();
            var numero = await cn.ExecuteScalarAsync<string>(new CommandDefinition("select sigov.proximo_numero_protocolo(@TenantId,@Exercicio)", new { TenantId = tenantId.Value, Exercicio = exercicio }, tx, cancellationToken: ct)).ConfigureAwait(false);
            const string insertProtocolo = "insert into sigov.protocolo (tenant_id, entidade_id, exercicio_id, numero, status, assunto, dados_json, created_by, correlation_id, exercicio) values (@TenantId,@EntidadeId,@ExercicioId,@Numero,'ABERTO',@Assunto,cast(@Dados as jsonb),@UserId,@CorrelationId,@Exercicio) returning id";
            var id = await cn.ExecuteScalarAsync<long>(new CommandDefinition(insertProtocolo, new { TenantId = tenantId.Value, EntidadeId = CurrentClaim("entidade_id"), ExercicioId = CurrentClaim("exercicio_id"), Numero = numero, Assunto = model.Assunto.Trim(), Dados = System.Text.Json.JsonSerializer.Serialize(new { interessado = LgpdMaskingHelper.MaskName(model.Interessado), interessadoDocumento = LgpdMaskingHelper.MaskDocument(model.Documento), categoria = model.Categoria, prioridade = model.Prioridade, setorAtual = model.UnidadeDestino, observacao = model.Observacao, tags = model.Tags }), UserId = userId, CorrelationId = correlationId, Exercicio = exercicio }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await RegistrarTimelineAsync(cn, tx, tenantId.Value, id, "CRIACAO", $"Protocolo {numero} criado.", userId, correlationId, ct).ConfigureAwait(false);
            await TryExecuteAsync(cn, tx, "insert into sigov.workflow_instancia (tenant_id, status, protocolo_id, created_by, correlation_id) values (@TenantId,'ATIVO',@Id,@UserId,@CorrelationId)", new { TenantId = tenantId.Value, Id = id, UserId = userId, CorrelationId = correlationId }, ct).ConfigureAwait(false);
            await TryExecuteAsync(cn, tx, "insert into sigov.tarefa (tenant_id, status, protocolo_id, titulo, responsavel_id, created_by, correlation_id) values (@TenantId,'PENDENTE',@Id,'Triar protocolo',@UserId,@UserId,@CorrelationId)", new { TenantId = tenantId.Value, Id = id, UserId = userId, CorrelationId = correlationId }, ct).ConfigureAwait(false);
            await TryExecuteAsync(cn, tx, "insert into sigov.notificacao (tenant_id, status, titulo, mensagem, usuario_id, created_by, correlation_id) values (@TenantId,'NAO_LIDA','Protocolo criado',@Msg,@UserId,@UserId,@CorrelationId)", new { TenantId = tenantId.Value, Msg = $"Protocolo {numero} criado", UserId = userId, CorrelationId = correlationId }, ct).ConfigureAwait(false);
            await TryExecuteAsync(cn, tx, "insert into sigov.outbox_evento (tenant_id, evento, payload, status, correlation_id, created_at) values (@TenantId,'protocolo.criado',cast(@Payload as jsonb),'PENDENTE',@CorrelationId,now())", new { TenantId = tenantId.Value, Payload = System.Text.Json.JsonSerializer.Serialize(new { id, numero }), CorrelationId = correlationId }, ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false); return id;
        }
        catch (Exception ex) { await tx.RollbackAsync(ct).ConfigureAwait(false); _logger.LogWarning(ex, "Fallback honesto ao criar protocolo Web real."); return null; }
    }

    public async Task<bool> TramitarProtocoloAsync(long id, string? observacao, CancellationToken ct)
    {
        if (!await _schema.TableExistsAsync("sigov", "protocolo_movimento", ct).ConfigureAwait(false)) return false;
        if (string.IsNullOrWhiteSpace(observacao)) return false;
        using var cn = _connectionFactory.CreateConnection(); var correlationId = CurrentCorrelationId(); var tenantId = _tenantContextAccessor.Resolve().TenantId; if (!tenantId.HasValue) return false; var userId = CurrentUserId();
        const string insertMovimento = "insert into sigov.protocolo_movimento (tenant_id, protocolo_id, status, observacao, created_by, correlation_id) select @TenantId,@Id,'TRAMITADO',@Observacao,@UserId,@CorrelationId where exists(select 1 from sigov.protocolo where tenant_id=@TenantId and id=@Id and coalesce(is_deleted,false)=false)";
        if (await cn.ExecuteAsync(new CommandDefinition(insertMovimento, new { TenantId = tenantId.Value, Id = id, Observacao = observacao.Trim(), UserId = userId, CorrelationId = correlationId }, cancellationToken: ct)).ConfigureAwait(false) != 1) return false;
        await cn.ExecuteAsync(new CommandDefinition("update sigov.protocolo set status='EM_TRAMITACAO',updated_at=now(),updated_by=@UserId where tenant_id=@TenantId and id=@Id", new { TenantId=tenantId.Value, Id=id, UserId=userId }, cancellationToken:ct)).ConfigureAwait(false);
        await RegistrarTimelineAsync(cn, null, tenantId.Value, id, "TRAMITACAO", observacao.Trim(), userId, correlationId, ct).ConfigureAwait(false);
        await TryExecuteAsync(cn, null, "update sigov.tarefa set status='CONCLUIDA', concluida_at=now() where tenant_id=@TenantId and protocolo_id=@Id and concluida_at is null", new { TenantId = tenantId.Value, Id = id }, ct).ConfigureAwait(false);
        await TryExecuteAsync(cn, null, "insert into sigov.tarefa (tenant_id,status,protocolo_id,titulo,created_by,correlation_id) values (@TenantId,'PENDENTE',@Id,'Analisar tramitação',@UserId,@CorrelationId)", new { TenantId = tenantId.Value, Id = id, UserId = userId, CorrelationId = correlationId }, ct).ConfigureAwait(false);
        await TryExecuteAsync(cn, null, "insert into sigov.notificacao (tenant_id,status,titulo,mensagem,created_by,correlation_id) values (@TenantId,'NAO_LIDA','Protocolo tramitado',@Msg,@UserId,@CorrelationId)", new { TenantId = tenantId.Value, Msg = $"Protocolo {id} tramitado", UserId = userId, CorrelationId = correlationId }, ct).ConfigureAwait(false);
        await TryExecuteAsync(cn, null, "insert into sigov.outbox_evento (tenant_id, evento, payload, status, correlation_id, created_at) values (@TenantId,'protocolo.tramitado',cast(@Payload as jsonb),'PENDENTE',@CorrelationId,now())", new { TenantId = tenantId.Value, Payload = System.Text.Json.JsonSerializer.Serialize(new { id }), CorrelationId = correlationId }, ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> AlterarStatusProtocoloAsync(long id, string status, string justificativa, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(justificativa) || status is not ("CONCLUIDO" or "ARQUIVADO")) return false;
        var tenantId = _tenantContextAccessor.Resolve().TenantId; if (!tenantId.HasValue) return false;
        try
        {
            await using var cn = _connectionFactory.CreateConnection(); var userId = CurrentUserId(); var correlationId = CurrentCorrelationId();
            var changed = await cn.ExecuteAsync(new CommandDefinition("update sigov.protocolo set status=@Status,updated_at=now(),updated_by=@UserId where tenant_id=@TenantId and id=@Id and coalesce(is_deleted,false)=false and status not in ('ARQUIVADO')", new { Status=status, UserId=userId, TenantId=tenantId.Value, Id=id }, cancellationToken:ct)).ConfigureAwait(false);
            if (changed == 0) return false;
            await RegistrarTimelineAsync(cn, null, tenantId.Value, id, status == "CONCLUIDO" ? "CONCLUSAO" : "ARQUIVAMENTO", justificativa.Trim(), userId, correlationId, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Falha ao alterar protocolo {ProtocoloId} para {Status}.", id, status); return false; }
    }

    public async Task<long?> CriarTarefaDoProtocoloAsync(long protocoloId, ProtocoloTarefaFormViewModel model, string correlationId, CancellationToken ct)
    {
        if (!await _schema.TableExistsAsync("sigov", "tarefa", ct).ConfigureAwait(false)
            || !await _schema.TableExistsAsync("sigov", "tarefa_vinculo", ct).ConfigureAwait(false)) return null;
        var tenantId = _tenantContextAccessor.Resolve().TenantId;
        var userId = CurrentUserId();
        if (!tenantId.HasValue) return null;
        await using var cn = _connectionFactory.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var protocoloExiste = await cn.ExecuteScalarAsync<bool>(new CommandDefinition(
                "select exists(select 1 from sigov.protocolo where id=@ProtocoloId and tenant_id=@TenantId and coalesce(is_deleted,false)=false)",
                new { ProtocoloId = protocoloId, TenantId = tenantId.Value }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (!protocoloExiste) { await tx.RollbackAsync(ct).ConfigureAwait(false); return null; }

            const string insertTarefa = "insert into sigov.tarefa (tenant_id,titulo,descricao,status,prioridade,responsavel_id,prazo_em,origem,entidade,entidade_id,created_by,correlation_id) values (@TenantId,@Titulo,@Descricao,'ABERTA',@Prioridade,@ResponsavelId,@PrazoEm,'PROTOCOLO','protocolo',@ProtocoloId,@UserId,@CorrelationId) returning id";
            var tarefaId = await cn.ExecuteScalarAsync<long>(new CommandDefinition(insertTarefa, new { TenantId = tenantId.Value, Titulo = model.Titulo.Trim(), Descricao = model.Descricao?.Trim(), model.Prioridade, model.ResponsavelId, model.PrazoEm, ProtocoloId = protocoloId.ToString(), UserId = userId, CorrelationId = correlationId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.tarefa_vinculo (tenant_id,tarefa_id,entidade,entidade_id,created_by,correlation_id) values (@TenantId,@TarefaId,'protocolo',@ProtocoloId,@UserId,@CorrelationId)", new { TenantId = tenantId.Value, TarefaId = tarefaId, ProtocoloId = protocoloId.ToString(), UserId = userId, CorrelationId = correlationId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (await _schema.TableExistsAsync("sigov", "notificacao", ct).ConfigureAwait(false))
            {
                var notificacaoId = await cn.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.notificacao (tenant_id,tipo,titulo,mensagem,modulo,prioridade,origem,entidade,entidade_id,created_by,correlation_id) values (@TenantId,'TAREFA_CRIADA','Nova tarefa atribuída',@Mensagem,'Tarefas',@Prioridade,'PROTOCOLO','tarefa',@TarefaId,@UserId,@CorrelationId) returning id", new { TenantId = tenantId.Value, Mensagem = $"A tarefa '{model.Titulo.Trim()}' foi vinculada ao protocolo {protocoloId}.", model.Prioridade, TarefaId = tarefaId.ToString(), UserId = userId, CorrelationId = correlationId }, tx, cancellationToken: ct)).ConfigureAwait(false);
                if (await _schema.TableExistsAsync("sigov", "notificacao_usuario", ct).ConfigureAwait(false))
                    await cn.ExecuteAsync(new CommandDefinition("insert into sigov.notificacao_usuario (tenant_id,notificacao_id,usuario_id,tipo,titulo,lida,created_by,correlation_id) values (@TenantId,@NotificacaoId,@ResponsavelId,'TAREFA_CRIADA','Nova tarefa atribuída',false,@UserId,@CorrelationId)", new { TenantId = tenantId.Value, NotificacaoId = notificacaoId, model.ResponsavelId, UserId = userId, CorrelationId = correlationId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            }
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return tarefaId;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogError(ex, "Falha ao criar tarefa vinculada ao protocolo {ProtocoloId}. CorrelationId {CorrelationId}", protocoloId, correlationId);
            return null;
        }
    }

    public async Task<bool> VincularDocumentoAsync(long protocoloId, long documentoId, string correlationId, CancellationToken ct)
    {
        if (!await _schema.TableExistsAsync("sigov", "protocolo_anexo", ct).ConfigureAwait(false)
            || !await _schema.TableExistsAsync("sigov", "documento", ct).ConfigureAwait(false)) return false;
        var tenantId = _tenantContextAccessor.Resolve().TenantId;
        var userId = CurrentUserId();
        if (!tenantId.HasValue) return false;
        try
        {
            await using var cn = _connectionFactory.CreateConnection();
            const string sql = "insert into sigov.protocolo_anexo (tenant_id,protocolo_id,documento_id,created_by,correlation_id) select @TenantId,@ProtocoloId,@DocumentoId,@UserId,@CorrelationId where exists (select 1 from sigov.protocolo p where p.id=@ProtocoloId and p.tenant_id=@TenantId and coalesce(p.is_deleted,false)=false) and exists (select 1 from sigov.documento d where d.id=@DocumentoId and d.tenant_id=@TenantId and coalesce(d.is_deleted,false)=false) and not exists (select 1 from sigov.protocolo_anexo a where a.tenant_id=@TenantId and a.protocolo_id=@ProtocoloId and a.documento_id=@DocumentoId and coalesce(a.is_deleted,false)=false)";
            return await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId.Value, ProtocoloId = protocoloId, DocumentoId = documentoId, UserId = userId, CorrelationId = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid() }, cancellationToken: ct)).ConfigureAwait(false) == 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao vincular documento {DocumentoId} ao protocolo {ProtocoloId}. CorrelationId {CorrelationId}", documentoId, protocoloId, correlationId);
            return false;
        }
    }

    private async Task<IReadOnlyList<DemoRecord>> QueryRecordsAsync(string table, string sql, string? q, CancellationToken ct)
    {
        try { using var cn = _connectionFactory.CreateConnection(); var rows = await cn.QueryAsync<Row>(new CommandDefinition(sql, new { TenantId = _tenantContextAccessor.Resolve().TenantId, Q = string.IsNullOrWhiteSpace(q) ? null : q, Like = $"%{q}%" }, cancellationToken: ct)).ConfigureAwait(false); return rows.Select(r => new DemoRecord(r.Id, r.Codigo, LgpdMaskingHelper.MaskName(r.Nome), r.Status, r.Responsavel, r.Atualizado_Em, LgpdMaskingHelper.MaskDocument(r.Documento))).ToArray(); }
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
    private long? CurrentUserId() => long.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : null;
    private long? CurrentClaim(string name) => long.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue(name), out var value) ? value : null;
    private int CurrentExercise() => int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("exercicio"), out var value) ? value : DateTime.UtcNow.Year;
    private Guid CurrentCorrelationId() => Guid.TryParse(_httpContextAccessor.HttpContext?.TraceIdentifier, out var value) ? value : Guid.NewGuid();
    private async Task RegistrarTimelineAsync(System.Data.IDbConnection cn, System.Data.IDbTransaction? tx, long tenantId, long id, string acao, string descricao, long? userId, Guid correlationId, CancellationToken ct) =>
        await cn.ExecuteAsync(new CommandDefinition("insert into sigov.timeline_evento (tenant_id,entidade_id,exercicio_id,modulo,entidade,entidade_registro_id,acao,descricao,severidade,usuario_id,correlation_id) values (@TenantId,@EntidadeId,@ExercicioId,'PROTOCOLO','protocolo',@Id,@Acao,@Descricao,'INFO',@UserId,@CorrelationId)", new { TenantId=tenantId, EntidadeId=CurrentClaim("entidade_id"), ExercicioId=CurrentClaim("exercicio_id"), Id=id, Acao=acao, Descricao=descricao, UserId=userId, CorrelationId=correlationId }, tx, cancellationToken:ct)).ConfigureAwait(false);
    private sealed record Row(long Id, string Codigo, string Nome, string Status, string Responsavel, string Atualizado_Em, string Documento);
}
