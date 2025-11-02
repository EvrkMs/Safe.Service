using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Safe.Host.Introspection;

internal sealed class TokenIntrospector : ITokenIntrospector
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<TokenIntrospectionOptions> _optionsMonitor;
    private readonly ILogger<TokenIntrospector> _logger;

    public TokenIntrospector(
        HttpClient httpClient,
        IOptionsMonitor<TokenIntrospectionOptions> optionsMonitor,
        ILogger<TokenIntrospector> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<TokenIntrospectionResult> IntrospectAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token must be provided.", nameof(token));
        }

        var options = _optionsMonitor.CurrentValue ?? throw new InvalidOperationException("Token introspection options are not configured.");
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException("ClientId и ClientSecret должны быть указаны для интроспекции токена.");
        }

        var endpoint = options.ResolveEndpoint();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

        var payload = new Dictionary<string, string>
        {
            ["token"] = token
        };

        if (!string.IsNullOrWhiteSpace(options.TokenTypeHint))
        {
            payload["token_type_hint"] = options.TokenTypeHint;
        }

        request.Content = new FormUrlEncodedContent(payload);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = CreateBasicAuthHeader(options.ClientId, options.ClientSecret);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("Интроспекция отклонена (401 Unauthorized). Проверьте учетные данные клиента.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException("Интроспекция отклонена (403 Forbidden). Проверьте права клиента.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadSnippetAsync(response, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"Интроспекция завершилась с ошибкой {(int)response.StatusCode} ({response.ReasonPhrase}). Фрагмент ответа: {body}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var parsed = await JsonSerializer.DeserializeAsync<IntrospectionResponse>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            if (parsed is null)
            {
                throw new InvalidOperationException("Ответ интроспекции пуст.");
            }

            return Map(parsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Ошибка при обращении к эндпоинту интроспекции {Endpoint}", endpoint);
            throw new InvalidOperationException($"Не удалось связаться с эндпоинтом интроспекции {endpoint}", ex);
        }
    }

    private static AuthenticationHeaderValue CreateBasicAuthHeader(string clientId, string clientSecret)
    {
        var raw = $"{clientId}:{clientSecret}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        return new AuthenticationHeaderValue("Basic", encoded);
    }

    private static async Task<string> ReadSnippetAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        const int limit = 256;
        return text.Length <= limit ? text : text[..limit] + "...";
    }

    private static TokenIntrospectionResult Map(IntrospectionResponse response)
    {
        var scopes = string.IsNullOrWhiteSpace(response.Scope)
            ? Array.Empty<string>()
            : response.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var audiences = ExtractAudiences(response.Audience);

        return new TokenIntrospectionResult
        {
            Active = response.Active,
            Subject = response.Subject,
            ClientId = response.ClientId,
            Username = response.Username,
            Issuer = response.Issuer,
            TokenType = response.TokenType,
            TokenId = response.TokenId,
            ExpiresAt = ToDateTime(response.ExpiresAtEpoch),
            IssuedAt = ToDateTime(response.IssuedAtEpoch),
            NotBefore = ToDateTime(response.NotBeforeEpoch),
            Scopes = scopes,
            Audiences = audiences,
            Raw = response.AdditionalData
        };
    }

    private static DateTimeOffset? ToDateTime(long? epochSeconds)
    {
        if (epochSeconds is null)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(epochSeconds.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ExtractAudiences(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    list.Add(item.GetString()!);
                }
            }
            return list;
        }

        if (element.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(element.GetString()))
        {
            return new[] { element.GetString()! };
        }

        return Array.Empty<string>();
    }

    private sealed class IntrospectionResponse
    {
        [JsonPropertyName("active")]
        public bool Active { get; init; }

        [JsonPropertyName("scope")]
        public string? Scope { get; init; }

        [JsonPropertyName("client_id")]
        public string? ClientId { get; init; }

        [JsonPropertyName("username")]
        public string? Username { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        [JsonPropertyName("exp")]
        public long? ExpiresAtEpoch { get; init; }

        [JsonPropertyName("iat")]
        public long? IssuedAtEpoch { get; init; }

        [JsonPropertyName("nbf")]
        public long? NotBeforeEpoch { get; init; }

        [JsonPropertyName("sub")]
        public string? Subject { get; init; }

        [JsonPropertyName("aud")]
        public JsonElement Audience { get; init; }

        [JsonPropertyName("iss")]
        public string? Issuer { get; init; }

        [JsonPropertyName("jti")]
        public string? TokenId { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalData { get; init; } = new(StringComparer.Ordinal);
    }
}
