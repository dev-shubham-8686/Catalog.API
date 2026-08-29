using Catalog.Contracts.Events;
using EventBus;

namespace Catalog.Worker.Handlers
{
    public class ItemUpdatedEventHandler : IIntegrationEventHandler<ItemUpdatedIntegrationEvent>
    {
        private readonly ILogger<ItemUpdatedEventHandler> _logger;

        public ItemUpdatedEventHandler(ILogger<ItemUpdatedEventHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(ItemUpdatedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Item updated: {ItemId} '{Name}'.",
                @event.ItemId, @event.Name);

            // Example real-world hook: invalidate the Redis cache entry for this item so the
            // next read repopulates it from the DB. Not implemented — caching is out of scope
            // for this pass, see the Roadmap in the event-bus plan.

            return Task.CompletedTask;
        }
    }
}
