using System.Security.Claims;
using OpenIddict.Abstractions;
using Safe.Host.Revocation;

namespace Safe.Host.Middleware;

public sealed class TokenRevocationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRevokedTokenCache _revokedTokenCache;
    private readonly ILogger<TokenRevocationMiddleware> _logger;

    public TokenRevocationMiddleware(
        RequestDelegate next,
        IRevokedTokenCache revokedTokenCache,
        ILogger<TokenRevocationMiddleware> logger)
    {
        _next = next;
        _revokedTokenCache = revokedTokenCache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tokenId = context.User.FindFirst(OpenIddictConstants.Claims.JwtId)?.Value
                          ?? context.User.FindFirst(ClaimTypes.SerialNumber)?.Value;

            var sessionId = context.User.FindFirst("sid")?.Value;

            if (_revokedTokenCache.IsRevoked(tokenId, sessionId))
            {
                _logger.LogInformation("Rejected request due to revoked token {TokenId} or session {SessionId}.", tokenId, sessionId);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Token revoked");
                return;
            }
        }

        await _next(context);
    }
}
