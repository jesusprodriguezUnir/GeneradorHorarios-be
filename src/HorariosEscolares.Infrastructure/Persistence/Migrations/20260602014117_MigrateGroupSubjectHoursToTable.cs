using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateGroupSubjectHoursToTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubjectHours",
                table: "CourseGroups");

            migrationBuilder.CreateTable(
                name: "GroupSubjectHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Hours = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupSubjectHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupSubjectHours_CourseGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CourseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSubjectHours_GroupId_SubjectKey",
                table: "GroupSubjectHours",
                columns: new[] { "GroupId", "SubjectKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupSubjectHours");

            migrationBuilder.AddColumn<string>(
                name: "SubjectHours",
                table: "CourseGroups",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
