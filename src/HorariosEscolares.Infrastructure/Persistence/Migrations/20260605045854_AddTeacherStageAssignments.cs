using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherStageAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherStageAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cycle = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherStageAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherStageAssignments_SchoolStages_StageId",
                        column: x => x.StageId,
                        principalTable: "SchoolStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherStageAssignments_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStageAssignments_StageId",
                table: "TeacherStageAssignments",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStageAssignments_TeacherId_StageId_Cycle",
                table: "TeacherStageAssignments",
                columns: new[] { "TeacherId", "StageId", "Cycle" },
                unique: true,
                filter: "[Cycle] IS NOT NULL");

            // Prevent duplicate whole-stage assignments (Cycle IS NULL)
            migrationBuilder.CreateIndex(
                name: "IX_TeacherStageAssignments_TeacherId_StageId_NullCycle",
                table: "TeacherStageAssignments",
                columns: new[] { "TeacherId", "StageId" },
                unique: true,
                filter: "[Cycle] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherStageAssignments");
        }
    }
}
