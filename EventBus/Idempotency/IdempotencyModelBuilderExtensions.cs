using Microsoft.EntityFrameworkCore;

namespace EventBus.Idempotency
{
    public static class IdempotencyModelBuilderExtensions
    {
        public const string DefaultSchema = "eventbus";

        public static ModelBuilder ApplyIdempotencyConfiguration(this ModelBuilder modelBuilder, string schema = DefaultSchema)
        {
            modelBuilder.Entity<ProcessedEvent>(builder =>
            {
                builder.ToTable("ProcessedEvents", schema);
                builder.HasKey(p => p.EventId);
                builder.Property(p => p.EventType).IsRequired().HasMaxLength(500);
            });

            return modelBuilder;
        }
    }
}
