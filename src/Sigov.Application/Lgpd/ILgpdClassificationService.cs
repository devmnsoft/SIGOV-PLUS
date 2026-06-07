namespace Sigov.Application.Lgpd;

public interface ILgpdClassificationService
{
    bool IsPersonalData(string fieldName);
    bool IsSensitiveData(string fieldName);
    bool IsSecret(string fieldName);
}
