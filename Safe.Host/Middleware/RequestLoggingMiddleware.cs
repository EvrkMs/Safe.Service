using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Safe.Host.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly RequestLoggingOptions _options;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IOptions<RequestLoggingOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var tokenSnippet = _options.IncludeAuthorizationToken
            ? ExtractTokenSnippet(context.Request)
            : null;

        try
        {
            await _next(context);

            stopwatch.Stop();
            _logger.LogInformation(
                "Safe.Host handled {Method} {Path} with {StatusCode} in {Elapsed} ms{TokenSuffix}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                tokenSnippet is null ? string.Empty : $" (token: {tokenSnippet})");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Safe.Host request {Method} {Path} failed after {Elapsed} ms{TokenSuffix}",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds,
                tokenSnippet is null ? string.Empty : $" (token: {tokenSnippet})");
            throw;
        }
    }

    private static string ExtractTokenSnippet(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return "<missing>";
        }

        var header = headerValues.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return "<non-bearer>";
        }

        var token = header["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return "<empty>";
        }

        const int previewLength = 16;
        return token.Length <= previewLength ? token : token[..previewLength] + "...";
    }
}
