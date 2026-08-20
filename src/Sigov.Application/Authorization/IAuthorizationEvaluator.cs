namespace Sigov.Application.Authorization;

public interface IAuthorizationEvaluator
{
    Task<AuthorizationDecision> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken = default);
}
