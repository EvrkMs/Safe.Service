using System;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using OpenIddict.Validation.AspNetCore;
using Safe.Application.Services;
using Safe.EntityFramework;
using Safe.EntityFramework.Contexts;
using Safe.Host.Middleware;
using System.Text.Json.Serialization;

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

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
    });

var introspectionSection = builder.Configuration.GetSection("Auth:Introspection");
var authority = introspectionSection["Authority"] ?? throw new InvalidOperationException("Auth:Introspection:Authority must be configured.");
var clientId = introspectionSection["ClientId"] ?? throw new InvalidOperationException("Auth:Introspection:ClientId must be configured.");
var clientSecret = introspectionSection["ClientSecret"];
if (string.IsNullOrWhiteSpace(clientSecret))
{
    if (string.Equals(clientId, "computerclub_api", StringComparison.Ordinal))
    {
        clientSecret = builder.Configuration["OIDC_RESOURCE_SECRET"];
    }
    else if (string.Equals(clientId, "svc.introspector", StringComparison.Ordinal))
    {
        clientSecret = builder.Configuration["OIDC_SVC_INTROSPECTOR_SECRET"];
    }
    else
    {
        clientSecret = builder.Configuration["OIDC_RESOURCE_SECRET"]
                       ?? builder.Configuration["OIDC_SVC_INTROSPECTOR_SECRET"];
    }
}
if (string.IsNullOrWhiteSpace(clientSecret))
{
    throw new InvalidOperationException("Auth:Introspection:ClientSecret must be configured (or set OIDC_RESOURCE_SECRET / OIDC_SVC_INTROSPECTOR_SECRET).");
}
var audiences = introspectionSection.GetSection("Audiences").Get<string[]>();
var audienceList = audiences is { Length: > 0 }
    ? audiences
    : introspectionSection["Audience"]?.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();

builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(authority);
        foreach (var audience in audienceList)
        {
            options.AddAudiences(audience);
        }

        options.UseIntrospection()
            .SetClientId(clientId)
            .SetClientSecret(clientSecret);

        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});

var connectionString = builder.Configuration.GetConnectionString("SafeDb")
                      ?? builder.Configuration.GetConnectionString("SAFEDB")
                      ?? builder.Configuration.GetConnectionString("SAFE_DB");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'SafeDb' is not configured.");
}

builder.Services.AddDbContext<SafeDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddValidatorsFromAssemblyContaining<CreateChangeCommandValidator>();

builder.Services.AddScoped<ISafeService, SafeService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Safe.Read", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("Safe.Write", policy => policy.RequireRole("root", "SafeManager"));
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SafeDbContext>("database");


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAvaSubdomains", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin))
                {
                    return false;
                }

                try
                {
                    var uri = new Uri(origin);
                    var host = uri.Host;
                    return host.Equals("ava-kk.ru", StringComparison.OrdinalIgnoreCase)
                           || host.EndsWith(".ava-kk.ru", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    options.AddPolicy("admin-ui", policy =>
    {
        policy.WithOrigins("https://admin.ava-kk.ru")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SafeDbContext>();
    await db.Database.MigrateAsync();
}

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
