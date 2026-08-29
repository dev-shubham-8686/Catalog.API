using Catalog.Contracts.Events;
using EventBus;

namespace Catalog.Worker.Handlers
{
    public class ItemCreatedEventHandler : IIntegrationEventHandler<ItemCreatedIntegrationEvent>
    {
        private readonly ILogger<ItemCreatedEventHandler> _logger;

        public ItemCreatedEventHandler(ILogger<ItemCreatedEventHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(ItemCreatedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Item created: {ItemId} '{Name}' (Price={Price}).",
                @event.ItemId, @event.Name, @event.Price);

            // Example real-world hooks a production handler would perform here:
            //   - invalidate/populate the Redis cache entry for this item
            //   - upsert the item into a search index (e.g. Elasticsearch)
            // Deliberately not implemented — caching/search are out of scope for this pass.

            return Task.CompletedTask;
        }
    }
}
