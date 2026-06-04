using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupSubjectHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubjectHours",
                table: "CourseGroups",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubjectHours",
                table: "CourseGroups");
        }
    }
}
