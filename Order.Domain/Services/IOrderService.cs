using Order.Domain.Requests;
using Order.Domain.Responses;

namespace Order.Domain.Services
{
    public interface IOrderService
    {
        Task<CreateOrderResult> CreateOrderAsync(CreateOrderRequest request, Guid userId, CancellationToken cancellationToken = default);
        Task<OrderResponse?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<OrderResponse>> GetOrdersAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<OrderWithItemResponse>> GetOrdersForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
