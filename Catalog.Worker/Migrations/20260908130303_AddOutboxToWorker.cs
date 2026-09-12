using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxToWorker : Migration
    {
        // Deliberately a no-op: Catalog.Worker's WorkerDbContext shares the same physical database
        // as Catalog.API's CatalogContext, and CatalogContext's own (earlier) migrations already
        // created [eventbus].[OutboxMessages] there. WorkerDbContext still maps that table (via
        // ApplyOutboxConfiguration in OnModelCreating) so EfOutbox<WorkerDbContext> can use it, but
        // this migration must not try to create it again.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
