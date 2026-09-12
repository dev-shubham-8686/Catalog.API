using Order.Domain.Requests;
using Order.Domain.Responses;

namespace Order.Domain.Services
{
    public interface IOrderService
    {
        Task<CreateOrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);
        Task<OrderResponse?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<OrderResponse>> GetOrdersAsync(CancellationToken cancellationToken = default);
    }
}
