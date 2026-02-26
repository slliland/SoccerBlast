using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHonoursTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Honours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    TrophyImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    HonourUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    TypeGuess = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_Honours", x => x.Id));

            migrationBuilder.CreateTable(
                name: "TeamHonours",
                columns: table => new
                {
                    TeamId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    HonourId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamHonours", x => new { x.TeamId, x.HonourId });
                    table.ForeignKey(
                        name: "FK_TeamHonours_Honours_HonourId",
                        column: x => x.HonourId,
                        principalTable: "Honours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HonourWinners",
                columns: table => new
                {
                    HonourId = table.Column<int>(type: "INTEGER", nullable: false),
                    YearLabel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TeamId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", nullable: true),
                    TeamBadgeUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HonourWinners", x => new { x.HonourId, x.YearLabel });
                    table.ForeignKey(
                        name: "FK_HonourWinners_Honours_HonourId",
                        column: x => x.HonourId,
                        principalTable: "Honours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamHonours_HonourId",
                table: "TeamHonours",
                column: "HonourId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamHonours_TeamId",
                table: "TeamHonours",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_HonourWinners_TeamId",
                table: "HonourWinners",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HonourWinners");
            migrationBuilder.DropTable(name: "TeamHonours");
            migrationBuilder.DropTable(name: "Honours");
        }
    }
}
