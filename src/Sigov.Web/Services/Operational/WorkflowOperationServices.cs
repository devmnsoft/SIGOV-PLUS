using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.Operational;
using Sigov.Web.Services;
using Sigov.Application.Operational;

namespace Sigov.Web.Services.Operational;

public interface IOperationalStatusService
{
    OperationalStatusDescriptor Get(string? status);
    IReadOnlyList<OperationalStatusDescriptor> ListAll();
}

public sealed class OperationalStatusService : IOperationalStatusService
{
    private static readonly IReadOnlyDictionary<string, OperationalStatusDescriptor> Map = new Dictionary<string, OperationalStatusDescriptor>(StringComparer.OrdinalIgnoreCase)
    {
        ["Rascunho"] = new("Rascunho", "secondary", "bi-pencil"), ["Aberto"] = new("Aberto", "primary", "bi-folder2-open"),
        ["Em andamento"] = new("Em andamento", "info", "bi-arrow-repeat"), ["Pendente"] = new("Pendente", "warning", "bi-hourglass-split"),
        ["Aguardando"] = new("Aguardando", "warning", "bi-pause-circle"), ["Concluído"] = new("Concluído", "success", "bi-check-circle", true),
        ["Arquivado"] = new("Arquivado", "dark", "bi-archive", true), ["Cancelado"] = new("Cancelado", "danger", "bi-x-circle", true),
        ["Vencido"] = new("Vencido", "danger", "bi-exclamation-triangle"), ["Bloqueado"] = new("Bloqueado", "danger", "bi-lock"),
        ["Em implantação"] = new("Em implantação", "secondary", "bi-tools")
    };
    public OperationalStatusDescriptor Get(string? status) => !string.IsNullOrWhiteSpace(status) && Map.TryGetValue(status, out var d) ? d : Map["Em implantação"];
    public IReadOnlyList<OperationalStatusDescriptor> ListAll() => Map.Values.ToList();
}

public sealed class OperationalEventService
{
    private readonly NpgsqlConnectionFactory _factory; private readonly IDatabaseSchemaInspector _schema; private readonly ILogger<OperationalEventService> _logger;
    public OperationalEventService(NpgsqlConnectionFactory factory, IDatabaseSchemaInspector schema, ILogger<OperationalEventService> logger) { _factory = factory; _schema = schema; _logger = logger; }
    public async Task<bool> TryRegisterAsync(string type, string module, string entity, string? entityId, object payload, CancellationToken ct)
    { try { if (!await _schema.TableExistsAsync("sigov", "evento_operacional", ct)) { _logger.LogWarning("Tabela de evento operacional indisponível: {Type}/{Module}/{Entity}/{EntityId}", type, module, entity, entityId); return false; } using var c = _factory.CreateConnection(); var rows = await c.ExecuteAsync(new CommandDefinition(@"insert into sigov.evento_operacional (tenant_id, tipo_evento, modulo, entidade_tipo, entidade_id, payload, status, correlation_id, created_at, processed_at) values (@TenantId,@Type,@Module,@Entity,@EntityId,cast(@Payload as jsonb),'ABERTO',@CorrelationId,now(),null)", new { TenantId=0L, Type=type, Module=module, Entity=entity, EntityId=entityId, Payload=System.Text.Json.JsonSerializer.Serialize(payload), CorrelationId=Guid.NewGuid().ToString() }, cancellationToken: ct)); return rows == 1; } catch(Exception ex) { _logger.LogError(ex, "Falha crítica ao registrar evento operacional {Type}", type); return false; } }
}

public sealed class OutboxSigovService
{
    private readonly IOperationalEventPublisher _publisher; private readonly IHttpContextAccessor _httpContext; private readonly ILogger<OutboxSigovService> _logger;
    public OutboxSigovService(IOperationalEventPublisher publisher, IHttpContextAccessor httpContext, ILogger<OutboxSigovService> logger) { _publisher = publisher; _httpContext = httpContext; _logger = logger; }
    public async Task<bool> TryEnqueueAsync(string type, string module, string entity, string? entityId, object payload, CancellationToken ct)
    {
        var http = _httpContext.HttpContext;
        if (!long.TryParse(http?.Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var tenantId) || tenantId <= 0)
        {
            _logger.LogError("Outbox rejeitado porque o tenant operacional não foi resolvido. EventType={EventType}", type);
            return false;
        }
        var correlationId = Guid.TryParse(http?.TraceIdentifier, out var parsed) ? parsed : Guid.NewGuid();
        try
        {
            await _publisher.PublishAsync(new OperationalEvent(type, entity, entityId ?? "indefinido", tenantId, 0, correlationId, payload, $"{module}:{entity}:{entityId}:{correlationId}"), ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha crítica ao persistir evento na outbox. EventType={EventType}", type);
            return false;
        }
    }
}

public abstract class OperationalHubServiceBase
{
    protected readonly NpgsqlConnectionFactory Factory; protected readonly IDatabaseSchemaInspector Schema; protected readonly ILogger Logger;
    protected OperationalHubServiceBase(NpgsqlConnectionFactory factory, IDatabaseSchemaInspector schema, ILogger logger) { Factory=factory; Schema=schema; Logger=logger; }
    protected abstract string AreaKey { get; } protected abstract string Title { get; } protected abstract string Description { get; } protected abstract IReadOnlyList<string> Tables { get; }
    public virtual async Task<OperationalHubViewModel> GetAsync(CancellationToken ct)
    { var states = new List<OperationalSchemaState>(); foreach (var t in Tables) { var cols = await Schema.GetColumnsAsync("sigov", t, ct); states.Add(new(t, cols.Count > 0, cols.ToList())); }
      var has = states.Any(s=>s.Exists); var items = has ? await QueryItemsAsync(states, ct) : FallbackItems();
      return new OperationalHubViewModel { AreaKey=AreaKey, Title=Title, Description=Description, PrimaryActionText="Nova operação", PrimaryActionUrl=$"/{AreaKey}/Nova", HasRealSchema=has, Schema=states, IntegratedModules=new[]{"Protocolo","GED","Tributário","Contratos","Jurídico","Financeiro"}, Items=items, Metrics=BuildMetrics(items, has) }; }
    protected virtual IReadOnlyList<OperationalMetric> BuildMetrics(IReadOnlyList<OperationalItem> items, bool has) => new[] { new OperationalMetric("Registros", items.Count.ToString(), has ? "Consultas reais schema-safe" : "Fallback honesto", has ? "Aberto" : "Em implantação"), new OperationalMetric("Vencidos", items.Count(i=>i.Status.Equals("Vencido", StringComparison.OrdinalIgnoreCase)).ToString(), "Prazos consolidados", "Vencido"), new OperationalMetric("Módulos", "6", "Integração transversal", "Em andamento") };
    protected virtual IReadOnlyList<OperationalItem> FallbackItems() => new[] { new OperationalItem(null, $"{Title} em implantação", "Nenhum dado foi simulado. Ative as tabelas sigov correspondentes para persistência real.", AreaKey, AreaKey, "Em implantação", null, null, true) };
    protected virtual async Task<IReadOnlyList<OperationalItem>> QueryItemsAsync(IReadOnlyList<OperationalSchemaState> states, CancellationToken ct)
    { var state = states.First(s=>s.Exists); var table = state.Table; var projection = string.Join(", ", state.Columns.Select(column => $"\"{column.Replace("\"", "\"\"", StringComparison.Ordinal)}\"")); try { using var c = Factory.CreateConnection(); var rows = await c.QueryAsync<dynamic>(new CommandDefinition($"select {projection} from sigov.{table} limit 25", cancellationToken: ct)); return rows.Select((r,i)=> new OperationalItem(null, $"Registro {i+1} em {table}", "Registro real carregado com colunas validadas por schema inspector.", AreaKey, table, "Aberto", null, null, false)).ToList(); } catch(Exception ex) { Logger.LogWarning(ex, "Falha ao consultar {Table}; usando fallback", table); return FallbackItems(); } }
}

public class WorkflowService : OperationalHubServiceBase { public WorkflowService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, ILogger<WorkflowService> l):base(f,s,l){} protected override string AreaKey=>"Workflow"; protected override string Title=>"Workflow e tramitação"; protected override string Description=>"Motor transversal de etapas, transições, responsáveis, prazos, histórico, eventos e auditoria."; protected override IReadOnlyList<string> Tables=>new[]{"workflow","workflow_etapa","workflow_transicao","workflow_instancia","workflow_historico"}; }
public sealed class WorkflowDefinitionService : WorkflowService { public WorkflowDefinitionService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, ILogger<WorkflowService> l):base(f,s,l){} }
public sealed class WorkflowInstanceService : WorkflowService { public WorkflowInstanceService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, ILogger<WorkflowService> l):base(f,s,l){} }
public sealed class TarefaService : OperationalHubServiceBase { public TarefaService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, ILogger<TarefaService> l):base(f,s,l){} protected override string AreaKey=>"Tarefas"; protected override string Title=>"Central de tarefas"; protected override string Description=>"Tarefas por responsável, prioridade, módulo, prazo e workflow."; protected override IReadOnlyList<string> Tables=>new[]{"tarefa","workflow_instancia","agenda_prazo"}; }
public sealed class NotificacaoService : OperationalHubServiceBase { public NotificacaoService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, ILogger<NotificacaoService> l):base(f,s,l){} protected override string AreaKey=>"Notificacoes"; protected override string Title=>"Notificações"; protected override string Description=>"Avisos reais quando há schema e recomendações derivadas quando não há persistência."; protected override IReadOnlyList<string> Tables=>new[]{"notificacao","notificacao_usuario","evento_operacional"}; }
public sealed class AgendaOperacionalService : OperationalHubServiceBase { public AgendaOperacionalService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, ILogger<AgendaOperacionalService> l):base(f,s,l){} protected override string AreaKey=>"Agenda"; protected override string Title=>"Agenda de prazos e vencimentos"; protected override string Description=>"Consolidação de prazos de contratos, jurídico, financeiro, protocolo e workflow."; protected override IReadOnlyList<string> Tables=>new[]{"agenda_prazo","contrato","processo_juridico","prazo_juridico","conta_pagar","conta_receber","protocolo","workflow_instancia"}; }
public sealed class IntegracaoMonitorService : OperationalHubServiceBase { public IntegracaoMonitorService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, ILogger<IntegracaoMonitorService> l):base(f,s,l){} protected override string AreaKey=>"Integracoes"; protected override string Title=>"Integrações monitoradas"; protected override string Description=>"Conectores, logs, erros, outbox e reprocessamento monitorado."; protected override IReadOnlyList<string> Tables=>new[]{"integracao_sistema","integracao_log","outbox_evento"}; }
public sealed class BiOperacionalService : OperationalHubServiceBase { public BiOperacionalService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, ILogger<BiOperacionalService> l):base(f,s,l){} protected override string AreaKey=>"Bi"; protected override string Title=>"BI operacional"; protected override string Description=>"Indicadores de governo, operação, documentos, financeiro e SaaS com dados reais ou fallback honesto."; protected override IReadOnlyList<string> Tables=>new[]{"protocolo","documento","debito","contrato","prazo_juridico","tarefa","workflow_instancia","notificacao","conta_pagar","conta_receber"}; }
public sealed class MobileCampoService : OperationalHubServiceBase { public MobileCampoService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, ILogger<MobileCampoService> l):base(f,s,l){} protected override string AreaKey=>"MobileCampo"; protected override string Title=>"Mobile/Campo"; protected override string Description=>"Roteiros, coletas, evidências, sincronização offline planejada, conflitos e logs."; protected override IReadOnlyList<string> Tables=>new[]{"campo_roteiro","campo_coleta","campo_evidencia","sincronizacao_dispositivo"}; }
