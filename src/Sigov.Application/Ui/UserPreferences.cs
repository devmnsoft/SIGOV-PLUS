namespace Sigov.Application.Ui;

public sealed record UserPreferenceResponse(long? TenantId, long UserId, string Key, string ValueJson, DateTimeOffset UpdatedAt);

public sealed record UserPreferenceUpdateRequest(long? TenantId, long UserId, string Key, string ValueJson);

public interface IUserPreferenceService
{
    UserPreferenceResponse Save(UserPreferenceUpdateRequest request);

    UserPreferenceResponse Get(long? tenantId, long userId, string key);
}

public sealed class UserPreferenceService : IUserPreferenceService
{
    private readonly Dictionary<string, UserPreferenceResponse> _preferences = new(StringComparer.OrdinalIgnoreCase);

    public UserPreferenceResponse Save(UserPreferenceUpdateRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Key);
        var response = new UserPreferenceResponse(request.TenantId, request.UserId, request.Key, string.IsNullOrWhiteSpace(request.ValueJson) ? "{}" : request.ValueJson, DateTimeOffset.UtcNow);
        _preferences[BuildKey(request.TenantId, request.UserId, request.Key)] = response;
        return response;
    }

    public UserPreferenceResponse Get(long? tenantId, long userId, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _preferences.TryGetValue(BuildKey(tenantId, userId, key), out var response)
            ? response
            : new UserPreferenceResponse(tenantId, userId, key, "{}", DateTimeOffset.UtcNow);
    }

    private static string BuildKey(long? tenantId, long userId, string key) => $"{tenantId?.ToString() ?? "global"}:{userId}:{key}";
}

public sealed record SavedFilterResponse(long? TenantId, long UserId, string Module, string Resource, string Name, string FiltersJson);

public sealed record SavedFilterCreateRequest(long? TenantId, long UserId, string Module, string Resource, string Name, string FiltersJson);

public interface IUserSavedFilterService
{
    SavedFilterResponse Save(SavedFilterCreateRequest request);
}

public sealed class UserSavedFilterService : IUserSavedFilterService
{
    public SavedFilterResponse Save(SavedFilterCreateRequest request) => new(request.TenantId, request.UserId, request.Module, request.Resource, request.Name, string.IsNullOrWhiteSpace(request.FiltersJson) ? "{}" : request.FiltersJson);
}
