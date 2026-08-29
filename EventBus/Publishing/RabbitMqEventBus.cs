using EventBus.Outbox;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;

namespace EventBus.Publishing
{
    public sealed class RabbitMqEventBus : IEventBus, IAsyncDisposable
    {
        private readonly RabbitMqSettings _settings;
        private readonly ConnectionFactory _factory;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private IConnection? _connection;
        private IChannel? _channel;

        public RabbitMqEventBus(IOptions<RabbitMqSettings> settings)
        {
            _settings = settings.Value;
            _factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                UserName = _settings.User,
                Password = _settings.Password
            };
        }

        public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            var channel = await GetChannelAsync(cancellationToken);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = message.Id.ToString(),
                Type = message.Type,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await channel.BasicPublishAsync(
                exchange: _settings.ExchangeName,
                routingKey: message.Type,
                mandatory: false,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message.Content),
                cancellationToken: cancellationToken);
        }

        private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_channel is { IsOpen: true })
                {
                    return _channel;
                }

                if (_connection is not { IsOpen: true })
                {
                    _connection?.Dispose();
                    _connection = await _factory.CreateConnectionAsync(cancellationToken);
                }

                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);
                _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);
                await _channel.ExchangeDeclareAsync(
                    _settings.ExchangeName,
                    ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: cancellationToken);

                return _channel;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
            }

            _connection?.Dispose();
            _lock.Dispose();
        }
    }
}
