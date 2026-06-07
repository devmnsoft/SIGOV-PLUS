namespace Sigov.Worker.Outbox;

public sealed record OutboxProcessingResult(int Fetched, int Processed, int Failed);
