using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropTeamExternalMapAddSportsDbId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamExternalMaps");

            migrationBuilder.AddColumn<string>(
                name: "SportsDbId",
                table: "Teams",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_SportsDbId",
                table: "Teams",
                column: "SportsDbId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_SportsDbId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "SportsDbId",
                table: "Teams");

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

            migrationBuilder.CreateIndex(
                name: "IX_TeamExternalMaps_ExternalId",
                table: "TeamExternalMaps",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamExternalMaps_LastSyncedUtc",
                table: "TeamExternalMaps",
                column: "LastSyncedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TeamExternalMaps_Provider_ExternalId",
                table: "TeamExternalMaps",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);
        }
    }
}
