namespace Order.Domain.Requests
{
    public class CreateOrderRequest
    {
        public Guid ItemId { get; set; }
        public int Quantity { get; set; }
    }
}
