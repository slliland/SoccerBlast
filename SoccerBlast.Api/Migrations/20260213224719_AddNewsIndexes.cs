using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_PublishedAtUtc",
                table: "NewsItems",
                column: "PublishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_UrlHash",
                table: "NewsItems",
                column: "UrlHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NewsItems_PublishedAtUtc",
                table: "NewsItems");

            migrationBuilder.DropIndex(
                name: "IX_NewsItems_UrlHash",
                table: "NewsItems");
        }
    }
}
