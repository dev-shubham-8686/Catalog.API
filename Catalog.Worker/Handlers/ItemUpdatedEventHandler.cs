using Catalog.Contracts.Caching;
using Catalog.Contracts.Events;
using EventBus;
using StackExchange.Redis;

namespace Catalog.Worker.Handlers
{
    public class ItemUpdatedEventHandler : IIntegrationEventHandler<ItemUpdatedIntegrationEvent>
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<ItemUpdatedEventHandler> _logger;

        public ItemUpdatedEventHandler(IConnectionMultiplexer redis, ILogger<ItemUpdatedEventHandler> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task HandleAsync(ItemUpdatedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Item updated: {ItemId} '{Name}'.",
                @event.ItemId, @event.Name);

            // Invalidate the cached detail view so the next GET repopulates it from the DB.
            // GetAll list pages are deliberately NOT invalidated here — they rely on their own
            // short sliding-expiration TTL (see README's caching section for the rationale).
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(ItemCacheKeys.Get(@event.ItemId));
        }
    }
}
