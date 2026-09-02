using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Infrastructure.SchemaDefinitions
{
    public class ItemEntitySchemaDefinition : IEntityTypeConfiguration<Item>
    {
        public void Configure(EntityTypeBuilder<Item> builder)
        {
            builder.ToTable("Items", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(k => k.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(450); // SQL Server's max nonclustered index key size — required for IX_Items_Name below.

            builder.Property(p => p.Description)
                .IsRequired()
                .HasMaxLength(1000);

            builder
                .HasOne(e => e.Genre)
                .WithMany(c => c.Items)
                .HasForeignKey(k => k.GenreId);

            builder
                .HasOne(e => e.Artist)
                .WithMany(c => c.Items)
                .HasForeignKey(k => k.ArtistId);

            builder.Property(x => x.Price)
                .HasPrecision(18, 2);

            // Supports the OrderBy(x => x.Name) used by the paginated GetAll query.
            builder.HasIndex(x => x.Name);
        }
    }
}
