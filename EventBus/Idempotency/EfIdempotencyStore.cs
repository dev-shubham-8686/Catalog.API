using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EventBus.Idempotency
{
    public class EfIdempotencyStore<TContext> : IIdempotencyStore where TContext : DbContext
    {
        private const int UniqueConstraintViolation = 2627;
        private const int UniqueIndexViolation = 2601;

        private readonly TContext _context;

        public EfIdempotencyStore(TContext context)
        {
            _context = context;
        }

        public async Task<bool> TryMarkProcessedAsync(Guid eventId, string eventType, CancellationToken cancellationToken = default)
        {
            _context.Set<ProcessedEvent>().Add(new ProcessedEvent
            {
                EventId = eventId,
                EventType = eventType,
                ProcessedOnUtc = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Already processed by this or another consumer instance.
                _context.ChangeTracker.Clear();
                return false;
            }
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sqlEx &&
                   sqlEx.Errors.Cast<SqlError>().Any(e => e.Number is UniqueConstraintViolation or UniqueIndexViolation);
        }
    }
}
