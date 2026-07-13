using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomStageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StageId",
                table: "Classrooms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_StageId",
                table: "Classrooms",
                column: "StageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Classrooms_SchoolStages_StageId",
                table: "Classrooms",
                column: "StageId",
                principalTable: "SchoolStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Classrooms_SchoolStages_StageId",
                table: "Classrooms");

            migrationBuilder.DropIndex(
                name: "IX_Classrooms_StageId",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "StageId",
                table: "Classrooms");
        }
    }
}
