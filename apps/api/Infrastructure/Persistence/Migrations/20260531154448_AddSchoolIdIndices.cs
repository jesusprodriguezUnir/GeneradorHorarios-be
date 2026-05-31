using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolIdIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TeacherConstraints_SchoolId",
                table: "TeacherConstraints",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_SchoolId",
                table: "Schedules",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_SchoolId",
                table: "ScheduleEntries",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_SchoolId",
                table: "Classrooms",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_SchoolId",
                table: "Assignments",
                column: "SchoolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherConstraints_SchoolId",
                table: "TeacherConstraints");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_SchoolId",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_SchoolId",
                table: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_Classrooms_SchoolId",
                table: "Classrooms");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_SchoolId",
                table: "Assignments");
        }
    }
}
