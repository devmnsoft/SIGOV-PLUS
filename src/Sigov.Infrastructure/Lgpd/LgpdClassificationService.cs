using Sigov.Application.Lgpd;

namespace Sigov.Infrastructure.Lgpd;

public sealed class LgpdClassificationService : ILgpdClassificationService
{
    public bool IsPersonalData(string fieldName) => PersonalDataFieldCatalog.Fields.Contains(Normalize(fieldName));
    public bool IsSensitiveData(string fieldName) => SensitiveFieldCatalog.Fields.Contains(Normalize(fieldName));
    public bool IsSecret(string fieldName) => SensitiveFieldCatalog.SecretFields.Any(secret => Normalize(fieldName).Contains(secret, StringComparison.Ordinal));
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
