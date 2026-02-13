using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamCrestUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CrestUrl",
                table: "Teams",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CrestUrl",
                table: "Teams");
        }
    }
}
