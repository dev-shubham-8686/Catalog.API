using EventBus.Idempotency;
using EventBus.Outbox;
using Microsoft.EntityFrameworkCore;
using Order.Infrastructure.SchemaDefinitions;

namespace Order.Infrastructure
{
    /// <summary>
    /// Backs both Order.Api (producer: writes Order rows + outbox) and Order.Worker (consumer:
    /// updates Order.Status as the saga progresses + idempotency + outbox for the compensating
    /// ReleaseStockIntegrationEvent) — both processes talk to the same "orders_db" database, which
    /// is physically separate from Catalog's database (a real bounded-context boundary, not just a
    /// table-prefix convention).
    /// </summary>
    public class OrderDbContext : DbContext
    {
        public const string DEFAULT_SCHEMA = "orders";

        public DbSet<Order.Domain.Entities.Order> Orders { get; set; } = null!;

        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new OrderEntitySchemaDefinition());
            modelBuilder.ApplyOutboxConfiguration();
            modelBuilder.ApplyIdempotencyConfiguration();

            base.OnModelCreating(modelBuilder);
        }
    }
}
