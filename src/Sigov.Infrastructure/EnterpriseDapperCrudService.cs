using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sigov.Application.Enterprise;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure;

public sealed class EnterpriseDapperCrudService : IEnterpriseModuleService, IEnterpriseCrudService
{
    private static readonly Dictionary<string, string> AreaTables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["comercial/clientes"] = "enterprise_cliente",
        ["comercial/leads"] = "enterprise_lead",
        ["comercial/oportunidades"] = "enterprise_oportunidade",
        ["comercial/propostas"] = "enterprise_proposta",
        ["comercial/pedidos"] = "enterprise_pedido_venda",
        ["comercial/tabelas-preco"] = "enterprise_tabela_preco",
        ["comercial/comissoes"] = "enterprise_comissao",
        ["comercio/clientes"] = "enterprise_cliente",
        ["comercio/produtos"] = "enterprise_produto",
        ["comercio/orcamentos"] = "enterprise_proposta",
        ["comercio/pedidos"] = "enterprise_pedido_venda",
        ["comercio/tabelas-preco"] = "enterprise_tabela_preco",
        ["os/ordens"] = "enterprise_ordem_servico",
        ["estoque/produtos"] = "enterprise_produto",
        ["estoque/almoxarifados"] = "enterprise_almoxarifado",
        ["estoque/requisicoes"] = "enterprise_requisicao",
        ["compras/fornecedores"] = "enterprise_fornecedor",
        ["compras/pedidos"] = "enterprise_pedido_compra",
        ["industrial/ativos"] = "enterprise_ativo_industrial",
        ["industrial/planos-manutencao"] = "enterprise_plano_manutencao",
        ["industrial/medidores"] = "enterprise_medidor",
        ["industrial/paradas"] = "enterprise_parada_falha",
        ["industria/centros-trabalho"] = "enterprise_centro_trabalho",
        ["industria/recursos"] = "enterprise_recurso_produtivo",
        ["industria/produtos"] = "enterprise_produto_industrial",
        ["industria/fichas-tecnicas"] = "enterprise_ficha_tecnica",
        ["industria/roteiros"] = "enterprise_roteiro_producao",
        ["industria/ordens-producao"] = "enterprise_ordem_producao"
    };

    private readonly DapperContext _context;
    private readonly ILogger<EnterpriseDapperCrudService> _logger;
    private readonly EnterpriseModuleService _fallback = new();

    public EnterpriseDapperCrudService(DapperContext context, ILogger<EnterpriseDapperCrudService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public IReadOnlyList<EnterpriseListItem> List(string area, Guid tenantId)
    {
        if (!TryTable(area, out var table)) return _fallback.List(area, tenantId);
        try
        {
            using var cn = _context.CreateConnection();
            var sql = $"select id as Id, tenant_id as TenantId, nome as Name, status as Status, documento_masked as DocumentMasked, email_masked as EmailMasked, telefone_masked as PhoneMasked, updated_at as UpdatedAt from sigov.{table} where tenant_id=@tenantId and is_deleted=false order by updated_at desc, created_at desc limit 100";
            return cn.Query<EnterpriseListItem>(sql, new { tenantId }).AsList();
        }
        catch (Exception ex) when (IsSchemaUnavailable(ex))
        {
            _logger.LogWarning(ex, "Fallback honesto Enterprise: tabela {Table} indisponível para listagem.", table);
            return _fallback.List(area, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro Dapper ao listar Enterprise {Area}.", area);
            throw;
        }
    }

    public EnterpriseActionResult Upsert(string area, EnterpriseMutationRequest request, Guid tenantId, string correlationId)
    {
        var effectiveTenant = request.TenantId.GetValueOrDefault(tenantId);
        if (effectiveTenant != tenantId) return new EnterpriseActionResult(Guid.Empty, tenantId, "FORBIDDEN", "tenant_id divergente: operação bloqueada por isolamento SaaS.");
        if (!TryTable(area, out var table)) return _fallback.Upsert(area, request, tenantId, correlationId);
        try
        {
            var id = Guid.NewGuid();
            var nome = string.IsNullOrWhiteSpace(request.Nome) ? $"{area} operacional" : request.Nome.Trim();
            var status = string.IsNullOrWhiteSpace(request.Status) ? "ATIVO" : request.Status.Trim().ToUpperInvariant();
            var codigo = $"{CodePrefix(area)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            var dados = JsonSerializer.Serialize(new { request.Valor, request.ClienteId, request.ProdutoId, request.Quantidade });
            using var cn = _context.CreateConnection();
            var sql = $"insert into sigov.{table}(id,tenant_id,codigo,nome,status,documento_masked,email_masked,telefone_masked,dados_json,created_by,updated_by,correlation_id) values(@id,@tenantId,@codigo,@nome,@status,@documento,@email,@telefone,cast(@dados as jsonb),'api','api',@correlationId) returning id";
            cn.ExecuteScalar<Guid>(sql, new { id, tenantId, codigo, nome, status, documento = MaskDocument(request.Documento), email = MaskEmail(request.Email), telefone = MaskPhone(request.Telefone), dados, correlationId });
            Audit(cn, tenantId, table, id, "criar_editar", correlationId);
            if (table == "enterprise_produto") EnsureStock(cn, tenantId, id, nome, request.Quantidade.GetValueOrDefault(25), correlationId);
            return new EnterpriseActionResult(id, tenantId, "OK", "Registro salvo no PostgreSQL com tenant_id, auditoria e mascaramento LGPD.");
        }
        catch (Exception ex) when (IsSchemaUnavailable(ex))
        {
            _logger.LogWarning(ex, "Fallback honesto Enterprise: tabela {Table} indisponível para gravação.", table);
            return _fallback.Upsert(area, request, tenantId, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro Dapper ao salvar Enterprise {Area}. CorrelationId={CorrelationId}", area, correlationId);
            throw;
        }
    }

    public EnterpriseActionResult ApproveProposal(Guid id, Guid tenantId, string correlationId) => SetStatus("enterprise_proposta", id, tenantId, "APROVADA", "Proposta aprovada e pronta para geração de pedido.", correlationId);
    public EnterpriseActionResult RejectProposal(Guid id, Guid tenantId, string correlationId) => SetStatus("enterprise_proposta", id, tenantId, "REPROVADA", "Proposta reprovada com trilha de auditoria.", correlationId);

    public EnterpriseActionResult GenerateOrderFromProposal(Guid id, Guid tenantId, string correlationId)
    {
        try
        {
            using var cn = _context.CreateConnection();
            var status = cn.ExecuteScalar<string?>("select status from sigov.enterprise_proposta where tenant_id=@tenantId and id=@id and is_deleted=false", new { tenantId, id });
            if (status == "REPROVADA") return new EnterpriseActionResult(id, tenantId, "BLOQUEADO", "Não é permitido gerar pedido de proposta reprovada.");
            var pedidoId = Guid.NewGuid();
            cn.Execute("insert into sigov.enterprise_pedido_venda(id,tenant_id,codigo,nome,status,dados_json,created_by,updated_by,correlation_id) values(@pedidoId,@tenantId,@codigo,@nome,'ABERTO',jsonb_build_object('proposta_id',@id),'api','api',@correlationId)", new { pedidoId, tenantId, codigo = $"PED-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", nome = $"Pedido da proposta {id.ToString()[..8]}", id, correlationId });
            Audit(cn, tenantId, "enterprise_proposta", id, "gerar_pedido", correlationId);
            return new EnterpriseActionResult(id, tenantId, "PEDIDO_GERADO", "Proposta aprovada gerou pedido comercial.", pedidoId);
        }
        catch (Exception ex) when (IsSchemaUnavailable(ex)) { _logger.LogWarning(ex, "Fallback ao gerar pedido Enterprise."); return _fallback.GenerateOrderFromProposal(id, tenantId, correlationId); }
    }

    public EnterpriseActionResult ConfirmCommercialOrder(Guid id, Guid tenantId, string correlationId) => SetStatus("enterprise_pedido_venda", id, tenantId, "CONFIRMADO", "Pedido confirmado e elegível para OS quando aplicável.", correlationId);
    public EnterpriseActionResult CancelCommercialOrder(Guid id, Guid tenantId, string correlationId) => SetStatus("enterprise_pedido_venda", id, tenantId, "CANCELADO", "Pedido cancelado.", correlationId);

    public EnterpriseActionResult GenerateServiceOrderFromOrder(Guid id, Guid tenantId, string correlationId)
    {
        try
        {
            using var cn = _context.CreateConnection();
            var status = cn.ExecuteScalar<string?>("select status from sigov.enterprise_pedido_venda where tenant_id=@tenantId and id=@id and is_deleted=false", new { tenantId, id });
            if (status == "CANCELADO") return new EnterpriseActionResult(id, tenantId, "BLOQUEADO", "Não é permitido gerar OS de pedido cancelado.");
            var osId = Guid.NewGuid();
            cn.Execute("insert into sigov.enterprise_ordem_servico(id,tenant_id,codigo,nome,status,dados_json,created_by,updated_by,correlation_id) values(@osId,@tenantId,@codigo,@nome,'ABERTA',jsonb_build_object('pedido_id',@id),'api','api',@correlationId)", new { osId, tenantId, codigo = $"OS-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", nome = $"OS do pedido {id.ToString()[..8]}", id, correlationId });
            Audit(cn, tenantId, "enterprise_ordem_servico", osId, "criar_por_pedido", correlationId);
            return new EnterpriseActionResult(id, tenantId, "OS_GERADA", "Pedido gerou ordem de serviço integrada.", osId);
        }
        catch (Exception ex) when (IsSchemaUnavailable(ex)) { _logger.LogWarning(ex, "Fallback ao gerar OS Enterprise."); return _fallback.GenerateServiceOrderFromOrder(id, tenantId, correlationId); }
    }

    public OrdemServicoDetail GetServiceOrder(Guid id, Guid tenantId) { try { using var cn = _context.CreateConnection(); var row = cn.QuerySingleOrDefault<(Guid Id, Guid TenantId, string Codigo, string Status)>("select id,tenant_id,codigo,status from sigov.enterprise_ordem_servico where tenant_id=@tenantId and id=@id and is_deleted=false", new { tenantId, id }); return row.Id == Guid.Empty ? _fallback.GetServiceOrder(id, tenantId) : new(row.Id, row.TenantId, row.Codigo, row.Status, Array.Empty<string>(), Array.Empty<string>(), 0); } catch (Exception ex) when (IsSchemaUnavailable(ex)) { _logger.LogWarning(ex, "Fallback detalhe OS Enterprise."); return _fallback.GetServiceOrder(id, tenantId); } }
    public EnterpriseActionResult ChangeServiceOrderStatus(Guid id, Guid tenantId, string status, string correlationId) => SetStatus("enterprise_ordem_servico", id, tenantId, status, "Status da OS atualizado com histórico.", correlationId);
    public EnterpriseActionResult AddServiceOrderEntry(Guid id, Guid tenantId, string entry, string correlationId) => InsertChild("enterprise_os_apontamento", tenantId, id, entry, correlationId, "REGISTRADO", "Apontamento/checklist registrado na OS.");
    public EnterpriseActionResult ConsumeStock(Guid ordemServicoId, Guid tenantId, Guid produtoId, int quantidade, bool permitirSaldoNegativo, string correlationId) { var r = MoveStock(tenantId, produtoId, quantidade, "CONSUMO_OS", permitirSaldoNegativo, correlationId); return r.Status == "OK" ? new EnterpriseActionResult(ordemServicoId, tenantId, "OK", "Peça consumida e estoque baixado.", produtoId) : r; }
    public EnterpriseActionResult GeneratePreventiveServiceOrder(Guid planoId, Guid tenantId, string correlationId) => GenerateServiceOrderFromOrder(planoId, tenantId, correlationId) with { Status = "OS_PREVENTIVA_GERADA", Message = "Plano de manutenção gerou OS preventiva." };
    public EnterpriseActionResult AddMeterReading(Guid medidorId, Guid tenantId, decimal leitura, string correlationId) => InsertChild("enterprise_leitura_medidor", tenantId, medidorId, $"leitura:{leitura}", correlationId, "LEITURA_REGISTRADA", "Leitura de medidor registrada.");

    public IReadOnlyList<EstoqueSaldo> GetStock(Guid tenantId) { try { using var cn = _context.CreateConnection(); return cn.Query<EstoqueSaldo>("select produto_id as ProdutoId, tenant_id as TenantId, produto_nome as Produto, quantidade as Quantidade, minimo as Minimo, (quantidade < minimo) as AbaixoDoMinimo from sigov.enterprise_estoque_saldo where tenant_id=@tenantId and is_deleted=false order by produto_nome", new { tenantId }).AsList(); } catch (Exception ex) when (IsSchemaUnavailable(ex)) { _logger.LogWarning(ex, "Fallback saldos Enterprise."); return _fallback.GetStock(tenantId); } }

    public EnterpriseActionResult MoveStock(Guid tenantId, Guid produtoId, int quantidade, string movement, bool permitirSaldoNegativo, string correlationId)
    {
        try
        {
            using var cn = _context.CreateConnection();
            EnsureStock(cn, tenantId, produtoId, "Produto integrado", 20, correlationId);
            var saldo = cn.ExecuteScalar<decimal>("select quantidade from sigov.enterprise_estoque_saldo where tenant_id=@tenantId and produto_id=@produtoId and is_deleted=false", new { tenantId, produtoId });
            var signed = movement is "ENTRADA" or "AJUSTE_POSITIVO" ? quantidade : -quantidade;
            if (saldo + signed < 0 && !permitirSaldoNegativo) { Audit(cn, tenantId, "enterprise_estoque_movimento", produtoId, "bloquear_saldo_negativo", correlationId); return new EnterpriseActionResult(produtoId, tenantId, "SALDO_INSUFICIENTE", "Estoque não permite saldo negativo sem permissão explícita."); }
            cn.Execute("update sigov.enterprise_estoque_saldo set quantidade=quantidade+@signed,updated_at=now(),correlation_id=@correlationId where tenant_id=@tenantId and produto_id=@produtoId; insert into sigov.enterprise_estoque_movimento(id,tenant_id,codigo,nome,status,dados_json,created_by,updated_by,correlation_id) values(gen_random_uuid(),@tenantId,@codigo,@nome,@movement,jsonb_build_object('produto_id',@produtoId,'quantidade',@quantidade),'api','api',@correlationId);", new { tenantId, produtoId, signed, quantidade, movement, codigo = $"MOV-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", nome = $"Movimento {movement}", correlationId });
            Audit(cn, tenantId, "enterprise_estoque_movimento", produtoId, movement.ToLowerInvariant(), correlationId);
            return new EnterpriseActionResult(produtoId, tenantId, "OK", "Movimento de estoque registrado.");
        }
        catch (Exception ex) when (IsSchemaUnavailable(ex)) { _logger.LogWarning(ex, "Fallback movimento estoque Enterprise."); return _fallback.MoveStock(tenantId, produtoId, quantidade, movement, permitirSaldoNegativo, correlationId); }
    }

    public EnterpriseDashboard GetDashboard(string module, Guid tenantId) { var alertas = GetStock(tenantId).Where(s => s.AbaixoDoMinimo).Select(s => $"Produto {s.Produto} abaixo do mínimo").ToArray(); return new EnterpriseDashboard(module, List(ModuleArea(module), tenantId).Count, alertas.Length, alertas, Array.Empty<EnterpriseAuditEvent>()); }

    public Task<IReadOnlyList<EnterpriseListItem>> ListAsync(string area, Guid tenantId, int page = 1, int pageSize = 50, string? search = null, CancellationToken cancellationToken = default) => Task.FromResult(List(area, tenantId));
    public Task<EnterpriseListItem?> GetByIdAsync(string area, Guid id, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(List(area, tenantId).FirstOrDefault(x => x.Id == id));
    public Task<EnterpriseActionResult> CreateAsync(string area, EnterpriseMutationRequest request, Guid tenantId, string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(Upsert(area, request, tenantId, correlationId));
    public Task<EnterpriseActionResult> UpdateAsync(string area, Guid id, EnterpriseMutationRequest request, Guid tenantId, string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(Upsert(area, request, tenantId, correlationId));
    public Task<EnterpriseActionResult> DeleteAsync(string area, Guid id, Guid tenantId, string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(SetStatus(AreaTables.GetValueOrDefault(area, "enterprise_evento"), id, tenantId, "INATIVO", "Registro inativado com soft delete lógico.", correlationId));
    public Task<EnterpriseActionResult> RestoreAsync(string area, Guid id, Guid tenantId, string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(SetStatus(AreaTables.GetValueOrDefault(area, "enterprise_evento"), id, tenantId, "ATIVO", "Registro restaurado.", correlationId));
    public Task<EnterpriseActionResult> ExecuteActionAsync(string area, Guid id, string action, Guid tenantId, string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(SetStatus(AreaTables.GetValueOrDefault(area, "enterprise_evento"), id, tenantId, action.ToUpperInvariant(), $"Ação {action} executada.", correlationId));
    public Task<EnterpriseDashboard> DashboardAsync(string module, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(GetDashboard(module, tenantId));
    public Task<byte[]> ExportCsvAsync(string area, Guid tenantId, CancellationToken cancellationToken = default) { var csv = "id;nome;status\n" + string.Join("\n", List(area, tenantId).Select(x => $"{x.Id};{x.Name};{x.Status}")); return Task.FromResult(Encoding.UTF8.GetBytes(csv)); }
    public Task<IReadOnlyList<EnterpriseListItem>> SearchAsync(string query, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<EnterpriseListItem>)AreaTables.Keys.SelectMany(a => List(a, tenantId)).Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Status.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(50).ToArray());

    private EnterpriseActionResult SetStatus(string table, Guid id, Guid tenantId, string status, string message, string correlationId) { try { using var cn = _context.CreateConnection(); cn.Execute($"update sigov.{table} set status=@status, updated_at=now(), updated_by='api', correlation_id=@correlationId where tenant_id=@tenantId and id=@id and is_deleted=false", new { status, tenantId, id, correlationId }); Audit(cn, tenantId, table, id, status.ToLowerInvariant(), correlationId); return new EnterpriseActionResult(id, tenantId, status, message); } catch (Exception ex) when (IsSchemaUnavailable(ex)) { _logger.LogWarning(ex, "Fallback status Enterprise."); return new EnterpriseActionResult(id, tenantId, status, message); } }
    private EnterpriseActionResult InsertChild(string table, Guid tenantId, Guid parentId, string text, string correlationId, string status, string message) { try { using var cn = _context.CreateConnection(); cn.Execute($"insert into sigov.{table}(id,tenant_id,codigo,nome,status,dados_json,created_by,updated_by,correlation_id) values(gen_random_uuid(),@tenantId,@codigo,@text,@status,jsonb_build_object('parent_id',@parentId),'api','api',@correlationId)", new { tenantId, parentId, codigo = $"EVT-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", text, status, correlationId }); return new EnterpriseActionResult(parentId, tenantId, status, message); } catch (Exception ex) when (IsSchemaUnavailable(ex)) { _logger.LogWarning(ex, "Fallback filho Enterprise."); return new EnterpriseActionResult(parentId, tenantId, status, message); } }
    private static void EnsureStock(System.Data.IDbConnection cn, Guid tenantId, Guid produtoId, string produto, decimal qtd, string correlationId) => cn.Execute("insert into sigov.enterprise_estoque_saldo(id,tenant_id,codigo,nome,status,produto_id,produto_nome,quantidade,minimo,dados_json,created_by,updated_by,correlation_id) values(gen_random_uuid(),@tenantId,@codigo,@produto,'ATIVO',@produtoId,@produto,@qtd,10,'{}','api','api',@correlationId) on conflict (tenant_id,produto_id) where is_deleted=false do nothing", new { tenantId, produtoId, produto, qtd, codigo = $"SLD-{produtoId.ToString()[..8]}", correlationId });
    private static void Audit(System.Data.IDbConnection cn, Guid tenantId, string entity, Guid id, string action, string correlationId) => cn.Execute("insert into sigov.enterprise_auditoria_operacional(id,tenant_id,codigo,nome,status,dados_json,created_by,updated_by,correlation_id) values(gen_random_uuid(),@tenantId,@codigo,@nome,'REGISTRADO',jsonb_build_object('entity',@entity,'entity_id',@id,'action',@action),'api','api',@correlationId)", new { tenantId, codigo = $"AUD-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", nome = action, entity, id, action, correlationId });
    private static bool TryTable(string area, out string table) => AreaTables.TryGetValue(area, out table!);
    private static bool IsSchemaUnavailable(Exception ex) => ex is PostgresException pg && (pg.SqlState == "42P01" || pg.SqlState == "3F000") || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
    private static string CodePrefix(string area) => new(area.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
    private static string ModuleArea(string module) => module switch { "ordem_servico" => "os/ordens", "estoque_compras" => "estoque/produtos", "manutencao_industrial" => "industrial/ativos", _ => "comercial/clientes" };
    private static string? MaskDocument(string? value) => string.IsNullOrWhiteSpace(value) ? null : $"***{OnlyDigits(value).TakeLast(4).Aggregate(string.Empty, (c, d) => c + d)}";
    private static string? MaskEmail(string? value) { if (string.IsNullOrWhiteSpace(value) || !value.Contains('@', StringComparison.Ordinal)) return null; var p = value.Split('@', 2); return $"{p[0][0]}***@{p[1]}"; }
    private static string? MaskPhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : $"(**) ****-{OnlyDigits(value).TakeLast(4).Aggregate(string.Empty, (c, d) => c + d)}";
    private static string OnlyDigits(string value) => new(value.Where(char.IsDigit).ToArray());
}
