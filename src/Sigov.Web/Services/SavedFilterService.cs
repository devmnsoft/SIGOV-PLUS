using System.Text.Json;
using Sigov.Application.Ui;

namespace Sigov.Web.Services;

public sealed class SavedFilterService(IUserPreferenceRepository repository)
{
    private const int ContractVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedValues =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["minha-central"] = new(StringComparer.OrdinalIgnoreCase) { "ABERTA", "EM_TRATAMENTO" },
            ["distribuicao-materiais"] = new(StringComparer.OrdinalIgnoreCase) { "RASCUNHO", "ENVIADA", "APROVADA", "ATENDIDA", "CANCELADA", "REJEITADA" }
        };

    public async Task<IReadOnlyList<SavedFilter>> ListAsync(long tenantId, long userId, string screen, CancellationToken ct)
    {
        EnsureScreen(screen);
        var row = await repository.GetAsync(tenantId, userId, Key(screen), ct).ConfigureAwait(false);
        if (row is null) return Array.Empty<SavedFilter>();
        try
        {
            var document = JsonSerializer.Deserialize<SavedFilterDocument>(row.ValueJson, JsonOptions);
            if (document?.Version != ContractVersion || document.Filters is null) return Array.Empty<SavedFilter>();
            return document.Filters.Where(IsValid).OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<SavedFilter>();
        }
    }

    public async Task SaveAsync(long tenantId, long userId, string screen, string name, string? status, bool isDefault, CancellationToken ct)
    {
        var normalizedName = RequiredName(name);
        var normalizedStatus = ValidateStatus(screen, status);
        var filters = (await ListAsync(tenantId, userId, screen, ct).ConfigureAwait(false)).ToList();
        var existing = filters.FindIndex(x => string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        var filter = new SavedFilter(normalizedName, normalizedStatus, isDefault);
        if (existing >= 0) filters[existing] = filter; else filters.Add(filter);
        if (isDefault) filters = filters.Select(x => x with { IsDefault = string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase) }).ToList();
        await PersistAsync(tenantId, userId, screen, filters, ct).ConfigureAwait(false);
    }

    public async Task RenameAsync(long tenantId, long userId, string screen, string currentName, string newName, CancellationToken ct)
    {
        var current = RequiredName(currentName);
        var replacement = RequiredName(newName);
        var filters = (await ListAsync(tenantId, userId, screen, ct).ConfigureAwait(false)).ToList();
        if (filters.Any(x => string.Equals(x.Name, replacement, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe um filtro com esse nome.");
        var index = filters.FindIndex(x => string.Equals(x.Name, current, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new KeyNotFoundException("O filtro não existe mais.");
        filters[index] = filters[index] with { Name = replacement };
        await PersistAsync(tenantId, userId, screen, filters, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(long tenantId, long userId, string screen, string name, CancellationToken ct)
    {
        var normalized = RequiredName(name);
        var filters = (await ListAsync(tenantId, userId, screen, ct).ConfigureAwait(false))
            .Where(x => !string.Equals(x.Name, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
        await PersistAsync(tenantId, userId, screen, filters, ct).ConfigureAwait(false);
    }

    private async Task PersistAsync(long tenantId, long userId, string screen, IReadOnlyList<SavedFilter> filters, CancellationToken ct)
    {
        await repository.UpsertAsync(new UserPreferenceUpdateRequest(tenantId, userId, Key(screen),
            JsonSerializer.Serialize(new SavedFilterDocument(ContractVersion, filters), JsonOptions)), ct).ConfigureAwait(false);
    }

    private static bool IsValid(SavedFilter filter) => !string.IsNullOrWhiteSpace(filter.Name) && filter.Name.Length <= 80;
    private static string Key(string screen) => $"saved-filters.v{ContractVersion}.{screen}";
    private static void EnsureScreen(string screen) { if (!AllowedValues.ContainsKey(screen)) throw new ArgumentException("Tela de filtro não permitida."); }
    private static string RequiredName(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 80) throw new ArgumentException("Informe um nome com até 80 caracteres.");
        return normalized;
    }
    private static string? ValidateStatus(string screen, string? status)
    {
        EnsureScreen(screen);
        if (string.IsNullOrWhiteSpace(status)) return null;
        var normalized = status.Trim().ToUpperInvariant();
        if (!AllowedValues[screen].Contains(normalized)) throw new ArgumentException("O status informado não é permitido nesta tela.");
        return normalized;
    }

    private sealed record SavedFilterDocument(int Version, IReadOnlyList<SavedFilter> Filters);
}

public sealed record SavedFilter(string Name, string? Status, bool IsDefault);
