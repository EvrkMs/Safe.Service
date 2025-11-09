using Microsoft.AspNetCore.Http;
using Safe.Host.Extensions;
using Safe.Host.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(options =>
{
    options.ListenAnyIP(5001, listen =>
    {
        listen.UseHttps(https =>
        {
            https.ServerCertificate = EphemeralCert.Create();
        });
    });
});

builder.Services
    .AddSafeControllers()
    .AddSafeDatabase(builder.Configuration)
    .AddSafeAuthentication(builder.Configuration)
    .AddSafeAuthorization()
    .AddSafeCors()
    .AddSafeHealthChecks()
    .AddSafeApplicationServices(builder.Configuration);

var app = builder.Build();

await app.ApplyDatabaseMigrationsAsync();

app.UseCors("AllowAvaSubdomains");
app.UseCors("admin-ui");

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    await next();

    if (!context.Response.HasStarted &&
        context.Response.StatusCode == StatusCodes.Status401Unauthorized &&
        context.Request.Path.StartsWithSegments("/api/safe", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
    }
});

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
