namespace Safe.Host.Revocation;

public sealed class RedisRevocationOptions
{
    public string ConnectionString { get; set; } = "redis:6379";
    public string RevocationChannel { get; set; } = "revoked_tokens";
    public int RevocationEntryTtlSeconds { get; set; } = 3600;
}
