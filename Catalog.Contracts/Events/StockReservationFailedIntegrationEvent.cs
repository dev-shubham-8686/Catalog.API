using EventBus;

namespace Catalog.Contracts.Events
{
    public record StockReservationFailedIntegrationEvent : IntegrationEvent
    {
        public Guid OrderId { get; init; }
        public Guid ItemId { get; init; }
        public int Quantity { get; init; }
        public string Reason { get; init; } = string.Empty;
    }
}
