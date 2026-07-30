namespace Sigov.Application.Enterprise;

public sealed class EnterpriseModuleService : IEnterpriseModuleService
{
    private readonly Dictionary<string, EnterpriseListItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OrdemServicoDetail> _serviceOrders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EstoqueSaldo> _stock = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<EnterpriseAuditEvent> _audit = new();

    public IReadOnlyList<EnterpriseListItem> List(string area, Guid tenantId)
    {
        EnsureSeed(tenantId);
        return _items.Values
            .Where(item => item.TenantId == tenantId && item.Status.StartsWith(area + ":", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .ToArray();
    }

    public EnterpriseActionResult Upsert(string area, EnterpriseMutationRequest request, Guid tenantId, string correlationId)
    {
        var effectiveTenant = request.TenantId.GetValueOrDefault(tenantId);
        if (effectiveTenant != tenantId)
        {
            return new EnterpriseActionResult(Guid.Empty, tenantId, "FORBIDDEN", "tenant_id divergente: operação bloqueada por isolamento SaaS.");
        }

        var id = Guid.NewGuid();
        var name = string.IsNullOrWhiteSpace(request.Nome) ? $"{area} demo" : request.Nome.Trim();
        var status = $"{area}:{(string.IsNullOrWhiteSpace(request.Status) ? "ATIVO" : request.Status.Trim().ToUpperInvariant())}";
        var item = new EnterpriseListItem(id, tenantId, name, status, MaskDocument(request.Documento), MaskEmail(request.Email), MaskPhone(request.Telefone), DateTimeOffset.UtcNow);
        _items[Key(area, tenantId, id)] = item;
        Audit(tenantId, area, id, "criar_editar", correlationId);

        if (area.Equals("estoque/produtos", StringComparison.OrdinalIgnoreCase))
        {
            _stock[Key("estoque", tenantId, id)] = new EstoqueSaldo(id, tenantId, name, request.Quantidade.GetValueOrDefault(25), 10, request.Quantidade.GetValueOrDefault(25) < 10);
        }

        return new EnterpriseActionResult(id, tenantId, "OK", "Registro salvo com tenant_id, auditoria e mascaramento LGPD.");
    }

    public EnterpriseActionResult ApproveProposal(Guid id, Guid tenantId, string correlationId)
    {
        Audit(tenantId, "comercial_proposta", id, "aprovar", correlationId);
        return new EnterpriseActionResult(id, tenantId, "APROVADA", "Proposta aprovada e pronta para geração de pedido.");
    }

    public EnterpriseActionResult RejectProposal(Guid id, Guid tenantId, string correlationId)
    {
        Audit(tenantId, "comercial_proposta", id, "reprovar", correlationId);
        return new EnterpriseActionResult(id, tenantId, "REPROVADA", "Proposta reprovada com trilha de auditoria.");
    }

    public EnterpriseActionResult GenerateOrderFromProposal(Guid id, Guid tenantId, string correlationId)
    {
        var pedidoId = Guid.NewGuid();
        _items[Key("comercial/pedidos", tenantId, pedidoId)] = new EnterpriseListItem(pedidoId, tenantId, $"Pedido da proposta {id.ToString()[..8]}", "comercial/pedidos:ABERTO", null, null, null, DateTimeOffset.UtcNow);
        Audit(tenantId, "comercial_proposta", id, "gerar_pedido", correlationId);
        Audit(tenantId, "comercial_pedido", pedidoId, "criar_por_proposta", correlationId);
        return new EnterpriseActionResult(id, tenantId, "PEDIDO_GERADO", "Proposta aprovada gerou pedido comercial.", pedidoId);
    }

    public EnterpriseActionResult ConfirmCommercialOrder(Guid id, Guid tenantId, string correlationId)
    {
        Audit(tenantId, "comercial_pedido", id, "confirmar", correlationId);
        return new EnterpriseActionResult(id, tenantId, "CONFIRMADO", "Pedido confirmado e elegível para OS quando aplicável.");
    }

    public EnterpriseActionResult CancelCommercialOrder(Guid id, Guid tenantId, string correlationId)
    {
        Audit(tenantId, "comercial_pedido", id, "cancelar", correlationId);
        return new EnterpriseActionResult(id, tenantId, "CANCELADO", "Pedido cancelado.");
    }

    public EnterpriseActionResult GenerateServiceOrderFromOrder(Guid id, Guid tenantId, string correlationId)
    {
        var osId = Guid.NewGuid();
        _serviceOrders[Key("os", tenantId, osId)] = new OrdemServicoDetail(osId, tenantId, $"OS-{osId.ToString()[..8]}", "ABERTA", new[] { $"Pedido {id}" }, Array.Empty<string>(), 0);
        Audit(tenantId, "os_ordem_servico", osId, "criar_por_pedido", correlationId);
        return new EnterpriseActionResult(id, tenantId, "OS_GERADA", "Pedido gerou ordem de serviço integrada.", osId);
    }

    public OrdemServicoDetail GetServiceOrder(Guid id, Guid tenantId)
    {
        EnsureSeed(tenantId);
        return _serviceOrders.TryGetValue(Key("os", tenantId, id), out var detail)
            ? detail
            : new OrdemServicoDetail(id, tenantId, $"OS-{id.ToString()[..8]}", "ABERTA", Array.Empty<string>(), Array.Empty<string>(), 0);
    }

    public EnterpriseActionResult ChangeServiceOrderStatus(Guid id, Guid tenantId, string status, string correlationId)
    {
        var detail = GetServiceOrder(id, tenantId) with { Status = status };
        _serviceOrders[Key("os", tenantId, id)] = detail;
        Audit(tenantId, "os_ordem_servico", id, status.ToLowerInvariant(), correlationId);
        return new EnterpriseActionResult(id, tenantId, status, "Status da OS atualizado com histórico.");
    }

    public EnterpriseActionResult AddServiceOrderEntry(Guid id, Guid tenantId, string entry, string correlationId)
    {
        var detail = GetServiceOrder(id, tenantId);
        var checklist = detail.Checklist.Concat(new[] { entry }).ToArray();
        _serviceOrders[Key("os", tenantId, id)] = detail with { Checklist = checklist, HorasApontadas = detail.HorasApontadas + 1 };
        Audit(tenantId, "os_ordem_servico", id, "apontamento_checklist", correlationId);
        return new EnterpriseActionResult(id, tenantId, "REGISTRADO", "Apontamento/checklist registrado na OS.");
    }

    public EnterpriseActionResult ConsumeStock(Guid ordemServicoId, Guid tenantId, Guid produtoId, int quantidade, bool permitirSaldoNegativo, string correlationId)
    {
        var result = MoveStock(tenantId, produtoId, quantidade, "CONSUMO_OS", permitirSaldoNegativo, correlationId);
        if (result.Status == "OK")
        {
            Audit(tenantId, "os_ordem_servico", ordemServicoId, "consumir_peca", correlationId);
        }

        return result;
    }

    public EnterpriseActionResult GeneratePreventiveServiceOrder(Guid planoId, Guid tenantId, string correlationId)
    {
        var osId = Guid.NewGuid();
        _serviceOrders[Key("os", tenantId, osId)] = new OrdemServicoDetail(osId, tenantId, $"OS-PREV-{osId.ToString()[..8]}", "AGENDADA", new[] { $"Plano preventivo {planoId}" }, new[] { "Checklist preventivo" }, 0);
        Audit(tenantId, "industrial_plano_manutencao", planoId, "gerar_os_preventiva", correlationId);
        return new EnterpriseActionResult(planoId, tenantId, "OS_PREVENTIVA_GERADA", "Plano de manutenção gerou OS preventiva.", osId);
    }

    public EnterpriseActionResult AddMeterReading(Guid medidorId, Guid tenantId, decimal leitura, string correlationId)
    {
        Audit(tenantId, "industrial_leitura_medidor", medidorId, $"leitura_{leitura}", correlationId);
        return new EnterpriseActionResult(medidorId, tenantId, "LEITURA_REGISTRADA", "Leitura de medidor registrada.");
    }

    public IReadOnlyList<EstoqueSaldo> GetStock(Guid tenantId)
    {
        EnsureSeed(tenantId);
        return _stock.Values.Where(item => item.TenantId == tenantId).OrderBy(item => item.Produto).ToArray();
    }

    public EnterpriseActionResult MoveStock(Guid tenantId, Guid produtoId, int quantidade, string movement, bool permitirSaldoNegativo, string correlationId)
    {
        EnsureSeed(tenantId);
        var key = _stock.Keys.FirstOrDefault(candidate => candidate.EndsWith(produtoId.ToString(), StringComparison.OrdinalIgnoreCase));
        var saldo = key is null ? new EstoqueSaldo(produtoId, tenantId, "Produto integrado", 20, 10, false) : _stock[key];
        var signedQuantity = movement is "ENTRADA" or "AJUSTE_POSITIVO" ? quantidade : -quantidade;
        var novaQuantidade = saldo.Quantidade + signedQuantity;
        if (novaQuantidade < 0 && !permitirSaldoNegativo)
        {
            Audit(tenantId, "estoque_movimento", produtoId, "bloquear_saldo_negativo", correlationId);
            return new EnterpriseActionResult(produtoId, tenantId, "SALDO_INSUFICIENTE", "Estoque não permite saldo negativo sem permissão explícita.");
        }

        _stock[Key("estoque", tenantId, produtoId)] = saldo with { Quantidade = novaQuantidade, AbaixoDoMinimo = novaQuantidade < saldo.Minimo };
        Audit(tenantId, "estoque_movimento", produtoId, movement.ToLowerInvariant(), correlationId);
        return new EnterpriseActionResult(produtoId, tenantId, "OK", "Movimento de estoque registrado.");
    }

    public EnterpriseDashboard GetDashboard(string module, Guid tenantId)
    {
        EnsureSeed(tenantId);
        var alertas = GetStock(tenantId).Where(item => item.AbaixoDoMinimo).Select(item => $"Produto {item.Produto} abaixo do mínimo").ToArray();
        var audit = _audit.Where(item => item.TenantId == tenantId).TakeLast(10).ToArray();
        return new EnterpriseDashboard(module, _items.Values.Count(item => item.TenantId == tenantId), alertas.Length, alertas, audit);
    }

    private void EnsureSeed(Guid tenantId)
    {
        if (_stock.Values.Any(item => item.TenantId == tenantId))
        {
            return;
        }

        var osId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        _items[Key("os/ordens", tenantId, osId)] = new EnterpriseListItem(osId, tenantId, "OS demonstração", "os/ordens:ABERTA", null, null, null, DateTimeOffset.UtcNow);
        _serviceOrders[Key("os", tenantId, osId)] = new OrdemServicoDetail(osId, tenantId, "OS-DEMO", "ABERTA", new[] { "Diagnóstico inicial" }, new[] { "Checklist seguro" }, 0);

        var produtoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _stock[Key("estoque", tenantId, produtoId)] = new EstoqueSaldo(produtoId, tenantId, "Peça crítica LGPD-safe", 8, 10, true);
    }

    private void Audit(Guid tenantId, string entity, Guid entityId, string action, string correlationId)
    {
        _audit.Enqueue(new EnterpriseAuditEvent(tenantId, entity, entityId, action, DateTimeOffset.UtcNow, correlationId));
        while (_audit.Count > 200) _audit.Dequeue();
    }

    private static string Key(string area, Guid tenantId, Guid id) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{area}:{tenantId}:{id}");

    private static string? MaskDocument(string? value) => string.IsNullOrWhiteSpace(value) ? null : $"***{OnlyDigits(value).TakeLast(4).Aggregate(string.Empty, (current, digit) => current + digit)}";

    private static string? MaskEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@', StringComparison.Ordinal))
        {
            return null;
        }

        var parts = value.Split('@', 2);
        return $"{parts[0][0]}***@{parts[1]}";
    }

    private static string? MaskPhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : $"(**) ****-{OnlyDigits(value).TakeLast(4).Aggregate(string.Empty, (current, digit) => current + digit)}";

    private static string OnlyDigits(string value) => new(value.Where(char.IsDigit).ToArray());
}
