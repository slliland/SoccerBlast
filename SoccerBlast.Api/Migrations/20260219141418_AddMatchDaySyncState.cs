using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchDaySyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchDaySyncStates",
                columns: table => new
                {
                    LocalDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncedCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchDaySyncStates", x => x.LocalDate);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchDaySyncStates");
        }
    }
}
