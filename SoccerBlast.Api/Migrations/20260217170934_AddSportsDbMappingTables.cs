using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSportsDbMappingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamProfiles_Teams_TeamId1",
                table: "TeamProfiles");

            migrationBuilder.DropIndex(
                name: "IX_TeamProfiles_TeamId1",
                table: "TeamProfiles");

            migrationBuilder.DropIndex(
                name: "IX_TeamExternalMaps_Provider_ExternalId",
                table: "TeamExternalMaps");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionExternalMaps_Provider_ExternalId",
                table: "CompetitionExternalMaps");

            migrationBuilder.DropColumn(
                name: "TeamId1",
                table: "TeamProfiles");

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "TeamProfiles",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

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

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionExternalMaps_ExternalId",
                table: "CompetitionExternalMaps",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionExternalMaps_LastSyncedUtc",
                table: "CompetitionExternalMaps",
                column: "LastSyncedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionExternalMaps_Provider_ExternalId",
                table: "CompetitionExternalMaps",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamProfiles_Teams_TeamId",
                table: "TeamProfiles",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamProfiles_Teams_TeamId",
                table: "TeamProfiles");

            migrationBuilder.DropIndex(
                name: "IX_TeamExternalMaps_ExternalId",
                table: "TeamExternalMaps");

            migrationBuilder.DropIndex(
                name: "IX_TeamExternalMaps_LastSyncedUtc",
                table: "TeamExternalMaps");

            migrationBuilder.DropIndex(
                name: "IX_TeamExternalMaps_Provider_ExternalId",
                table: "TeamExternalMaps");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionExternalMaps_ExternalId",
                table: "CompetitionExternalMaps");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionExternalMaps_LastSyncedUtc",
                table: "CompetitionExternalMaps");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionExternalMaps_Provider_ExternalId",
                table: "CompetitionExternalMaps");

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "TeamProfiles",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "TeamId1",
                table: "TeamProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TeamProfiles_TeamId1",
                table: "TeamProfiles",
                column: "TeamId1");

            migrationBuilder.CreateIndex(
                name: "IX_TeamExternalMaps_Provider_ExternalId",
                table: "TeamExternalMaps",
                columns: new[] { "Provider", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionExternalMaps_Provider_ExternalId",
                table: "CompetitionExternalMaps",
                columns: new[] { "Provider", "ExternalId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TeamProfiles_Teams_TeamId1",
                table: "TeamProfiles",
                column: "TeamId1",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
