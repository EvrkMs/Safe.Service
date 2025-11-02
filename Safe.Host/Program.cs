using Auth.TokenValidation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Safe.Application.Services;
using Safe.EntityFramework;
using Safe.EntityFramework.Contexts;
using Safe.Host;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(o =>
{
    o.ListenAnyIP(5001, listen =>
    {
        listen.UseHttps(https =>
        {
            https.ServerCertificate = EphemeralCert.Create();
        });
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
    });

builder.Services.AddAuthTokenIntrospection(options =>
{
    options.Authority = "https://auth.ava-kk.ru";
    options.ClientId = "svc.introspector";
    options.ClientSecret = Environment.GetEnvironmentVariable("OIDC_SVC_INTROSPECTOR_SECRET")!;
});

builder.Services.AddDbContext<SafeDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("SAFE_DB")));

// FluentValidation - нужен пакет FluentValidation.DependencyInjectionExtensions
builder.Services.AddValidatorsFromAssemblyContaining<CreateChangeCommandValidator>();

builder.Services.AddScoped<ISafeService, SafeService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Safe.Read", p => p.RequireAuthenticatedUser());
    options.AddPolicy("Safe.Write", p => p.RequireRole("root", "SafeManager"));
});

// Health checks - нужен пакет Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SafeDbContext>("database");

var app = builder.Build();

app.UseMiddleware<SampleMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SafeDbContext>();
    await db.Database.MigrateAsync();
}

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();