using EventBus;

namespace Order.Contracts.Events
{
    /// <summary>
    /// Compensating event: published by Order.Worker's saga orchestrator only when stock was
    /// already reserved but a later saga step (simulated payment) failed. Consumed by
    /// Catalog.Worker, which releases the reservation back onto AvailableStock.
    /// </summary>
    public record ReleaseStockIntegrationEvent : IntegrationEvent
    {
        public Guid OrderId { get; init; }
        public Guid ItemId { get; init; }
        public int Quantity { get; init; }
    }
}
