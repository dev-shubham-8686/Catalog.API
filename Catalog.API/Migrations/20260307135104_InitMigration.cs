using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.API.Migrations
{
    /// <inheritdoc />
    public partial class InitMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "Artists",
                schema: "catalog",
                columns: table => new
                {
                    ArtistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtistName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.ArtistId);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                schema: "catalog",
                columns: table => new
                {
                    GenreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenreDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.GenreId);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LabelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PictureUri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReleaseDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvailableStock = table.Column<int>(type: "int", nullable: true),
                    GenreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArtistId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalSchema: "catalog",
                        principalTable: "Artists",
                        principalColumn: "ArtistId");
                    table.ForeignKey(
                        name: "FK_Items_Genres_GenreId",
                        column: x => x.GenreId,
                        principalSchema: "catalog",
                        principalTable: "Genres",
                        principalColumn: "GenreId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_ArtistId",
                schema: "catalog",
                table: "Items",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_GenreId",
                schema: "catalog",
                table: "Items",
                column: "GenreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Items",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Artists",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Genres",
                schema: "catalog");
        }
    }
}
