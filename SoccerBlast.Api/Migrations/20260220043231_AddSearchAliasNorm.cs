using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchAliasNorm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AliasNorm",
                table: "SearchAliases",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SearchAliases_HitCount",
                table: "SearchAliases",
                column: "HitCount");

            migrationBuilder.CreateIndex(
                name: "IX_SearchAliases_Type_AliasNorm",
                table: "SearchAliases",
                columns: new[] { "Type", "AliasNorm" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchAliases_Type_Canonical_Alias",
                table: "SearchAliases",
                columns: new[] { "Type", "Canonical", "Alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchAliases_UpdatedAtUtc",
                table: "SearchAliases",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SearchAliases_HitCount",
                table: "SearchAliases");

            migrationBuilder.DropIndex(
                name: "IX_SearchAliases_Type_AliasNorm",
                table: "SearchAliases");

            migrationBuilder.DropIndex(
                name: "IX_SearchAliases_Type_Canonical_Alias",
                table: "SearchAliases");

            migrationBuilder.DropIndex(
                name: "IX_SearchAliases_UpdatedAtUtc",
                table: "SearchAliases");

            migrationBuilder.DropColumn(
                name: "AliasNorm",
                table: "SearchAliases");
        }
    }
}
