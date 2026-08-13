using Dapper;
using Sigov.Application.ExecutiveOperations;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.ExecutiveOperations;

public sealed class ExecutiveOperationsRepository(DapperContext context) : IExecutiveOperationsRepository
{
    public async Task<GovernanceSummary> GovernanceAsync(long tenantId, long? userId, CancellationToken ct)
    {
        var indicators = await IndicatorsAsync(tenantId, null, ct);
        var pending = await PendenciesAsync(tenantId, new OperationFilter(PageSize: 12), ct);
        using var cn = context.CreateConnection();
        var unread = userId is null ? 0 : await cn.ExecuteScalarAsync<int>(new CommandDefinition("select count(*) from sigov.notificacao_destinatario d join sigov.notificacao_interna n on n.id=d.notificacao_id and n.tenant_id=@tenantId and n.is_deleted=false where d.usuario_id=@userId and not d.lida and not d.arquivada", new { tenantId, userId }, cancellationToken: ct));
        return new GovernanceSummary(indicators, pending.Items, unread, pending.Items.Count(x => x.Severity == "CRITICA"));
    }

    public async Task<Page<OperationalItem>> PendenciesAsync(long tenantId, OperationFilter f, CancellationToken ct)
    {
        using var cn = context.CreateConnection(); var skip=(Math.Max(1,f.Page)-1)*Math.Clamp(f.PageSize,1,100); var take=Math.Clamp(f.PageSize,1,100);
        const string where="tenant_id=@tenantId and status='ABERTA' and is_deleted=false and (@Module is null or modulo=@Module) and (@Severity is null or severidade=@Severity)";
        var p=new {tenantId,Module=Upper(f.Module),Severity=Upper(f.Severity),skip,take};
        var total=await cn.ExecuteScalarAsync<long>(new CommandDefinition($"select count(*) from sigov.qualidade_dados_inconsistencia where {where}",p,cancellationToken:ct));
        var rows=await cn.QueryAsync<OperationalItem>(new CommandDefinition($"select id,modulo as Module,tipo as Type,descricao as Title,severidade as Severity,status as Status,referencia_tipo as ReferenceType,referencia_id as ReferenceId,null::text as Url,created_at as CreatedAt from sigov.qualidade_dados_inconsistencia where {where} order by created_at desc,id desc offset @skip limit @take",p,cancellationToken:ct));
        return new(rows.AsList(),Math.Max(1,f.Page),take,total);
    }

    public async Task<IReadOnlyList<ExecutiveMetric>> IndicatorsAsync(long tenantId, string? module, CancellationToken ct)
    {
        using var cn=context.CreateConnection();
        var rows=await cn.QueryAsync<ExecutiveMetric>(new CommandDefinition("select modulo as Module,'PENDENCIAS' as Key,'Pendências abertas' as Label,count(*)::numeric as Value,case when count(*) filter(where severidade='CRITICA')>0 then 'CRITICA' else 'ATENCAO' end as Severity,null::text as Url from sigov.qualidade_dados_inconsistencia where tenant_id=@tenantId and status='ABERTA' and is_deleted=false and (@module is null or modulo=@module) group by modulo order by modulo",new{tenantId,module=Upper(module)},cancellationToken:ct));
        return rows.AsList();
    }

    public async Task<Page<NotificationItem>> NotificationsAsync(long tenantId,long userId,OperationFilter f,bool unreadOnly,CancellationToken ct)
    {
        using var cn=context.CreateConnection(); var page=Math.Max(1,f.Page);var take=Math.Clamp(f.PageSize,1,100);var skip=(page-1)*take;
        const string where="n.tenant_id=@tenantId and d.usuario_id=@userId and n.is_deleted=false and not d.arquivada and (not @unreadOnly or not d.lida) and (@Module is null or n.modulo=@Module) and (@Severity is null or n.severidade=@Severity)";
        var p=new{tenantId,userId,unreadOnly,Module=Upper(f.Module),Severity=Upper(f.Severity),skip,take};
        var total=await cn.ExecuteScalarAsync<long>(new CommandDefinition($"select count(*) from sigov.notificacao_interna n join sigov.notificacao_destinatario d on d.notificacao_id=n.id where {where}",p,cancellationToken:ct));
        var rows=await cn.QueryAsync<NotificationItem>(new CommandDefinition($"select n.id,n.modulo as Module,n.tipo as Type,n.titulo as Title,n.mensagem as Message,n.severidade as Severity,n.url_destino as Url,d.lida as Read,d.arquivada as Archived,n.created_at as CreatedAt from sigov.notificacao_interna n join sigov.notificacao_destinatario d on d.notificacao_id=n.id where {where} order by n.created_at desc,n.id desc offset @skip limit @take",p,cancellationToken:ct));
        return new(rows.AsList(),page,take,total);
    }

    public async Task MarkNotificationAsync(long tenantId,long userId,long? id,bool archive,CancellationToken ct){using var cn=context.CreateConnection();await cn.ExecuteAsync(new CommandDefinition(archive?"update sigov.notificacao_destinatario d set arquivada=true,arquivada_at=now() from sigov.notificacao_interna n where n.id=d.notificacao_id and n.tenant_id=@tenantId and d.usuario_id=@userId and d.notificacao_id=@id":"update sigov.notificacao_destinatario d set lida=true,lida_at=coalesce(lida_at,now()) from sigov.notificacao_interna n where n.id=d.notificacao_id and n.tenant_id=@tenantId and d.usuario_id=@userId and (@id is null or d.notificacao_id=@id)",new{tenantId,userId,id},cancellationToken:ct));}
    public async Task<string> GetPreferencesAsync(long tenantId,long userId,CancellationToken ct){using var cn=context.CreateConnection();return await cn.ExecuteScalarAsync<string?>(new CommandDefinition("select preferencias::text from sigov.notificacao_preferencia where tenant_id=@tenantId and usuario_id=@userId",new{tenantId,userId},cancellationToken:ct))??"{}";}
    public async Task SetPreferencesAsync(long tenantId,long userId,string preferences,CancellationToken ct){using var cn=context.CreateConnection();await cn.ExecuteAsync(new CommandDefinition("insert into sigov.notificacao_preferencia(tenant_id,usuario_id,preferencias) values(@tenantId,@userId,cast(@preferences as jsonb)) on conflict(tenant_id,usuario_id) do update set preferencias=excluded.preferencias,updated_at=now()",new{tenantId,userId,preferences},cancellationToken:ct));}
    public async Task<Page<IntegrationEventItem>> IntegrationsAsync(long tenantId,OperationFilter f,CancellationToken ct){using var cn=context.CreateConnection();var page=Math.Max(1,f.Page);var take=Math.Clamp(f.PageSize,1,100);var skip=(page-1)*take;var p=new{tenantId,Module=Upper(f.Module),Status=Upper(f.Status),skip,take};const string w="tenant_id=@tenantId and is_deleted=false and (@Module is null or origem_modulo=@Module or destino_modulo=@Module) and (@Status is null or status=@Status)";var total=await cn.ExecuteScalarAsync<long>(new CommandDefinition($"select count(*) from sigov.integracao_interna_evento where {w}",p,cancellationToken:ct));var rows=await cn.QueryAsync<IntegrationEventItem>(new CommandDefinition($"select id,origem_modulo as SourceModule,destino_modulo as TargetModule,tipo_evento as EventType,status as Status,referencia_tipo as ReferenceType,referencia_id as ReferenceId,correlation_id::text as CorrelationId,erro as Error,created_at as CreatedAt,processed_at as ProcessedAt from sigov.integracao_interna_evento where {w} order by created_at desc,id desc offset @skip limit @take",p,cancellationToken:ct));return new(rows.AsList(),page,take,total);}
    public async Task ChangeIntegrationAsync(long tenantId,long id,string status,long? userId,string correlationId,CancellationToken ct){using var cn=context.CreateConnection();await cn.ExecuteAsync(new CommandDefinition("update sigov.integracao_interna_evento set status=@status,erro=null,processed_at=case when @status='CANCELADO' then now() else null end,auditoria=auditoria||jsonb_build_object('acao',@status,'usuario_id',@userId,'correlation_id',@correlationId,'em',now()) where id=@id and tenant_id=@tenantId and is_deleted=false",new{tenantId,id,status,userId,correlationId},cancellationToken:ct));}
    public async Task<IReadOnlyList<DataQualitySummary>> QualitySummaryAsync(long tenantId,CancellationToken ct){using var cn=context.CreateConnection();var r=await cn.QueryAsync<DataQualitySummary>(new CommandDefinition("select modulo as Module,count(*) filter(where status='ABERTA')::int as Open,count(*) filter(where status='ABERTA' and severidade='CRITICA')::int as Critical,greatest(0,100-count(*) filter(where status='ABERTA')*2)::numeric as Score from sigov.qualidade_dados_inconsistencia where tenant_id=@tenantId and is_deleted=false group by modulo order by modulo",new{tenantId},cancellationToken:ct));return r.AsList();}
    public async Task<Page<DataQualityItem>> QualityAsync(long tenantId,OperationFilter f,CancellationToken ct){var p=await PendenciesAsync(tenantId,f,ct);return new(p.Items.Select(x=>new DataQualityItem(x.Id,x.Module,x.Type,x.Severity,x.Title,x.Status,x.ReferenceType,x.ReferenceId,x.CreatedAt)).ToList(),p.PageNumber,p.PageSize,p.Total);}
    public async Task ReprocessQualityAsync(long tenantId,long? userId,string correlationId,CancellationToken ct){using var cn=context.CreateConnection();await cn.ExecuteAsync(new CommandDefinition("insert into sigov.integracao_interna_evento(tenant_id,origem_modulo,destino_modulo,tipo_evento,status,payload,correlation_id,created_by) values(@tenantId,'QUALIDADE','QUALIDADE','REPROCESSAR_QUALIDADE','PENDENTE',jsonb_build_object('escopo','TODOS'),cast(@correlationId as uuid),@userId)",new{tenantId,userId,correlationId},cancellationToken:ct));}
    public async Task<AssistantExecution> SaveAssistantAsync(long tenantId,long userId,AssistantCommand command,string correlationId,CancellationToken ct){using var cn=context.CreateConnection();return await cn.QuerySingleAsync<AssistantExecution>(new CommandDefinition("insert into sigov.assistente_operacional_execucao(tenant_id,usuario_id,assistente,etapa,status,dados,auditoria) values(@tenantId,@userId,@Assistant,@Step,case when @Complete then 'CONCLUIDO' else 'EM_ANDAMENTO' end,cast(@Payload as jsonb),jsonb_build_object('correlation_id',@correlationId,'usuario_id',@userId)) returning id,assistente as Assistant,etapa as Step,status as Status,updated_at as UpdatedAt",new{tenantId,userId,command.Assistant,command.Step,command.Payload,command.Complete,correlationId},cancellationToken:ct));}
    private static string? Upper(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim().ToUpperInvariant();
}
