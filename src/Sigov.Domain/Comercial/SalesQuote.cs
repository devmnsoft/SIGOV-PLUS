namespace Sigov.Domain.Comercial;

public enum SalesQuoteStatus { Draft, PendingApproval, Approved, Rejected, Sent, Accepted, Refused, Expired, Converted, Cancelled }

public sealed record SalesQuoteItem(Guid ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal DiscountPercent)
{
    public decimal Subtotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public decimal Discount => decimal.Round(Subtotal * DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal - Discount;
}

public sealed class SalesQuote
{
    private readonly List<SalesQuoteItem> _items = [];

    private SalesQuote(Guid tenantId, Guid customerId, DateOnly validUntil)
    {
        if (tenantId == Guid.Empty || customerId == Guid.Empty) throw new CommercialRuleException("Tenant e cliente são obrigatórios.");
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CustomerId = customerId;
        ChangeValidityInternal(validUntil);
        Status = SalesQuoteStatus.Draft;
        RowVersion = 1;
    }

    public Guid Id { get; }
    public Guid TenantId { get; }
    public Guid CustomerId { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public SalesQuoteStatus Status { get; private set; }
    public decimal GlobalDiscountPercent { get; private set; }
    public decimal Subtotal => _items.Sum(x => x.Subtotal);
    public decimal Discount => _items.Sum(x => x.Discount) + decimal.Round((_items.Sum(x => x.Total) * GlobalDiscountPercent) / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal - Discount;
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public long RowVersion { get; private set; }
    public IReadOnlyList<SalesQuoteItem> Items => _items;

    public static SalesQuote CreateDraft(Guid tenantId, Guid customerId, DateOnly validUntil) => new(tenantId, customerId, validUntil);

    public void ChangeCustomer(Guid customerId) { EnsureDraft(); if (customerId == Guid.Empty) throw new CommercialRuleException("Cliente é obrigatório."); CustomerId = customerId; Touch(); }
    public void ChangeValidity(DateOnly validUntil) { EnsureDraft(); ChangeValidityInternal(validUntil); Touch(); }

    public void AddItem(Guid productId, string description, decimal quantity, decimal serverResolvedPrice, decimal discountPercent = 0)
    {
        EnsureDraft();
        ValidateItem(productId, description, quantity, serverResolvedPrice, discountPercent);
        _items.Add(new(productId, description.Trim(), quantity, serverResolvedPrice, discountPercent));
        Touch();
    }

    public void UpdateItemQuantity(Guid productId, decimal quantity)
    {
        EnsureDraft();
        if (quantity <= 0) throw new CommercialRuleException("A quantidade deve ser maior que zero.");
        var index = FindItem(productId);
        _items[index] = _items[index] with { Quantity = quantity };
        Touch();
    }

    public void RemoveItem(Guid productId) { EnsureDraft(); _items.RemoveAt(FindItem(productId)); Touch(); }
    public void ApplyItemDiscount(Guid productId, decimal percent) { EnsureDraft(); ValidateDiscount(percent); var index = FindItem(productId); _items[index] = _items[index] with { DiscountPercent = percent }; Touch(); }
    public void ApplyGlobalDiscount(decimal percent) { EnsureDraft(); ValidateDiscount(percent); GlobalDiscountPercent = percent; Touch(); }

    public void SubmitForApproval()
    {
        EnsureDraft();
        if (_items.Count == 0) throw new CommercialRuleException("Inclua ao menos um produto.");
        if (ValidUntil < DateOnly.FromDateTime(DateTime.UtcNow)) throw new CommercialConflictException("Orçamento vencido não pode ser enviado.");
        Status = SalesQuoteStatus.PendingApproval;
        Touch();
    }

    public void Approve(Guid userId, DateTimeOffset now) { EnsureStatus(SalesQuoteStatus.PendingApproval); if (userId == Guid.Empty) throw new CommercialRuleException("Aprovador é obrigatório."); ApprovedBy = userId; ApprovedAt = now; Status = SalesQuoteStatus.Approved; Touch(); }
    public void Reject(Guid userId, string reason) { EnsureStatus(SalesQuoteStatus.PendingApproval); if (userId == Guid.Empty || string.IsNullOrWhiteSpace(reason)) throw new CommercialRuleException("Aprovador e motivo da rejeição são obrigatórios."); RejectionReason = reason.Trim(); Status = SalesQuoteStatus.Rejected; Touch(); }
    public void ConvertToOrder() { EnsureStatus(SalesQuoteStatus.Approved); Status = SalesQuoteStatus.Converted; Touch(); }
    public void Cancel() { if (Status is SalesQuoteStatus.Converted or SalesQuoteStatus.Cancelled) throw new CommercialConflictException("Orçamento convertido ou cancelado não pode ser cancelado."); Status = SalesQuoteStatus.Cancelled; Touch(); }

    private int FindItem(Guid productId) { var index = _items.FindIndex(x => x.ProductId == productId); return index < 0 ? throw new CommercialNotFoundException("Item não encontrado.") : index; }
    private void EnsureDraft() => EnsureStatus(SalesQuoteStatus.Draft);
    private void EnsureStatus(SalesQuoteStatus expected) { if (Status != expected) throw new CommercialConflictException($"A operação exige o status {expected}."); }
    private void Touch() => RowVersion++;
    private void ChangeValidityInternal(DateOnly value) { if (value < DateOnly.FromDateTime(DateTime.UtcNow)) throw new CommercialRuleException("A validade não pode estar no passado."); ValidUntil = value; }
    private static void ValidateDiscount(decimal percent) { if (percent is < 0 or > 100) throw new CommercialRuleException("O desconto deve estar entre 0 e 100%."); }
    private static void ValidateItem(Guid productId, string description, decimal quantity, decimal price, decimal discount) { if (productId == Guid.Empty || string.IsNullOrWhiteSpace(description)) throw new CommercialRuleException("Produto e descrição são obrigatórios."); if (quantity <= 0 || price < 0) throw new CommercialRuleException("Quantidade e preço são inválidos."); ValidateDiscount(discount); }
}
