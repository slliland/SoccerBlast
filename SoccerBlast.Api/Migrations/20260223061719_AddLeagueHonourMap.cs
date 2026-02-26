using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueHonourMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeagueHonourMaps",
                columns: table => new
                {
                    LeagueId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    HonourId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueHonourMaps", x => new { x.LeagueId, x.HonourId });
                    table.ForeignKey(
                        name: "FK_LeagueHonourMaps_Honours_HonourId",
                        column: x => x.HonourId,
                        principalTable: "Honours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueHonourMaps_HonourId",
                table: "LeagueHonourMaps",
                column: "HonourId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueHonourMaps_LeagueId",
                table: "LeagueHonourMaps",
                column: "LeagueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueHonourMaps");
        }
    }
}
