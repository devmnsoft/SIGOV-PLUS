using FluentAssertions;
using Sigov.Worker.Outbox;
using Xunit;

namespace Sigov.UnitTests.Hardening;

public sealed class OutboxRetryPolicyTests
{
    [Theory]
    [InlineData(0, 3, false, 60)]
    [InlineData(1, 3, false, 300)]
    [InlineData(2, 3, true, 900)]
    public void Deve_Calcular_Proxima_Tentativa_E_DeadLetter(int currentAttempts, int maxAttempts, bool deadLetter, int delaySeconds)
    {
        var decision = new OutboxRetryPolicy().Calculate(currentAttempts, maxAttempts);

        decision.NextAttempts.Should().Be(currentAttempts + 1);
        decision.DeadLetter.Should().Be(deadLetter);
        decision.Delay.Should().Be(TimeSpan.FromSeconds(delaySeconds));
    }
}
