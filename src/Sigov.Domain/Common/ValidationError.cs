namespace Sigov.Domain.Common;

public sealed record ValidationError(string Field, string Message, string? Code = null);
