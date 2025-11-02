using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Auth.TokenValidation;
using Auth.TokenValidation.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Safe.Host.Authentication;

public sealed class IntrospectionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ITokenIntrospector introspector) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
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

        TokenIntrospectionResult result;
        try
        {
            result = await introspector.IntrospectAsync(token, Context.RequestAborted);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Token introspection failed");
            return AuthenticateResult.Fail("Token introspection failed.");
        }

        if (!result.Active)
        {
            return AuthenticateResult.Fail("Token is inactive.");
        }

        var principal = CreatePrincipal(result);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
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
}
