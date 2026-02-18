using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerBlast.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnrichTeamProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BadgeUrl",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Facebook",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FormedYear",
                table: "TeamProfiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instagram",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keywords",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Leagues",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TertiaryColor",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Twitter",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Youtube",
                table: "TeamProfiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BadgeUrl",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "Facebook",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "FormedYear",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "Instagram",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "Leagues",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "TertiaryColor",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "Twitter",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "TeamProfiles");

            migrationBuilder.DropColumn(
                name: "Youtube",
                table: "TeamProfiles");
        }
    }
}
