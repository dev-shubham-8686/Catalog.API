using Catalog.Contracts.Events;
using EventBus;

namespace Catalog.Worker.Handlers
{
    public class ItemDeletedEventHandler : IIntegrationEventHandler<ItemDeletedIntegrationEvent>
    {
        private readonly ILogger<ItemDeletedEventHandler> _logger;

        public ItemDeletedEventHandler(ILogger<ItemDeletedEventHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(ItemDeletedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Item deleted: {ItemId}.", @event.ItemId);

            // Example real-world hook: remove the item from the Redis cache and search index.
            // Not implemented — caching/search are out of scope for this pass.

            return Task.CompletedTask;
        }
    }
}
