using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "orders",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                schema: "orders",
                table: "Orders",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId",
                schema: "orders",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "orders",
                table: "Orders");
        }
    }
}
