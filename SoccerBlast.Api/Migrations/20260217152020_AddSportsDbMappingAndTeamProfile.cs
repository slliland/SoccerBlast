using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSportsDbMappingAndTeamProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetitionExternalMaps",
                columns: table => new
                {
                    CompetitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionExternalMaps", x => new { x.CompetitionId, x.Provider });
                    table.ForeignKey(
                        name: "FK_CompetitionExternalMaps_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamExternalMaps",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamExternalMaps", x => new { x.TeamId, x.Provider });
                    table.ForeignKey(
                        name: "FK_TeamExternalMaps_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamProfiles",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId1 = table.Column<int>(type: "INTEGER", nullable: false),
                    StadiumName = table.Column<string>(type: "TEXT", nullable: true),
                    StadiumCapacity = table.Column<int>(type: "INTEGER", nullable: true),
                    StadiumLocation = table.Column<string>(type: "TEXT", nullable: true),
                    DescriptionEn = table.Column<string>(type: "TEXT", nullable: true),
                    BannerUrl = table.Column<string>(type: "TEXT", nullable: true),
                    JerseyUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamProfiles", x => x.TeamId);
                    table.ForeignKey(
                        name: "FK_TeamProfiles_Teams_TeamId1",
                        column: x => x.TeamId1,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionExternalMaps_Provider_ExternalId",
                table: "CompetitionExternalMaps",
                columns: new[] { "Provider", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamExternalMaps_Provider_ExternalId",
                table: "TeamExternalMaps",
                columns: new[] { "Provider", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamProfiles_LastUpdatedUtc",
                table: "TeamProfiles",
                column: "LastUpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TeamProfiles_TeamId1",
                table: "TeamProfiles",
                column: "TeamId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionExternalMaps");

            migrationBuilder.DropTable(
                name: "TeamExternalMaps");

            migrationBuilder.DropTable(
                name: "TeamProfiles");
        }
    }
}
