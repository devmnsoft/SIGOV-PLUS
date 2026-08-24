using Dapper;
using Sigov.Application.Health;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Helpers;
using Sigov.Web.Models.Operational;

namespace Sigov.Web.Services.Operational;

public interface IOperationalModuleDataService
{
    Task<OperationalModuleViewModel> BuildAsync(string module, string screen, string? q, CancellationToken cancellationToken);
}

public abstract class OperationalModuleDataServiceBase : IOperationalModuleDataService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSchemaInspector _schemaInspector;
    private readonly IAuditTrailService _auditTrail;
    private readonly OperationalDemoService _fallback;
    private readonly ILogger _logger;
    private readonly IDatabaseObjectInspector? _objectInspector;

    protected OperationalModuleDataServiceBase(NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schemaInspector, IAuditTrailService auditTrail, OperationalDemoService fallback, ILogger logger, IDatabaseObjectInspector? objectInspector = null)
    {
        _connectionFactory = connectionFactory;
        _schemaInspector = schemaInspector;
        _auditTrail = auditTrail;
        _fallback = fallback;
        _logger = logger;
        _objectInspector = objectInspector;
    }

    protected abstract string ModuleKey { get; }
    protected abstract IReadOnlyList<string> CandidateTables { get; }
    protected virtual string EntityName => ModuleKey.ToLowerInvariant();
    protected virtual IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredColumns => new Dictionary<string, IReadOnlyList<string>>();

    public async Task<OperationalModuleViewModel> BuildAsync(string module, string screen, string? q, CancellationToken cancellationToken)
    {
        try
        {
            if (_objectInspector is not null)
            {
                foreach (var requirement in RequiredColumns)
                {
                    if (!await _objectInspector.TableExistsAsync("sigov", requirement.Key, cancellationToken).ConfigureAwait(false)
                        || !(await Task.WhenAll(requirement.Value.Select(column => _objectInspector.ColumnExistsAsync("sigov", requirement.Key, column, cancellationToken))).ConfigureAwait(false)).All(exists => exists))
                    {
                        var pending = await _fallback.BuildFallbackAsync(ModuleKey, screen, q, cancellationToken).ConfigureAwait(false);
                        pending.Status = "Estrutura pendente";
                        pending.Description = $"Estrutura pendente em sigov.{requirement.Key}; indicadores retornam zero até a migration do Bloco 7 ser concluída.";
                        pending.Kpis = new[] { new ModuleKpi("Estrutura pendente", "0", "Tabela ou coluna ainda não disponível", "warning") };
                        pending.PageStatus = new OperationalPageStatusViewModel { Modulo = pending.Title, Status = "Estrutura pendente", UsaDadosReais = false, UsaFallback = true, Mensagem = pending.Description };
                        pending.Records = Array.Empty<DemoRecord>();
                        return pending;
                    }
                }
            }

            var detected = new List<string>();
            foreach (var table in CandidateTables)
            {
                if (await _schemaInspector.TableExistsAsync("sigov", table, cancellationToken).ConfigureAwait(false)) detected.Add(table);
            }

            if (detected.Count == 0)
            {
                return await _fallback.BuildFallbackAsync(ModuleKey, screen, q, cancellationToken).ConfigureAwait(false);
            }

            var primary = detected[0];
            var columns = await _schemaInspector.GetColumnsAsync("sigov", primary, cancellationToken).ConfigureAwait(false);
            var records = await QueryRecordsAsync(primary, columns, q, cancellationToken).ConfigureAwait(false);
            var baseModel = await _fallback.BuildFallbackAsync(ModuleKey, screen, q, cancellationToken).ConfigureAwait(false);
            await AuditSafeAsync($"{EntityName}.consultar", primary, cancellationToken).ConfigureAwait(false);

            baseModel.Status = "Parcial";
            baseModel.Description = $"Dados reais consultados em sigov.{primary}; campos pessoais são mascarados e fontes ausentes permanecem em fallback honesto.";
            baseModel.PageStatus = new OperationalPageStatusViewModel { Modulo = baseModel.Title, Status = "Parcial", UsaDadosReais = true, UsaFallback = detected.Count < CandidateTables.Count, Mensagem = $"Schema operacional detectado: {string.Join(", ", detected.Select(t => "sigov." + t))}." };
            baseModel.SchemaTables = detected;
            baseModel.Records = records.Count == 0 ? baseModel.Records : records;
            baseModel.Kpis = BuildRealKpis(baseModel.Kpis, records.Count, detected.Count);
            return baseModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao montar módulo operacional real {Module}", ModuleKey);
            return await _fallback.BuildFallbackAsync(ModuleKey, screen, q, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<DemoRecord>> QueryRecordsAsync(string table, IReadOnlySet<string> columns, string? q, CancellationToken cancellationToken)
    {
        if (columns.Count == 0) return Array.Empty<DemoRecord>();
        static string Pick(IReadOnlySet<string> cols, params string[] names) => names.FirstOrDefault(cols.Contains) ?? "";
        var id = Pick(columns, "id", $"{table}_id", "codigo");
        var code = Pick(columns, "numero", "codigo", "protocolo", "inscricao", "titulo");
        var name = Pick(columns, "nome", "interessado", "assunto", "titulo", "descricao", "fornecedor");
        var status = Pick(columns, "status", "situacao");
        var owner = Pick(columns, "responsavel", "setor_atual", "setor", "usuario", "fiscal");
        var updated = Pick(columns, "updated_at", "criado_em", "created_at", "data", "data_abertura");
        var doc = Pick(columns, "documento", "cpf", "cnpj", "cpf_cnpj");
        var select = new[] { id, code, name, status, owner, updated, doc }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Select(c => $"\"{c}\"");
        var filter = !string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(name) ? $" where cast(\"{name}\" as text) ilike @Q" : "";
        var sql = $"select {string.Join(",", select)} from sigov.\"{table}\"{filter} order by 1 desc limit 25";
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync(new CommandDefinition(sql, new { Q = $"%{q}%" }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var result = new List<DemoRecord>();
        long n = 1;
        foreach (var item in rows)
        {
            var row = (IDictionary<string, object>)item;
            string Get(string col, string fallback) => string.IsNullOrWhiteSpace(col) || !row.TryGetValue(col, out var v) || v is null ? fallback : Convert.ToString(v) ?? fallback;
            var rawId = Get(id, n.ToString());
            _ = long.TryParse(rawId, out var parsedId);
            result.Add(new DemoRecord(parsedId == 0 ? n : parsedId, Get(code, $"{ModuleKey[..Math.Min(3, ModuleKey.Length)].ToUpperInvariant()}-{n:000}"), Get(name, "Registro operacional"), Get(status, "Ativo"), Get(owner, "Não informado"), Get(updated, "Atualização real"), MaskDocument(Get(doc, ""))));
            n++;
        }
        return result;
    }

    private static IReadOnlyList<ModuleKpi> BuildRealKpis(IReadOnlyList<ModuleKpi> fallback, int recordCount, int tableCount) => new[] { new ModuleKpi("Registros reais", recordCount.ToString("N0", new System.Globalization.CultureInfo("pt-BR")), "Consulta Dapper schema-safe", "success"), new ModuleKpi("Tabelas detectadas", tableCount.ToString(), "information_schema", "primary") }.Concat(fallback.Take(2)).ToArray();
    private static string MaskDocument(string value) => string.IsNullOrWhiteSpace(value) ? "" : LgpdMaskingHelper.MaskDocument(value);
    private async Task AuditSafeAsync(string action, string entity, CancellationToken ct) { try { await _auditTrail.RegistrarAsync(null, null, action, entity, null, null, null, null, null, Guid.NewGuid().ToString(), ct).ConfigureAwait(false); } catch (Exception ex) { _logger.LogWarning(ex, "Auditoria operacional em fallback"); } }
}

public sealed class ProtocoloOperationalService : OperationalModuleDataServiceBase { public ProtocoloOperationalService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<ProtocoloOperationalService> l) : base(c, s, a, f, l) { } protected override string ModuleKey => "Protocolo"; protected override IReadOnlyList<string> CandidateTables => new[] { "protocolo", "processo", "tramite", "protocolo_movimento", "protocolo_anexo", "arquivo" }; }
public sealed class GedOperationalService : OperationalModuleDataServiceBase { public GedOperationalService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<GedOperationalService> l) : base(c, s, a, f, l) { } protected override string ModuleKey => "Ged"; protected override IReadOnlyList<string> CandidateTables => new[] { "documento", "ged_documento", "ged_pasta", "pasta", "documento_versao", "arquivo", "ocr_fila" }; }
public sealed class TributarioOperationalService : OperationalModuleDataServiceBase { public TributarioOperationalService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<TributarioOperationalService> l) : base(c, s, a, f, l) { } protected override string ModuleKey => "Tributario"; protected override IReadOnlyList<string> CandidateTables => new[] { "contribuinte", "imovel", "debito", "guia", "divida_ativa" }; }
public sealed class ContratosOperationalService : OperationalModuleDataServiceBase { public ContratosOperationalService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<ContratosOperationalService> l) : base(c, s, a, f, l) { } protected override string ModuleKey => "Contratos"; protected override IReadOnlyList<string> CandidateTables => new[] { "contrato", "contrato_aditivo", "contrato_fiscal", "contrato_documento" }; }
public sealed class JuridicoOperationalService : OperationalModuleDataServiceBase { public JuridicoOperationalService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<JuridicoOperationalService> l) : base(c, s, a, f, l) { } protected override string ModuleKey => "Juridico"; protected override IReadOnlyList<string> CandidateTables => new[] { "processo_juridico", "parecer_juridico", "prazo_juridico", "audiencia_juridica" }; }
public sealed class FinanceiroOperationalService : OperationalModuleDataServiceBase { public FinanceiroOperationalService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<FinanceiroOperationalService> l) : base(c, s, a, f, l) { } protected override string ModuleKey => "Financeiro"; protected override IReadOnlyList<string> CandidateTables => new[] { "financeiro_dotacao", "financeiro_empenho", "financeiro_liquidacao", "financeiro_ordem_pagamento", "financeiro_receita_arrecadada", "financeiro_resto_pagar", "financeiro_suprimento" }; }
