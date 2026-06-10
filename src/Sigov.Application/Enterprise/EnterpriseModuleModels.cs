namespace Sigov.Application.Enterprise;

public static class EnterpriseModules
{
    public const string Comercial = "comercial";
    public const string OrdemServico = "ordem_servico";
    public const string ManutencaoIndustrial = "manutencao_industrial";
    public const string EstoqueCompras = "estoque_compras";
    public const string ComercioVarejo = "comercio_varejo";
    public const string ComercioAtacado = "comercio_atacado";
    public const string IndustriaProducao = "industria_producao";
    public const string FinanceiroEmpresarial = "financeiro_empresarial";
}

public sealed record EnterpriseAuditEvent(Guid TenantId, string Entity, Guid EntityId, string Action, DateTimeOffset OccurredAt, string CorrelationId);

public sealed record EnterpriseListItem(Guid Id, Guid TenantId, string Name, string Status, string? DocumentMasked, string? EmailMasked, string? PhoneMasked, DateTimeOffset UpdatedAt);

public sealed record EnterpriseMutationRequest(Guid? TenantId, string? Nome, string? Documento, string? Email, string? Telefone, decimal? Valor, string? Status, Guid? ClienteId, Guid? ProdutoId, int? Quantidade, bool? PermitirSaldoNegativo);

public sealed record EnterpriseActionResult(Guid Id, Guid TenantId, string Status, string Message, Guid? RelatedId = null);

public sealed record OrdemServicoDetail(Guid Id, Guid TenantId, string Numero, string Status, IReadOnlyList<string> Itens, IReadOnlyList<string> Checklist, decimal HorasApontadas);

public sealed record EstoqueSaldo(Guid ProdutoId, Guid TenantId, string Produto, decimal Quantidade, decimal Minimo, bool AbaixoDoMinimo);

public sealed record EnterpriseDashboard(string Module, int Registros, int Pendencias, IReadOnlyList<string> Alertas, IReadOnlyList<EnterpriseAuditEvent> Auditoria);

public interface IEnterpriseModuleService
{
    IReadOnlyList<EnterpriseListItem> List(string area, Guid tenantId);

    EnterpriseActionResult Upsert(string area, EnterpriseMutationRequest request, Guid tenantId, string correlationId);

    EnterpriseActionResult ApproveProposal(Guid id, Guid tenantId, string correlationId);

    EnterpriseActionResult RejectProposal(Guid id, Guid tenantId, string correlationId);

    EnterpriseActionResult GenerateOrderFromProposal(Guid id, Guid tenantId, string correlationId);

    EnterpriseActionResult ConfirmCommercialOrder(Guid id, Guid tenantId, string correlationId);

    EnterpriseActionResult CancelCommercialOrder(Guid id, Guid tenantId, string correlationId);

    EnterpriseActionResult GenerateServiceOrderFromOrder(Guid id, Guid tenantId, string correlationId);

    OrdemServicoDetail GetServiceOrder(Guid id, Guid tenantId);

    EnterpriseActionResult ChangeServiceOrderStatus(Guid id, Guid tenantId, string status, string correlationId);

    EnterpriseActionResult AddServiceOrderEntry(Guid id, Guid tenantId, string entry, string correlationId);

    EnterpriseActionResult ConsumeStock(Guid ordemServicoId, Guid tenantId, Guid produtoId, int quantidade, bool permitirSaldoNegativo, string correlationId);

    EnterpriseActionResult GeneratePreventiveServiceOrder(Guid planoId, Guid tenantId, string correlationId);

    EnterpriseActionResult AddMeterReading(Guid medidorId, Guid tenantId, decimal leitura, string correlationId);

    IReadOnlyList<EstoqueSaldo> GetStock(Guid tenantId);

    EnterpriseActionResult MoveStock(Guid tenantId, Guid produtoId, int quantidade, string movement, bool permitirSaldoNegativo, string correlationId);

    EnterpriseDashboard GetDashboard(string module, Guid tenantId);
}
