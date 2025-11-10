using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Safe.Host.Revocation;

public sealed class RedisRevocationListener(
    IOptions<RedisRevocationOptions> options,
    IRevokedTokenCache revokedTokenCache,
    ILogger<RedisRevocationListener> logger)
    : BackgroundService
{
    private readonly RedisRevocationOptions _options = options.Value;
    private readonly IRevokedTokenCache _revokedTokenCache = revokedTokenCache;
    private readonly ILogger<RedisRevocationListener> _logger = logger;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private IConnectionMultiplexer? _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("Redis:ConnectionString must be configured.");
        }

        _connection = await ConnectionMultiplexer.ConnectAsync(_options.ConnectionString);
        var subscriber = _connection.GetSubscriber();
        var channelName = string.IsNullOrWhiteSpace(_options.RevocationChannel)
            ? "revoked_tokens"
            : _options.RevocationChannel;
        var channel = RedisChannel.Literal(channelName);

        var queue = await subscriber.SubscribeAsync(channel);
        _logger.LogInformation("Subscribed to Redis channel {Channel} for token revocations.", channelName);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ChannelMessage message;
                try
                {
                    message = await queue.ReadAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                ProcessMessage(message);
            }
        }
        finally
        {
            await subscriber.UnsubscribeAsync(channel);
        }
    }

    private void ProcessMessage(ChannelMessage message)
    {
        try
        {
            var payload = message.Message.ToString();
            var trimmed = payload.TrimStart();
            if (string.IsNullOrWhiteSpace(payload) || trimmed.Length == 0 || trimmed[0] != '[')
            {
                _logger.LogDebug("Ignoring non-revocation payload: {Payload}", payload);
                return;
            }

            var notifications = JsonSerializer.Deserialize<RevocationNotification[]>(payload, _serializerOptions);
            if (notifications is null || notifications.Length == 0)
            {
                return;
            }

            var ttl = TimeSpan.FromSeconds(Math.Max(60, _options.RevocationEntryTtlSeconds));
            foreach (var notification in notifications)
            {
                if (!string.IsNullOrWhiteSpace(notification.TokenId))
                {
                    _revokedTokenCache.MarkToken(notification.TokenId!, ttl);
                }

                if (!string.IsNullOrWhiteSpace(notification.SessionReferenceId) &&
                    (notification.TokenCount.HasValue || string.IsNullOrWhiteSpace(notification.TokenId)))
                {
                    _revokedTokenCache.MarkSession(notification.SessionReferenceId!, ttl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process revocation message: {Payload}", message.Message.ToString());
        }
    }

    public override void Dispose()
    {
        _connection?.Dispose();
        base.Dispose();
    }
}
