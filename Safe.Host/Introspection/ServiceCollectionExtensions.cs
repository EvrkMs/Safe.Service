using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Safe.Host.Introspection;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSafeTokenIntrospection(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Auth:Introspection")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<TokenIntrospectionOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations();

        RegisterHttpClient(services);
        return services;
    }

    private static void RegisterHttpClient(IServiceCollection services)
    {
        if (services.Any(sd => sd.ServiceType == typeof(ITokenIntrospector)))
        {
            return;
        }

        services.AddHttpClient<ITokenIntrospector, TokenIntrospector>(static (_, client) =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestVersion = HttpVersion.Version20;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All
            });
    }
}
