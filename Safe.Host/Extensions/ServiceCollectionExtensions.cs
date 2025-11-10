using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using Safe.Application.Factories;
using Safe.Application.Handlers;
using Safe.Application.Repositories;
using Safe.Application.Services;
using Safe.EntityFramework;
using Safe.EntityFramework.Contexts;
using Safe.EntityFramework.Repositories;
using Safe.Host.Filters;
using Safe.Host.Middleware;
using Safe.Host.Revocation;
using static Safe.Domain.Commands.SafeCommand;

namespace Safe.Host.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSafeControllers(this IServiceCollection services)
    {
        services.AddControllers(options =>
            {
                options.Filters.Add<SafeApiExceptionFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(SafeJsonOptions.EnumConverter);
            });

        return services;
    }

    public static IServiceCollection AddSafeDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SafeDb")
                              ?? configuration.GetConnectionString("SAFEDB")
                              ?? configuration.GetConnectionString("SAFE_DB");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'SafeDb' is not configured.");
        }

        services.AddDbContext<SafeDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }

    public static IServiceCollection AddSafeAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var introspectionSection = configuration.GetSection("Auth:Introspection");
        var authority = introspectionSection["Authority"]
                        ?? throw new InvalidOperationException("Auth:Introspection:Authority must be configured.");
        var clientId = introspectionSection["ClientId"]
                       ?? throw new InvalidOperationException("Auth:Introspection:ClientId must be configured.");

        var clientSecret = ResolveClientSecret(configuration, introspectionSection, clientId);
        var audiences = introspectionSection.GetSection("Audiences").Get<string[]>();
        var audienceList = audiences is { Length: > 0 }
            ? audiences
            : introspectionSection["Audience"]
                ?.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
              ?? Array.Empty<string>();

        services.AddOpenIddict()
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

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        return services;
    }

    public static IServiceCollection AddSafeAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Safe.Read", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("Safe.Write", policy => policy.RequireRole("Root", "SafeManager"));
        });

        return services;
    }

    public static IServiceCollection AddSafeCors(this IServiceCollection services)
    {
        services.AddCors(options =>
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

        return services;
    }

    public static IServiceCollection AddSafeHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<SafeDbContext>("database");

        return services;
    }

    public static IServiceCollection AddSafeApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        services.AddScoped<SafeChangeFactory>();
        services.AddScoped<CreateSafeChangeHandler>();
        services.AddScoped<ReverseSafeChangeHandler>();
        services.AddScoped<GetBalanceQueryHandler>();
        services.AddScoped<GetSafeChangeDetailsHandler>();
        services.AddScoped<GetSafeChangesPageHandler>();

        services.AddScoped<ISafeChangeReadRepository, SafeChangeRepository>();
        services.AddScoped<ISafeChangeWriteRepository, SafeChangeRepository>();

        services.AddScoped<ISafeService, SafeService>();
        services.AddHttpContextAccessor();

        services.AddValidatorsFromAssemblyContaining<CreateChangeCommandValidator>();

        services.Configure<RedisRevocationOptions>(configuration.GetSection("Redis"));
        services.AddSingleton<IRevokedTokenCache, RevokedTokenCache>();
        services.AddHostedService<RedisRevocationListener>();

        services.Configure<RequestLoggingOptions>(options =>
        {
            var includeTokens = configuration.GetValue("SAFE_LOG_TOKENS", false);
            options.IncludeAuthorizationToken = includeTokens;
        });

        return services;
    }

    private static string ResolveClientSecret(
        IConfiguration configuration,
        IConfigurationSection introspectionSection,
        string clientId)
    {
        var clientSecret = introspectionSection["ClientSecret"];
        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            return clientSecret;
        }

        if (string.Equals(clientId, "computerclub_api", StringComparison.Ordinal))
        {
            return configuration["OIDC_RESOURCE_SECRET"]
                   ?? throw new InvalidOperationException("OIDC_RESOURCE_SECRET is not configured.");
        }

        if (string.Equals(clientId, "svc.introspector", StringComparison.Ordinal))
        {
            return configuration["OIDC_SVC_INTROSPECTOR_SECRET"]
                   ?? throw new InvalidOperationException("OIDC_SVC_INTROSPECTOR_SECRET is not configured.");
        }

        var fallback = configuration["OIDC_RESOURCE_SECRET"]
                       ?? configuration["OIDC_SVC_INTROSPECTOR_SECRET"];

        return fallback ?? throw new InvalidOperationException("Auth:Introspection:ClientSecret must be configured.");
    }
}
