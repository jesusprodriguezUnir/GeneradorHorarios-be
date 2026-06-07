using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleAndCourseLevelToSubjectAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseLevel",
                table: "SubjectAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Cycle",
                table: "SubjectAllocations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourseLevel",
                table: "SubjectAllocations");

            migrationBuilder.DropColumn(
                name: "Cycle",
                table: "SubjectAllocations");
        }
    }
}
