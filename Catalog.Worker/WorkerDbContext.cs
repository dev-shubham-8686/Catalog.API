using EventBus.Idempotency;
using EventBus.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Worker
{
    public class WorkerDbContext : DbContext
    {
        public WorkerDbContext(DbContextOptions<WorkerDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyIdempotencyConfiguration();

            // Producer side: Catalog.Worker now also publishes StockReserved/StockReservationFailed
            // in reply to OrderPlacedIntegrationEvent, so it needs its own outbox table too.
            modelBuilder.ApplyOutboxConfiguration();

            base.OnModelCreating(modelBuilder);
        }
    }
}
