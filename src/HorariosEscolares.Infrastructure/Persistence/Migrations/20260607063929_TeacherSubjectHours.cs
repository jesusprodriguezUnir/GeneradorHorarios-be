using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeacherSubjectHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Specialties",
                table: "Teachers");

            migrationBuilder.CreateTable(
                name: "TeacherSubjectHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WeeklyHours = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSubjectHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherSubjectHours_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjectHours_TeacherId_SubjectKey",
                table: "TeacherSubjectHours",
                columns: new[] { "TeacherId", "SubjectKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherSubjectHours");

            migrationBuilder.AddColumn<string>(
                name: "Specialties",
                table: "Teachers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "[]");
        }
    }
}
