namespace Safe.Host.Revocation;

public interface IRevokedTokenCache
{
    void MarkToken(string tokenId, TimeSpan ttl);
    void MarkSession(string sessionReferenceId, TimeSpan ttl);
    bool IsRevoked(string? tokenId, string? sessionReferenceId);
}
