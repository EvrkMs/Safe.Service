using Auth.TokenValidation;
using Auth.TokenValidation.Models;
using System.Security.Claims;

namespace Safe.Host;

public class SampleMiddleware(RequestDelegate next, ITokenIntrospector introspector, ILogger<SampleMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var token = ExtractBearerToken(context);
        if (token is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        TokenIntrospectionResult result;
        try
        {
            result = await introspector.IntrospectAsync(token, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Token introspection failed");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!result.Active)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.User = CreatePrincipal(result);
        await next(context);
    }

    private static string? ExtractBearerToken(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var auth) || auth.Count == 0)
            return null;

        var value = auth.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? value.Substring("Bearer ".Length).Trim()
            : null;
    }

    private static ClaimsPrincipal CreatePrincipal(TokenIntrospectionResult result)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Subject ?? result.Username ?? "unknown"),
            new("iss", result.Issuer ?? string.Empty),
            new("token_type", result.TokenType ?? "access_token")
        };

        foreach (var s in result.Scopes) claims.Add(new("scope", s));
        foreach (var a in result.Audiences) claims.Add(new("aud", a));

        // Роли из разных форматов
        if (result.Raw.TryGetValue("role", out var roleEl) && roleEl.ValueKind == System.Text.Json.JsonValueKind.String)
            claims.Add(new(ClaimTypes.Role, roleEl.GetString()!));

        if (result.Raw.TryGetValue("roles", out var rolesEl) && rolesEl.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var it in rolesEl.EnumerateArray())
                if (it.ValueKind == System.Text.Json.JsonValueKind.String)
                    claims.Add(new(ClaimTypes.Role, it.GetString()!));

        if (result.Raw.TryGetValue("realm_access", out var realmEl) && realmEl.ValueKind == System.Text.Json.JsonValueKind.Object)
            if (realmEl.TryGetProperty("roles", out var rr) && rr.ValueKind == System.Text.Json.JsonValueKind.Array)
                foreach (var it in rr.EnumerateArray())
                    if (it.ValueKind == System.Text.Json.JsonValueKind.String)
                        claims.Add(new(ClaimTypes.Role, it.GetString()!));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "introspection"));
    }
}