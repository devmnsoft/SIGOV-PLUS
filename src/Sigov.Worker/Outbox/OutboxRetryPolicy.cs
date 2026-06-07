namespace Sigov.Worker.Outbox;

public sealed class OutboxRetryPolicy : IOutboxRetryPolicy
{
    public OutboxRetryDecision Calculate(int currentAttempts, int maxAttempts)
    {
        var nextAttempts = currentAttempts + 1;
        var deadLetter = nextAttempts >= maxAttempts;
        var delay = nextAttempts switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromHours(1)
        };
        return new OutboxRetryDecision(nextAttempts, deadLetter, delay);
    }
}
