using EventBus.Idempotency;
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

            base.OnModelCreating(modelBuilder);
        }
    }
}
