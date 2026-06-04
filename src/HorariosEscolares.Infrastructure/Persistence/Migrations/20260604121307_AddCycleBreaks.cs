using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleBreaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CycleBreaks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AfterSlot = table.Column<int>(type: "int", nullable: false),
                    Minutes = table.Column<int>(type: "int", nullable: false, defaultValue: 30)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CycleBreaks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CycleBreaks_CycleSchedules_CycleScheduleId",
                        column: x => x.CycleScheduleId,
                        principalTable: "CycleSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CycleBreaks_CycleScheduleId",
                table: "CycleBreaks",
                column: "CycleScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CycleBreaks");
        }
    }
}
