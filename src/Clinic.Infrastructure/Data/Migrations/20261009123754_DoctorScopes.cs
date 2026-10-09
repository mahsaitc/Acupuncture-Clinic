using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DoctorScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DoctorProfileId",
                table: "PatientProfiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppointmentId",
                table: "ContactMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DoctorProfileId",
                table: "ContactMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientProfiles_DoctorProfileId",
                table: "PatientProfiles",
                column: "DoctorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactMessages_DoctorProfileId",
                table: "ContactMessages",
                column: "DoctorProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientProfiles_DoctorProfileId",
                table: "PatientProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ContactMessages_DoctorProfileId",
                table: "ContactMessages");

            migrationBuilder.DropColumn(
                name: "DoctorProfileId",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "ContactMessages");

            migrationBuilder.DropColumn(
                name: "DoctorProfileId",
                table: "ContactMessages");
        }
    }
}
