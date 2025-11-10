using Microsoft.Extensions.Caching.Memory;

namespace Safe.Host.Revocation;

public sealed class RevokedTokenCache(IMemoryCache cache) : IRevokedTokenCache
{
    private static string TokenKey(string tokenId) => $"revoked:token:{tokenId}";
    private static string SessionKey(string sessionReferenceId) => $"revoked:sid:{sessionReferenceId}";

    public void MarkToken(string tokenId, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return;
        }

        cache.Set(TokenKey(tokenId), true, ttl);
    }

    public void MarkSession(string sessionReferenceId, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(sessionReferenceId))
        {
            return;
        }

        cache.Set(SessionKey(sessionReferenceId), true, ttl);
    }

    public bool IsRevoked(string? tokenId, string? sessionReferenceId)
    {
        if (!string.IsNullOrWhiteSpace(tokenId) && cache.TryGetValue(TokenKey(tokenId), out _))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(sessionReferenceId) && cache.TryGetValue(SessionKey(sessionReferenceId), out _))
        {
            return true;
        }

        return false;
    }
}
