using EventBus;

namespace Catalog.Contracts.Events
{
    public record ItemUpdatedIntegrationEvent : IntegrationEvent
    {
        public Guid ItemId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
    }
}
