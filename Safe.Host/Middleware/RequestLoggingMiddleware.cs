using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Safe.Host.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tokenSnippet = ExtractTokenSnippet(context.Request);
        _logger.LogInformation(
            "Safe.Host incoming request {Method} {Path}. Authorization bearer: {TokenSnippet}",
            context.Request.Method,
            context.Request.Path,
            tokenSnippet);

        try
        {
            await _next(context);
            _logger.LogInformation(
                "Safe.Host response {StatusCode} for {Method} {Path}",
                context.Response.StatusCode,
                context.Request.Method,
                context.Request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Safe.Host request {Method} {Path} failed while processing bearer: {TokenSnippet}",
                context.Request.Method,
                context.Request.Path,
                tokenSnippet);
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

        const int previewLength = 24;
        return token.Length <= previewLength ? token : token[..previewLength] + "...";
    }
}
