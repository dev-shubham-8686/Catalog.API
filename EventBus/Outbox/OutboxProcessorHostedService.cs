using EventBus.Publishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;

namespace EventBus.Outbox
{
    public class OutboxProcessorHostedService<TContext> : BackgroundService where TContext : DbContext
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<OutboxProcessorHostedService<TContext>> _logger;
        private readonly string _instanceId = Guid.NewGuid().ToString("N");

        public OutboxProcessorHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<RabbitMqSettings> settings,
            ILogger<OutboxProcessorHostedService<TContext>> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var startupScope = _scopeFactory.CreateScope())
            {
                var context = startupScope.ServiceProvider.GetRequiredService<TContext>();
                if (context.Model.FindEntityType(typeof(OutboxMessage)) is null)
                {
                    throw new InvalidOperationException(
                        $"{typeof(TContext).Name} does not have OutboxMessage registered in its model. " +
                        "Call modelBuilder.ApplyOutboxConfiguration() from OnModelCreating.");
                }
            }

            var interval = TimeSpan.FromSeconds(Math.Max(1, _settings.DispatchIntervalSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DispatchBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox dispatch cycle failed.");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // shutting down
                }
            }
        }

        private async Task DispatchBatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            var claimed = await ClaimBatchAsync(context, cancellationToken);
            if (claimed.Count == 0)
            {
                return;
            }

            foreach (var message in claimed)
            {
                try
                {
                    await eventBus.PublishAsync(message, cancellationToken);
                    await MarkProcessedAsync(context, message.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish outbox message {MessageId} ({MessageType}).", message.Id, message.Type);
                    await MarkFailedAsync(context, message.Id, ex.Message, cancellationToken);
                }
            }
        }

        private async Task<List<OutboxMessage>> ClaimBatchAsync(TContext context, CancellationToken cancellationToken)
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var schema = OutboxModelBuilderExtensions.DefaultSchema;
            var claimed = new List<OutboxMessage>();

            await using var command = connection.CreateCommand();
            command.CommandText = $@"
UPDATE TOP (@batchSize) o
SET LockedBy = @instanceId, LockedUntilUtc = DATEADD(SECOND, 30, SYSUTCDATETIME())
OUTPUT INSERTED.Id, INSERTED.Type, INSERTED.Content, INSERTED.OccurredOnUtc, INSERTED.RetryCount
FROM [{schema}].[OutboxMessages] AS o WITH (READPAST)
WHERE ProcessedOnUtc IS NULL AND (LockedUntilUtc IS NULL OR LockedUntilUtc < SYSUTCDATETIME())";

            var batchSizeParam = command.CreateParameter();
            batchSizeParam.ParameterName = "@batchSize";
            batchSizeParam.Value = _settings.BatchSize;
            command.Parameters.Add(batchSizeParam);

            var instanceIdParam = command.CreateParameter();
            instanceIdParam.ParameterName = "@instanceId";
            instanceIdParam.Value = _instanceId;
            command.Parameters.Add(instanceIdParam);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                claimed.Add(new OutboxMessage
                {
                    Id = reader.GetGuid(0),
                    Type = reader.GetString(1),
                    Content = reader.GetString(2),
                    OccurredOnUtc = reader.GetDateTime(3),
                    RetryCount = reader.GetInt32(4)
                });
            }

            return claimed;
        }

        private static async Task MarkProcessedAsync(TContext context, Guid id, CancellationToken cancellationToken)
        {
            await ExecuteUpdateAsync(
                context,
                "UPDATE [{0}].[OutboxMessages] SET ProcessedOnUtc = SYSUTCDATETIME(), LockedBy = NULL, LockedUntilUtc = NULL WHERE Id = @id",
                id,
                cancellationToken);
        }

        private static async Task MarkFailedAsync(TContext context, Guid id, string error, CancellationToken cancellationToken)
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var schema = OutboxModelBuilderExtensions.DefaultSchema;

            await using var command = connection.CreateCommand();
            command.CommandText =
                $"UPDATE [{schema}].[OutboxMessages] SET RetryCount = RetryCount + 1, Error = @error, LockedBy = NULL, LockedUntilUtc = NULL WHERE Id = @id";

            var idParam = command.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = id;
            command.Parameters.Add(idParam);

            var errorParam = command.CreateParameter();
            errorParam.ParameterName = "@error";
            errorParam.Value = error.Length > 2000 ? error[..2000] : error;
            command.Parameters.Add(errorParam);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task ExecuteUpdateAsync(TContext context, string sqlTemplate, Guid id, CancellationToken cancellationToken)
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var schema = OutboxModelBuilderExtensions.DefaultSchema;

            await using var command = connection.CreateCommand();
            command.CommandText = string.Format(sqlTemplate, schema);

            var idParam = command.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = id;
            command.Parameters.Add(idParam);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
