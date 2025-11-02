using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Safe.Host.Introspection;

namespace Safe.Host.Authentication;

public sealed class IntrospectionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RateLimitCacheDuration = TimeSpan.FromSeconds(2);

    private readonly ITokenIntrospector _introspector;
    private readonly IMemoryCache _cache;
    private readonly ILogger<IntrospectionAuthenticationHandler> _logger;

    public IntrospectionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ITokenIntrospector introspector,
        IMemoryCache cache)
        : base(options, loggerFactory, encoder)
    {
        _introspector = introspector;
        _cache = cache;
        _logger = loggerFactory.CreateLogger<IntrospectionAuthenticationHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var rawHeader) || rawHeader.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var token = ExtractBearerToken(rawHeader);
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        if (_cache.TryGetValue(token, out CachedTokenEntry? cachedEntry) &&
            cachedEntry is { State: CachedTokenState.Active, Principal: { } principalFromCache })
        {
            return AuthenticateResult.Success(new AuthenticationTicket(principalFromCache, Scheme.Name));
        }

        cachedEntry = await _cache.GetOrCreateAsync(token, entry => CreateCacheEntryAsync(entry, token))
                       ?? CachedTokenEntry.Inactive();

        return cachedEntry.State switch
        {
            CachedTokenState.Active when cachedEntry.Principal is not null =>
                AuthenticateResult.Success(new AuthenticationTicket(cachedEntry.Principal, Scheme.Name)),
            CachedTokenState.Inactive => AuthenticateResult.Fail("Token is inactive."),
            CachedTokenState.Error => AuthenticateResult.Fail(cachedEntry.Error ?? "Token introspection failed."),
            _ => AuthenticateResult.Fail("Token introspection failed.")
        };
    }

    private async Task<CachedTokenEntry> CreateCacheEntryAsync(ICacheEntry entry, string token)
    {
        try
        {
            var result = await _introspector.IntrospectAsync(token, Context.RequestAborted);

            if (!result.Active)
            {
                entry.AbsoluteExpirationRelativeToNow = FailureCacheDuration;
                return CachedTokenEntry.Inactive();
            }

            var principal = CreatePrincipal(result);
            entry.AbsoluteExpirationRelativeToNow = GetSuccessTtl(result);
            return CachedTokenEntry.Active(principal);
        }
        catch (Exception ex) when (IsRateLimitException(ex))
        {
            entry.AbsoluteExpirationRelativeToNow = RateLimitCacheDuration;
            _logger.LogWarning(ex, "Token introspection throttled by authority.");
            return CachedTokenEntry.FromError("Token introspection failed due to rate limiting.");
        }
        catch (Exception ex)
        {
            entry.AbsoluteExpirationRelativeToNow = FailureCacheDuration;
            _logger.LogWarning(ex, "Token introspection failed");
            return CachedTokenEntry.FromError("Token introspection failed.");
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static string? ExtractBearerToken(StringValues headerValues)
    {
        var value = headerValues.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = value["Bearer ".Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    private static ClaimsPrincipal CreatePrincipal(TokenIntrospectionResult result)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Subject ?? result.Username ?? "unknown"),
            new("iss", result.Issuer ?? string.Empty),
            new("token_type", result.TokenType ?? "access_token")
        };

        foreach (var scope in result.Scopes)
        {
            claims.Add(new("scope", scope));
        }

        foreach (var audience in result.Audiences)
        {
            claims.Add(new("aud", audience));
        }

        AddRoleClaims(result.Raw, claims);
        var identity = new ClaimsIdentity(claims, "introspection");
        return new ClaimsPrincipal(identity);
    }

    private static void AddRoleClaims(IReadOnlyDictionary<string, JsonElement> raw, ICollection<Claim> claims)
    {
        if (raw.TryGetValue("role", out var roleElement) && roleElement.ValueKind == JsonValueKind.String)
        {
            claims.Add(new(ClaimTypes.Role, roleElement.GetString()!));
        }

        if (raw.TryGetValue("roles", out var rolesElement) && rolesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in rolesElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    claims.Add(new(ClaimTypes.Role, element.GetString()!));
                }
            }
        }

        if (!raw.TryGetValue("realm_access", out var realmAccess) || realmAccess.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!realmAccess.TryGetProperty("roles", out var realmRoles) || realmRoles.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var element in realmRoles.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                claims.Add(new(ClaimTypes.Role, element.GetString()!));
            }
        }
    }

    private static TimeSpan GetSuccessTtl(TokenIntrospectionResult result)
    {
        if (result.ExpiresAt is { } expiresAt)
        {
            var ttl = expiresAt - DateTimeOffset.UtcNow;
            if (ttl > TimeSpan.FromSeconds(15))
            {
                return ttl;
            }
        }

        return TimeSpan.FromMinutes(5);
    }

    private static bool IsRateLimitException(Exception ex)
        => ex is InvalidOperationException ioe && ioe.Message.Contains("429", StringComparison.OrdinalIgnoreCase);

    private enum CachedTokenState
    {
        Active,
        Inactive,
        Error
    }

    private sealed record CachedTokenEntry(CachedTokenState State, ClaimsPrincipal? Principal, string? Error)
    {
        public static CachedTokenEntry Active(ClaimsPrincipal principal) => new(CachedTokenState.Active, principal, null);
        public static CachedTokenEntry Inactive() => new(CachedTokenState.Inactive, null, null);
        public static CachedTokenEntry FromError(string? error) => new(CachedTokenState.Error, null, error);
    }
}
