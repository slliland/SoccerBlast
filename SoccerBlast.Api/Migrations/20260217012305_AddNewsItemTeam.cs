using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsItemTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsItemTeam",
                columns: table => new
                {
                    NewsItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItemTeam", x => new { x.NewsItemId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_NewsItemTeam_NewsItems_NewsItemId",
                        column: x => x.NewsItemId,
                        principalTable: "NewsItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsItemTeam_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItemTeam_NewsItemId",
                table: "NewsItemTeam",
                column: "NewsItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsItemTeam_TeamId",
                table: "NewsItemTeam",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsItemTeam");
        }
    }
}
