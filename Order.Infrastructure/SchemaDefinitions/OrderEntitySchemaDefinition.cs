using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.Entities;

namespace Order.Infrastructure.SchemaDefinitions
{
    public class OrderEntitySchemaDefinition : IEntityTypeConfiguration<Order.Domain.Entities.Order>
    {
        public void Configure(EntityTypeBuilder<Order.Domain.Entities.Order> builder)
        {
            builder.ToTable("Orders", OrderDbContext.DEFAULT_SCHEMA);
            builder.HasKey(o => o.Id);
            builder.Property(o => o.UnitPriceSnapshot).HasColumnType("decimal(18,2)");
            builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(o => o.CancellationReason).HasMaxLength(500);
        }
    }
}
