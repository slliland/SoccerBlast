using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsItemTeam2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NewsItemTeam_NewsItems_NewsItemId",
                table: "NewsItemTeam");

            migrationBuilder.DropForeignKey(
                name: "FK_NewsItemTeam_Teams_TeamId",
                table: "NewsItemTeam");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NewsItemTeam",
                table: "NewsItemTeam");

            migrationBuilder.RenameTable(
                name: "NewsItemTeam",
                newName: "NewsItemTeams");

            migrationBuilder.RenameIndex(
                name: "IX_NewsItemTeam_TeamId",
                table: "NewsItemTeams",
                newName: "IX_NewsItemTeams_TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_NewsItemTeam_NewsItemId",
                table: "NewsItemTeams",
                newName: "IX_NewsItemTeams_NewsItemId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NewsItemTeams",
                table: "NewsItemTeams",
                columns: new[] { "NewsItemId", "TeamId" });

            migrationBuilder.AddForeignKey(
                name: "FK_NewsItemTeams_NewsItems_NewsItemId",
                table: "NewsItemTeams",
                column: "NewsItemId",
                principalTable: "NewsItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NewsItemTeams_Teams_TeamId",
                table: "NewsItemTeams",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NewsItemTeams_NewsItems_NewsItemId",
                table: "NewsItemTeams");

            migrationBuilder.DropForeignKey(
                name: "FK_NewsItemTeams_Teams_TeamId",
                table: "NewsItemTeams");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NewsItemTeams",
                table: "NewsItemTeams");

            migrationBuilder.RenameTable(
                name: "NewsItemTeams",
                newName: "NewsItemTeam");

            migrationBuilder.RenameIndex(
                name: "IX_NewsItemTeams_TeamId",
                table: "NewsItemTeam",
                newName: "IX_NewsItemTeam_TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_NewsItemTeams_NewsItemId",
                table: "NewsItemTeam",
                newName: "IX_NewsItemTeam_NewsItemId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NewsItemTeam",
                table: "NewsItemTeam",
                columns: new[] { "NewsItemId", "TeamId" });

            migrationBuilder.AddForeignKey(
                name: "FK_NewsItemTeam_NewsItems_NewsItemId",
                table: "NewsItemTeam",
                column: "NewsItemId",
                principalTable: "NewsItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NewsItemTeam_Teams_TeamId",
                table: "NewsItemTeam",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
