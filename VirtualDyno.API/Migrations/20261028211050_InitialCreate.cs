using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualDyno.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DynoRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LogText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VehicleKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VehicleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gear = table.Column<int>(type: "int", nullable: true),
                    WeightLb = table.Column<double>(type: "float", nullable: false),
                    Correction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PeakWhp = table.Column<double>(type: "float", nullable: false),
                    PeakWhpRpm = table.Column<double>(type: "float", nullable: false),
                    PeakWtq = table.Column<double>(type: "float", nullable: false),
                    PeakWtqRpm = table.Column<double>(type: "float", nullable: false),
                    PeakBoostPsi = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynoRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DynoRuns_CreatedAtUtc",
                table: "DynoRuns",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DynoRuns");
        }
    }
}
