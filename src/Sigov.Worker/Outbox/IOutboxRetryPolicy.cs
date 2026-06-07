namespace Sigov.Worker.Outbox;

public interface IOutboxRetryPolicy
{
    OutboxRetryDecision Calculate(int currentAttempts, int maxAttempts);
}

public sealed record OutboxRetryDecision(int NextAttempts, bool DeadLetter, TimeSpan Delay);
