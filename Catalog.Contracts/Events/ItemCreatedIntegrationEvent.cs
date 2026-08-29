using EventBus;

namespace Catalog.Contracts.Events
{
    public record ItemCreatedIntegrationEvent : IntegrationEvent
    {
        public Guid ItemId { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal? Price { get; init; }
    }
}
