using EventBus;

namespace Catalog.Contracts.Events
{
    public record StockReservedIntegrationEvent : IntegrationEvent
    {
        public Guid OrderId { get; init; }
        public Guid ItemId { get; init; }
        public int Quantity { get; init; }
    }
}
