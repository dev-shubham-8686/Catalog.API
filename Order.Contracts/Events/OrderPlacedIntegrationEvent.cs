using EventBus;

namespace Order.Contracts.Events
{
    /// <summary>
    /// Published by Order.Api once an order is durably recorded as Pending. Consumed by
    /// Catalog.Worker, which attempts to reserve stock and replies with
    /// StockReservedIntegrationEvent / StockReservationFailedIntegrationEvent.
    /// </summary>
    public record OrderPlacedIntegrationEvent : IntegrationEvent
    {
        public Guid OrderId { get; init; }
        public Guid ItemId { get; init; }
        public int Quantity { get; init; }
    }
}
