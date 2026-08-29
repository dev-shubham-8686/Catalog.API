using Microsoft.EntityFrameworkCore;

namespace EventBus.Outbox
{
    public static class OutboxModelBuilderExtensions
    {
        public const string DefaultSchema = "eventbus";

        public static ModelBuilder ApplyOutboxConfiguration(this ModelBuilder modelBuilder, string schema = DefaultSchema)
        {
            modelBuilder.Entity<OutboxMessage>(builder =>
            {
                builder.ToTable("OutboxMessages", schema);
                builder.HasKey(m => m.Id);
                builder.Property(m => m.Type).IsRequired().HasMaxLength(500);
                builder.Property(m => m.Content).IsRequired();
                builder.Property(m => m.LockedBy).HasMaxLength(100);
                builder.HasIndex(m => m.ProcessedOnUtc);
            });

            return modelBuilder;
        }
    }
}
