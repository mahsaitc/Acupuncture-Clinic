using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Branding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandSubtitleEn",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandSubtitleFa",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandTitleEn",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandTitleFa",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicNameEn",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicNameFa",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeroEyebrowEn",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeroEyebrowFa",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoPath",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemeColor",
                table: "SiteContent",
                type: "TEXT",
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "BrandSubtitleEn",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "BrandSubtitleFa",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "BrandTitleEn",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "BrandTitleFa",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "ClinicNameEn",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "ClinicNameFa",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "HeroEyebrowEn",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "HeroEyebrowFa",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "LogoPath",
                table: "SiteContent");

            migrationBuilder.DropColumn(
                name: "ThemeColor",
                table: "SiteContent");
        }
    }
}
