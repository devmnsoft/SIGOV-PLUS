namespace Sigov.Application.Lgpd;

public interface ILgpdAccessLogger
{
    void LogPersonalDataAccess(string operation, string fieldName, long? tenantId, Guid? correlationId);
}
