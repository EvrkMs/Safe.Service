namespace Safe.Host.Revocation;

public sealed record RevocationNotification(
    string? TokenId,
    string? AuthorizationId,
    string? SessionReferenceId,
    string? Reason,
    DateTimeOffset? TimestampUtc,
    string? ClientId,
    int? TokenCount);
