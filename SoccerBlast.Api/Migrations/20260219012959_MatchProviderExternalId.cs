using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class MatchProviderExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExternalId",
                table: "Matches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Matches",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows so the unique index can be created
            migrationBuilder.Sql(@"
        UPDATE Matches
        SET Provider = 'SportsDbMatches',
            ExternalId = Id
        WHERE Provider = '' OR ExternalId = 0;
        ");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Provider_ExternalId",
                table: "Matches",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_Provider_ExternalId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Matches");
        }
    }
}
