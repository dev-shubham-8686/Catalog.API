using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Domain.Repositories
{
    public interface IOrderRepository
    {
        Task<OrderEntity> AddAsync(OrderEntity order, CancellationToken cancellationToken = default);
        Task<OrderEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<OrderEntity>> GetAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<OrderEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
