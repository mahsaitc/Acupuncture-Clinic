using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DoctorDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NationalCode",
                table: "Doctors",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedUtc",
                table: "Doctors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalCodeHash",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DoctorDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DoctorProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    StoredName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorDocuments_Doctors_DoctorProfileId",
                        column: x => x.DoctorProfileId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NationalCodeHash",
                table: "AspNetUsers",
                column: "NationalCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorDocuments_DoctorProfileId",
                table: "DoctorDocuments",
                column: "DoctorProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NationalCodeHash",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NationalCode",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "RequestedUtc",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "NationalCodeHash",
                table: "AspNetUsers");
        }
    }
}
