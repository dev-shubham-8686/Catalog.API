using EventBus.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EventBus.Consuming
{
    public class RabbitMqConsumerHostedService : BackgroundService
    {
        private const string RetryCountHeader = "x-retry-count";

        private readonly EventBusSubscriptionManager _subscriptions;
        private readonly RabbitMqSettings _settings;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RabbitMqConsumerHostedService> _logger;

        private IConnection? _connection;
        private IChannel? _channel;

        public RabbitMqConsumerHostedService(
            EventBusSubscriptionManager subscriptions,
            IOptions<RabbitMqSettings> settings,
            IServiceScopeFactory scopeFactory,
            ILogger<RabbitMqConsumerHostedService> logger)
        {
            _subscriptions = subscriptions;
            _settings = settings.Value;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                UserName = _settings.User,
                Password = _settings.Password
            };

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.ExchangeDeclareAsync(_settings.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.BasicQosAsync(0, (ushort)_settings.PrefetchCount, false, stoppingToken);

            var dlxName = $"{_settings.ServiceName}.dlx";
            await _channel.ExchangeDeclareAsync(dlxName, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: stoppingToken);

            foreach (var subscription in _subscriptions.Subscriptions)
            {
                await DeclareTopologyAsync(subscription.EventType.Name, dlxName, stoppingToken);
            }

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, delivery) => await OnMessageReceivedAsync(delivery, stoppingToken);

            foreach (var subscription in _subscriptions.Subscriptions)
            {
                var queueName = QueueName(subscription.EventType.Name);
                await _channel.BasicConsumeAsync(
                    queue: queueName,
                    autoAck: false,
                    consumerTag: string.Empty,
                    noLocal: false,
                    exclusive: false,
                    arguments: null!,
                    consumer: consumer,
                    cancellationToken: stoppingToken);
                _logger.LogInformation("Listening for {EventType} on queue {Queue}.", subscription.EventType.Name, queueName);
            }

            await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        }

        private string QueueName(string eventTypeName) => $"{_settings.ServiceName}.{eventTypeName}";

        private async Task DeclareTopologyAsync(string eventTypeName, string dlxName, CancellationToken cancellationToken)
        {
            if (_channel is null)
            {
                return;
            }

            var queueName = QueueName(eventTypeName);
            var dlqName = $"{queueName}.dlq";

            await _channel.QueueDeclareAsync(dlqName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
            await _channel.QueueBindAsync(dlqName, dlxName, queueName, cancellationToken: cancellationToken);

            var mainQueueArgs = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = dlxName,
                ["x-dead-letter-routing-key"] = queueName
            };
            await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: mainQueueArgs, cancellationToken: cancellationToken);
            await _channel.QueueBindAsync(queueName, _settings.ExchangeName, eventTypeName, cancellationToken: cancellationToken);

            foreach (var (tierName, ttlMs) in RetryTiers)
            {
                var tierQueueName = $"{queueName}.retry.{tierName}";
                var tierArgs = new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = ttlMs,
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = queueName
                };
                await _channel.QueueDeclareAsync(tierQueueName, durable: true, exclusive: false, autoDelete: false, arguments: tierArgs, cancellationToken: cancellationToken);
            }
        }

        private static readonly (string Name, int TtlMs)[] RetryTiers =
        {
            ("5s", 5_000),
            ("30s", 30_000),
            ("2m", 120_000)
        };

        private async Task OnMessageReceivedAsync(BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
        {
            if (_channel is null)
            {
                return;
            }

            var eventTypeName = delivery.BasicProperties.Type ?? string.Empty;

            if (!_subscriptions.TryGetSubscription(eventTypeName, out var subscription))
            {
                _logger.LogWarning("No handler registered for event type {EventType}; dropping message.", eventTypeName);
                await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();

                var eventId = Guid.Parse(delivery.BasicProperties.MessageId!);
                var idempotencyStore = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

                var alreadyProcessed = !await idempotencyStore.TryMarkProcessedAsync(eventId, eventTypeName, stoppingToken);
                if (alreadyProcessed)
                {
                    _logger.LogInformation("Event {EventId} ({EventType}) already processed, skipping.", eventId, eventTypeName);
                    await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    return;
                }

                var json = Encoding.UTF8.GetString(delivery.Body.Span);
                var @event = JsonSerializer.Deserialize(json, subscription.EventType)
                    ?? throw new InvalidOperationException($"Could not deserialize message body to {subscription.EventType.Name}.");

                var handler = scope.ServiceProvider.GetRequiredService(subscription.HandlerType);
                var method = subscription.HandlerType.GetMethod("HandleAsync")!;
                await (Task)method.Invoke(handler, new[] { @event, stoppingToken })!;

                await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {EventType}; routing to retry/DLQ.", eventTypeName);
                await HandleFailureAsync(delivery, eventTypeName, stoppingToken);
            }
        }

        private async Task HandleFailureAsync(BasicDeliverEventArgs delivery, string eventTypeName, CancellationToken stoppingToken)
        {
            if (_channel is null)
            {
                return;
            }

            var retryCount = GetRetryCount(delivery) + 1;

            if (retryCount > _settings.MaxRetryCount)
            {
                _logger.LogWarning("Event {EventType} exceeded max retry count, dead-lettering.", eventTypeName);
                await _channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                return;
            }

            var tier = retryCount switch
            {
                <= 2 => RetryTiers[0],
                <= 4 => RetryTiers[1],
                _ => RetryTiers[2]
            };

            var queueName = QueueName(eventTypeName);
            var tierQueueName = $"{queueName}.retry.{tier.Name}";

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = delivery.BasicProperties.ContentType,
                MessageId = delivery.BasicProperties.MessageId,
                Type = delivery.BasicProperties.Type,
                Headers = new Dictionary<string, object?> { [RetryCountHeader] = retryCount }
            };

            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: tierQueueName,
                mandatory: false,
                basicProperties: properties,
                body: delivery.Body,
                cancellationToken: stoppingToken);

            await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
        }

        private static int GetRetryCount(BasicDeliverEventArgs delivery)
        {
            if (delivery.BasicProperties.Headers is { } headers &&
                headers.TryGetValue(RetryCountHeader, out var value))
            {
                return value switch
                {
                    int i => i,
                    byte[] bytes => int.Parse(Encoding.UTF8.GetString(bytes)),
                    _ => 0
                };
            }

            return 0;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync(cancellationToken);
            }

            _connection?.Dispose();

            await base.StopAsync(cancellationToken);
        }
    }
}
