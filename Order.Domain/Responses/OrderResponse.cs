using Order.Domain.Entities;

namespace Order.Domain.Responses
{
    public class OrderResponse
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPriceSnapshot { get; set; }
        public OrderStatus Status { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
