using Microsoft.EntityFrameworkCore;
using Order.Domain.Repositories;

namespace Order.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly OrderDbContext _context;

        public OrderRepository(OrderDbContext context)
        {
            _context = context;
        }

        public async Task<Domain.Entities.Order> AddAsync(Domain.Entities.Order order, CancellationToken cancellationToken = default)
        {
            var entry = await _context.Orders.AddAsync(order, cancellationToken);
            return entry.Entity;
        }

        public async Task<Domain.Entities.Order?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Domain.Entities.Order>> GetAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Domain.Entities.Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
