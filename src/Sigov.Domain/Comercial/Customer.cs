namespace Sigov.Domain.Comercial;

public enum CustomerKind { Individual, Company }
public enum CustomerStatus { Active, Inactive }

public sealed record CustomerContact(
    Guid Id,
    string Name,
    string? Role,
    string? Email,
    string? Phone,
    string? WhatsApp,
    bool IsPrimary);

public sealed record CustomerAddress(
    Guid Id,
    string Type,
    string PostalCode,
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State,
    bool IsPrimary,
    bool IsBilling,
    bool IsDelivery);

public sealed class Customer
{
    private readonly List<CustomerContact> _contacts = [];
    private readonly List<CustomerAddress> _addresses = [];

    private Customer(Guid id, Guid tenantId, string name, CustomerKind kind, string document)
    {
        Id = id;
        TenantId = tenantId;
        Name = Required(name, "O nome do cliente é obrigatório.");
        Kind = kind;
        Document = Required(document, "O CPF/CNPJ é obrigatório.");
        Status = CustomerStatus.Active;
        RowVersion = 1;
    }

    public Guid Id { get; }
    public Guid TenantId { get; }
    public string Name { get; private set; }
    public string? TradeName { get; private set; }
    public CustomerKind Kind { get; }
    public string Document { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public CustomerStatus Status { get; private set; }
    public long RowVersion { get; private set; }
    public IReadOnlyList<CustomerContact> Contacts => _contacts;
    public IReadOnlyList<CustomerAddress> Addresses => _addresses;

    public static Customer Create(Guid tenantId, string name, CustomerKind kind, string document)
    {
        if (tenantId == Guid.Empty) throw new CommercialRuleException("Tenant é obrigatório.");
        return new Customer(Guid.NewGuid(), tenantId, name, kind, document);
    }

    public void ChangeName(string name) => Change(() => Name = Required(name, "O nome do cliente é obrigatório."));
    public void ChangeDocument(string document) => Change(() => Document = Required(document, "O CPF/CNPJ é obrigatório."));
    public void ChangeTradeName(string? tradeName) => Change(() => TradeName = Clean(tradeName));
    public void ChangeEmail(string? email) => Change(() => Email = Clean(email));
    public void ChangePhone(string? phone) => Change(() => Phone = Clean(phone));

    public CustomerContact AddContact(string name, string? role, string? email, string? phone, string? whatsapp, bool primary)
    {
        EnsureActive();
        var contact = new CustomerContact(Guid.NewGuid(), Required(name, "O nome do contato é obrigatório."), Clean(role), Clean(email), Clean(phone), Clean(whatsapp), primary);
        if (primary || _contacts.Count == 0) SetPrimaryContactInternal(contact.Id);
        _contacts.Add(contact with { IsPrimary = primary || _contacts.Count == 0 });
        Touch();
        return _contacts[^1];
    }

    public void RemoveContact(Guid id)
    {
        var index = _contacts.FindIndex(x => x.Id == id);
        if (index < 0) throw new CommercialNotFoundException("Contato não encontrado.");
        var wasPrimary = _contacts[index].IsPrimary;
        _contacts.RemoveAt(index);
        if (wasPrimary && _contacts.Count > 0) _contacts[0] = _contacts[0] with { IsPrimary = true };
        Touch();
    }

    public void UpdateContact(Guid id, string name, string? role, string? email, string? phone, string? whatsapp)
    {
        EnsureActive();
        var index = _contacts.FindIndex(x => x.Id == id);
        if (index < 0) throw new CommercialNotFoundException("Contato não encontrado.");
        var current = _contacts[index];
        _contacts[index] = current with
        {
            Name = Required(name, "O nome do contato é obrigatório."),
            Role = Clean(role),
            Email = Clean(email),
            Phone = Clean(phone),
            WhatsApp = Clean(whatsapp)
        };
        Touch();
    }

    public void SetPrimaryContact(Guid id)
    {
        if (_contacts.All(x => x.Id != id)) throw new CommercialNotFoundException("Contato não encontrado.");
        SetPrimaryContactInternal(id);
        Touch();
    }

    public CustomerAddress AddAddress(string type, string postalCode, string street, string number, string? complement, string district, string city, string state, bool primary, bool billing, bool delivery)
    {
        EnsureActive();
        var id = Guid.NewGuid();
        if (primary || _addresses.Count == 0) SetPrimaryAddressInternal(id);
        var address = new CustomerAddress(id, Required(type, "O tipo do endereço é obrigatório."), Required(postalCode, "O CEP é obrigatório."), Required(street, "O logradouro é obrigatório."), Required(number, "O número é obrigatório."), Clean(complement), Required(district, "O bairro é obrigatório."), Required(city, "A cidade é obrigatória."), Required(state, "A UF é obrigatória.").ToUpperInvariant(), primary || _addresses.Count == 0, billing, delivery);
        _addresses.Add(address);
        Touch();
        return address;
    }

    public void RemoveAddress(Guid id)
    {
        var index = _addresses.FindIndex(x => x.Id == id);
        if (index < 0) throw new CommercialNotFoundException("Endereço não encontrado.");
        var wasPrimary = _addresses[index].IsPrimary;
        _addresses.RemoveAt(index);
        if (wasPrimary && _addresses.Count > 0) _addresses[0] = _addresses[0] with { IsPrimary = true };
        Touch();
    }

    public void UpdateAddress(Guid id, string type, string postalCode, string street, string number, string? complement, string district, string city, string state, bool billing, bool delivery)
    {
        EnsureActive();
        var index = _addresses.FindIndex(x => x.Id == id);
        if (index < 0) throw new CommercialNotFoundException("Endereço não encontrado.");
        var current = _addresses[index];
        _addresses[index] = current with
        {
            Type = Required(type, "O tipo do endereço é obrigatório."),
            PostalCode = Required(postalCode, "O CEP é obrigatório."),
            Street = Required(street, "O logradouro é obrigatório."),
            Number = Required(number, "O número é obrigatório."),
            Complement = Clean(complement),
            District = Required(district, "O bairro é obrigatório."),
            City = Required(city, "A cidade é obrigatória."),
            State = Required(state, "A UF é obrigatória.").ToUpperInvariant(),
            IsBilling = billing,
            IsDelivery = delivery
        };
        Touch();
    }

    public void SetPrimaryAddress(Guid id)
    {
        if (_addresses.All(x => x.Id != id)) throw new CommercialNotFoundException("Endereço não encontrado.");
        SetPrimaryAddressInternal(id);
        Touch();
    }

    public void Activate()
    {
        if (Status == CustomerStatus.Active) return;
        Status = CustomerStatus.Active;
        Touch();
    }

    public void Deactivate() => Change(() => Status = CustomerStatus.Inactive);

    private void Change(Action change) { EnsureActive(); change(); Touch(); }
    private void EnsureActive() { if (Status == CustomerStatus.Inactive) throw new CommercialConflictException("Cliente inativo não pode ser alterado."); }
    private void Touch() => RowVersion++;
    private void SetPrimaryContactInternal(Guid id) { for (var i = 0; i < _contacts.Count; i++) _contacts[i] = _contacts[i] with { IsPrimary = _contacts[i].Id == id }; }
    private void SetPrimaryAddressInternal(Guid id) { for (var i = 0; i < _addresses.Count; i++) _addresses[i] = _addresses[i] with { IsPrimary = _addresses[i].Id == id }; }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new CommercialRuleException(message) : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
