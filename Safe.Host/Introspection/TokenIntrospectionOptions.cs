using System;
using System.ComponentModel.DataAnnotations;

namespace Safe.Host.Introspection;

internal sealed class TokenIntrospectionOptions
{
    [Url]
    public string Authority { get; set; } = "https://auth.ava-kk.ru";

    public string IntrospectionEndpoint { get; set; } = "/connect/introspect";

    [Required]
    public string ClientId { get; set; } = "svc.introspector";

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    public string TokenTypeHint { get; set; } = "access_token";

    public Uri ResolveEndpoint()
    {
        if (Uri.TryCreate(IntrospectionEndpoint, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        if (string.IsNullOrWhiteSpace(Authority))
        {
            throw new InvalidOperationException("Authority must be set when IntrospectionEndpoint is relative.");
        }

        if (!Uri.TryCreate(Authority, UriKind.Absolute, out var authorityUri))
        {
            throw new InvalidOperationException("Authority must be an absolute URI.");
        }

        return new Uri(authorityUri, IntrospectionEndpoint);
    }
}
