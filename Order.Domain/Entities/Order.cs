namespace Order.Domain.Entities
{
    /// <summary>
    /// The Order row IS the saga state: its Status field tracks progress through the
    /// place-order saga (Pending -> StockReserved -> Confirmed, or -> Cancelled with a reason at
    /// any point). No separate saga-state table is needed for a workflow this size.
    /// </summary>
    public class Order
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public int Quantity { get; set; }

        /// <summary>
        /// Price snapshotted from Catalog.API at order-creation time — orders must not silently
        /// reprice if the catalog price changes later while the order is in flight.
        /// </summary>
        public decimal UnitPriceSnapshot { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public string? CancellationReason { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
