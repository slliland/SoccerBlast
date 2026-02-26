using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueSeasonStandings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeagueSeasonStandings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LeagueId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Season = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TeamBadgeUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Form = table.Column<string>(type: "TEXT", nullable: true),
                    Played = table.Column<int>(type: "INTEGER", nullable: false),
                    Win = table.Column<int>(type: "INTEGER", nullable: false),
                    Draw = table.Column<int>(type: "INTEGER", nullable: false),
                    Loss = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsFor = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalDifference = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    StrDescription = table.Column<string>(type: "TEXT", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ScrapedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueSeasonStandings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasonStandings_LeagueId_Season_TeamId",
                table: "LeagueSeasonStandings",
                columns: new[] { "LeagueId", "Season", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasonStandings_LeagueId_Season",
                table: "LeagueSeasonStandings",
                columns: new[] { "LeagueId", "Season" });

            migrationBuilder.CreateIndex(
                name: "IX_Honours_Id",
                table: "Honours",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Honours_Id",
                table: "Honours");

            migrationBuilder.DropTable(name: "LeagueSeasonStandings");
        }
    }
}
