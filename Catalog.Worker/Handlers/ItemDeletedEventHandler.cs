using Catalog.Contracts.Caching;
using Catalog.Contracts.Events;
using EventBus;
using StackExchange.Redis;

namespace Catalog.Worker.Handlers
{
    public class ItemDeletedEventHandler : IIntegrationEventHandler<ItemDeletedIntegrationEvent>
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<ItemDeletedEventHandler> _logger;

        public ItemDeletedEventHandler(IConnectionMultiplexer redis, ILogger<ItemDeletedEventHandler> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task HandleAsync(ItemDeletedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Item deleted: {ItemId}.", @event.ItemId);

            // Example real-world hook: also remove the item from a search index (not implemented,
            // no search index exists in this project yet).
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(ItemCacheKeys.Get(@event.ItemId));
        }
    }
}
