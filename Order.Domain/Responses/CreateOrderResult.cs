namespace Order.Domain.Responses
{
    public class CreateOrderResult
    {
        public bool Succeeded { get; init; }
        public OrderResponse? Order { get; init; }
        public string? Error { get; init; }

        public static CreateOrderResult Success(OrderResponse order) => new() { Succeeded = true, Order = order };
        public static CreateOrderResult Failure(string error) => new() { Succeeded = false, Error = error };
    }
}
