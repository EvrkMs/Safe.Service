using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Safe.Application.Services;
using Safe.EntityFramework;
using Safe.EntityFramework.Contexts;
using Safe.Host.Authentication;
using Safe.Host.Introspection;
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

builder.Services.AddSafeTokenIntrospection(builder.Configuration);
var fallbackIntrospectionSecret = builder.Configuration["Auth:Introspection:ClientSecret"]
    ?? builder.Configuration["OIDC_SVC_INTROSPECTOR_SECRET"];
builder.Services.PostConfigure<TokenIntrospectionOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.ClientSecret) && !string.IsNullOrWhiteSpace(fallbackIntrospectionSecret))
    {
        options.ClientSecret = fallbackIntrospectionSecret;
    }
    if (string.IsNullOrWhiteSpace(options.ClientSecret))
    {
        throw new InvalidOperationException("Auth:Introspection:ClientSecret must be configured.");
    }
});
builder.Services.AddMemoryCache();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Introspection";
        options.DefaultChallengeScheme = "Introspection";
    })
    .AddScheme<AuthenticationSchemeOptions, IntrospectionAuthenticationHandler>("Introspection", _ => { });

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
    options.AddPolicy(name: "admin-ui",
                      policy =>
                      {
                          policy.WithOrigins("https://admin.ava-kk.ru") // Replace with your allowed origins
                                .AllowAnyHeader() // Allows any header in the request
                                .AllowCredentials()
                                .AllowAnyMethod(); // Allows any HTTP method (GET, POST, PUT, DELETE, etc.)
                      });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SafeDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors("AllowAvaSubdomains");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
