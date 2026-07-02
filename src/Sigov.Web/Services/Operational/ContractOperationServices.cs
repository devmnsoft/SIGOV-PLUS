using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.Operational;
using Sigov.Web.Services;

namespace Sigov.Web.Services.Operational;

public abstract class ContractOperationServiceBase : OperationalHubServiceBase
{
    private readonly IAuditTrailService _audit; private readonly OutboxSigovService _outbox;
    protected ContractOperationServiceBase(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger logger) : base(f, s, logger) { _audit=a; _outbox=o; }
    protected virtual string Entity => AreaKey;
    public async Task<bool> RegistrarAcaoAsync(string action, string? note, CancellationToken ct)
    {
        try
        {
            await _audit.RegistrarAsync(null, null, $"{AreaKey}.{action}", Entity, null, null, new { note }, null, null, Guid.NewGuid().ToString(), ct).ConfigureAwait(false);
            await _outbox.TryEnqueueAsync($"{AreaKey}.{action}", AreaKey, Entity, null, new { note }, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) { Logger.LogWarning(ex, "Ação contratual em fallback {AreaKey}.{Action}", AreaKey, action); return false; }
    }
    protected override IReadOnlyList<OperationalMetric> BuildMetrics(IReadOnlyList<OperationalItem> items, bool has) => new[] {
        new OperationalMetric("Registros", items.Count.ToString(), has ? "Persistência real detectada" : "Portal em implantação", has ? "Aberto" : "Em implantação"),
        new OperationalMetric("Pendências", items.Count(i=>!i.Status.Equals("Concluído", StringComparison.OrdinalIgnoreCase)).ToString(), "Operação contratual", "Pendente"),
        new OperationalMetric("SLA/aceite", items.Count(i=>i.Status.Contains("Venc", StringComparison.OrdinalIgnoreCase) || i.Status.Contains("Pendente", StringComparison.OrdinalIgnoreCase)).ToString(), "Alertas auditáveis", "Aguardando") };
    protected override async Task<IReadOnlyList<OperationalItem>> QueryItemsAsync(IReadOnlyList<OperationalSchemaState> states, CancellationToken ct)
    {
        var table = states.First(s=>s.Exists).Table;
        try
        {
            using var c = Factory.CreateConnection();
            var cols = states.First(s=>s.Table==table).Columns.ToHashSet(StringComparer.OrdinalIgnoreCase);
            static string Pick(ISet<string> cols, params string[] names) => names.FirstOrDefault(cols.Contains) ?? "";
            var title = Pick(cols,"titulo","nome","assunto","numero","modulo","origem","descricao"); var status = Pick(cols,"status","situacao"); var resp = Pick(cols,"responsavel","responsavel_interno","solicitante","avaliador","instrutor"); var due = Pick(cols,"data_prevista","prazo_sla","data","data_abertura","created_at");
            var select = new[]{title,status,resp,due}.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct().Select(x=>$"\"{x}\"");
            if (!select.Any()) return FallbackItems();
            var rows = await c.QueryAsync(new CommandDefinition($"select {string.Join(',',select)} from sigov.\"{table}\" limit 25", cancellationToken:ct)).ConfigureAwait(false);
            return rows.Select((r,i)=>{ var d=(IDictionary<string,object>)r; string G(string k,string fb)=>string.IsNullOrWhiteSpace(k)||!d.TryGetValue(k,out var v)||v is null?fb:Convert.ToString(v)??fb; return new OperationalItem(null, G(title,$"Registro {i+1}"), "Registro real carregado com Dapper, schema-safe e dados pessoais minimizados.", AreaKey, table, G(status,"Aberto"), MaskName(G(resp,"Não informado")), null, false);}).ToList();
        }
        catch(Exception ex){ Logger.LogWarning(ex,"Falha consulta contratual {Table}", table); return FallbackItems(); }
    }
    private static string MaskName(string v) => string.IsNullOrWhiteSpace(v) || v == "Não informado" ? v : v[0] + "***";
}
public class ImplantacaoService : ContractOperationServiceBase { public ImplantacaoService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<ImplantacaoService> l):base(f,s,a,o,l){} protected override string AreaKey=>"Implantacao"; protected override string Title=>"Implantação guiada"; protected override string Description=>"Projetos, etapas, evidências e termos de aceite com auditoria e fallback honesto."; protected override IReadOnlyList<string> Tables=>new[]{"implantacao","implantacao_etapa","implantacao_evidencia","aceite_formal"}; }
public class MigracaoService : ContractOperationServiceBase { public MigracaoService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<MigracaoService> l):base(f,s,a,o,l){} protected override string AreaKey=>"Migracao"; protected override string Title=>"Migração de dados"; protected override string Description=>"Lotes, validações, logs e relatórios de erro sem sobrescrever dados sem confirmação."; protected override IReadOnlyList<string> Tables=>new[]{"migracao_lote","migracao_log","migracao_validacao"}; }
public class TreinamentoService : ContractOperationServiceBase { public TreinamentoService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<TreinamentoService> l):base(f,s,a,o,l){} protected override string AreaKey=>"Treinamentos"; protected override string Title=>"Treinamentos e capacitação"; protected override string Description=>"Treinamentos, turmas, participantes e certificados visuais somente quando persistidos."; protected override IReadOnlyList<string> Tables=>new[]{"treinamento","treinamento_turma","treinamento_participante","treinamento_certificado","treinamento_avaliacao"}; }
public sealed class SuporteService : ContractOperationServiceBase { public SuporteService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<SuporteService> l):base(f,s,a,o,l){} protected override string AreaKey=>"Suporte"; protected override string Title=>"Portal de suporte"; protected override string Description=>"Chamados, interações, anexos, satisfação e painel SLA."; protected override IReadOnlyList<string> Tables=>new[]{"suporte_chamado","suporte_interacao","suporte_anexo","suporte_satisfacao","sla_regra","sla_evento"}; }
public class SlaService : ContractOperationServiceBase { public SlaService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<SlaService> l):base(f,s,a,o,l){} protected override string AreaKey=>"Sla"; protected override string Title=>"SLA e penalidades"; protected override string Description=>"Regras, monitoramento, eventos e relatórios de cumprimento contratual."; protected override IReadOnlyList<string> Tables=>new[]{"sla_regra","sla_evento","suporte_chamado"}; }
public sealed class SlaMonitorService : SlaService { public SlaMonitorService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<SlaService> l):base(f,s,a,o,l){} }
public class PocService : ContractOperationServiceBase { public PocService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<PocService> l):base(f,s,a,o,l){} protected override string AreaKey=>"Poc"; protected override string Title=>"POC operacional"; protected override string Description=>"Roteiros, requisitos binários Atende/Não Atende, execuções, evidências e relatório."; protected override IReadOnlyList<string> Tables=>new[]{"poc_roteiro","poc_requisito","poc_execucao","poc_evidencia"}; }
public sealed class AceiteFormalService : ContractOperationServiceBase { public AceiteFormalService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<AceiteFormalService> l):base(f,s,a,o,l){} protected override string AreaKey=>"Aceites"; protected override string Title=>"Aceite formal"; protected override string Description=>"Aceites por módulo, etapa, POC, treinamento, migração, suporte e implantação."; protected override IReadOnlyList<string> Tables=>new[]{"aceite_formal","implantacao_etapa","poc_execucao"}; }
public sealed class ImplantacaoEtapaService : ImplantacaoService { public ImplantacaoEtapaService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<ImplantacaoService> l):base(f,s,a,o,l){} }
public sealed class ImplantacaoEvidenciaService : ImplantacaoService { public ImplantacaoEvidenciaService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<ImplantacaoService> l):base(f,s,a,o,l){} }
public sealed class MigracaoValidacaoService : MigracaoService { public MigracaoValidacaoService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<MigracaoService> l):base(f,s,a,o,l){} }
public sealed class CertificadoTreinamentoService : TreinamentoService { public CertificadoTreinamentoService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<TreinamentoService> l):base(f,s,a,o,l){} }
public sealed class PocRoteiroService : PocService { public PocRoteiroService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<PocService> l):base(f,s,a,o,l){} }
public sealed class PocEvidenciaService : PocService { public PocEvidenciaService(NpgsqlConnectionFactory f, IDatabaseSchemaInspector s, IAuditTrailService a, OutboxSigovService o, ILogger<PocService> l):base(f,s,a,o,l){} }
