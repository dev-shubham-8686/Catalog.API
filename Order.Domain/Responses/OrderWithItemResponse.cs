using Order.Domain.Entities;

namespace Order.Domain.Responses
{
    /// <summary>
    /// An order enriched with a live read from Catalog.API, for "my orders" style views where the
    /// caller wants to see what they ordered without a separate round-trip per item.
    /// </summary>
    public class OrderWithItemResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid ItemId { get; set; }
        public int Quantity { get; set; }

        /// <summary>Price captured at order-creation time.</summary>
        public decimal UnitPriceSnapshot { get; set; }

        public OrderStatus Status { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>Item name as it exists in the catalog right now (null if the item was deleted since).</summary>
        public string? ItemName { get; set; }

        /// <summary>The catalog's current price for this item, distinct from <see cref="UnitPriceSnapshot"/>.</summary>
        public decimal? CurrentItemPrice { get; set; }
    }
}
