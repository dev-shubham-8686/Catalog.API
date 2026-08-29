using EventBus;

namespace Catalog.Contracts.Events
{
    public record ItemDeletedIntegrationEvent : IntegrationEvent
    {
        public Guid ItemId { get; init; }
    }
}
