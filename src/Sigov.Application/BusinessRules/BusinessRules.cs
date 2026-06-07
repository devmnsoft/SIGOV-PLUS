namespace Sigov.Application.BusinessRules;

public enum BusinessRuleSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public sealed record BusinessRuleContext(long? TenantId, long? UserId, string Module, IReadOnlyDictionary<string, object?> Values);

public sealed record BusinessRuleViolation(string Code, string Message, BusinessRuleSeverity Severity, string Module);

public sealed record BusinessRuleResult(bool IsValid, IReadOnlyList<BusinessRuleViolation> Violations)
{
    public static BusinessRuleResult Success { get; } = new(true, Array.Empty<BusinessRuleViolation>());
}

public interface IBusinessRule
{
    string Code { get; }

    string Module { get; }

    string Description { get; }

    BusinessRuleSeverity Severity { get; }
}

public sealed record BusinessRuleDefinition(string Code, string Module, string Description, BusinessRuleSeverity Severity) : IBusinessRule;

public interface IBusinessRuleEvaluator
{
    BusinessRuleResult Evaluate(BusinessRuleContext context, IEnumerable<IBusinessRule> rules);
}

public sealed class BusinessRuleEvaluator : IBusinessRuleEvaluator
{
    public BusinessRuleResult Evaluate(BusinessRuleContext context, IEnumerable<IBusinessRule> rules)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rules);

        var violations = rules
            .Where(rule => rule.Severity is BusinessRuleSeverity.Error or BusinessRuleSeverity.Critical)
            .Select(rule => new BusinessRuleViolation(rule.Code, rule.Description, rule.Severity, rule.Module))
            .ToArray();

        return new BusinessRuleResult(violations.Length == 0, violations);
    }
}
