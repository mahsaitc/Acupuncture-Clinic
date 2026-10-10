using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PatientInsurance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasSupplementaryInsurance",
                table: "PatientProfiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Insurance",
                table: "PatientProfiles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasSupplementaryInsurance",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "Insurance",
                table: "PatientProfiles");
        }
    }
}
