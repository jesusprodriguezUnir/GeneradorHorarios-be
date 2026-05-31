using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolConfigFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "Schools",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "2025/2026");

            migrationBuilder.AddColumn<int>(
                name: "AfternoonSlots",
                table: "Schools",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CenterCode",
                table: "Schools",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Community",
                table: "Schools",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "madrid");

            migrationBuilder.AddColumn<string>(
                name: "Locality",
                table: "Schools",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCourseLevel",
                table: "Schools",
                type: "int",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "MinCourseLevel",
                table: "Schools",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "Schools",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "primaria");

            migrationBuilder.AddColumn<string>(
                name: "WorkingDays",
                table: "Schools",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "[1,2,3,4,5]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "AfternoonSlots",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "CenterCode",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "Community",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "Locality",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "MaxCourseLevel",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "MinCourseLevel",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "WorkingDays",
                table: "Schools");
        }
    }
}
