namespace Sigov.Application.Common;

public static class ProblemErrorCodes
{
    public const string Validation = "validation.error";
    public const string Unauthorized = "security.unauthorized";
    public const string Forbidden = "security.forbidden";
    public const string NotFound = "resource.not_found";
    public const string BusinessRule = "business.rule";
    public const string TechnicalFailure = "technical.failure";
}
