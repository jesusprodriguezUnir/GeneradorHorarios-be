using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CycleSchedules_SchoolId_Cycle",
                table: "CycleSchedules");

            migrationBuilder.AddColumn<Guid>(
                name: "PeriodId",
                table: "Schedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PeriodId",
                table: "CycleSchedules",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "PeriodAssignmentHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeeklyHours = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodAssignmentHours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Months = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "[10,11,12,1,2,3,4,5]"),
                    ScheduleType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "continua"),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    SlotsPerDay = table.Column<int>(type: "int", nullable: false),
                    AfternoonSlots = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolPeriods", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CycleSchedules_PeriodId_Cycle",
                table: "CycleSchedules",
                columns: new[] { "PeriodId", "Cycle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CycleSchedules_SchoolId_Cycle",
                table: "CycleSchedules",
                columns: new[] { "SchoolId", "Cycle" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodAssignmentHours_PeriodId_AssignmentId",
                table: "PeriodAssignmentHours",
                columns: new[] { "PeriodId", "AssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolPeriods_SchoolId",
                table: "SchoolPeriods",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolPeriods_SchoolId_Key",
                table: "SchoolPeriods",
                columns: new[] { "SchoolId", "Key" },
                unique: true);

            // ── Back-fill: crear periodo ordinario para centros con CycleSchedules ──
            migrationBuilder.Sql(@"
                DECLARE @schoolId uniqueidentifier, @periodId uniqueidentifier;
                DECLARE school_cursor CURSOR FOR
                    SELECT DISTINCT cs.SchoolId
                    FROM CycleSchedules cs
                    WHERE NOT EXISTS (SELECT 1 FROM SchoolPeriods sp WHERE sp.SchoolId = cs.SchoolId);
                OPEN school_cursor;
                FETCH NEXT FROM school_cursor INTO @schoolId;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @periodId = NEWID();
                    INSERT INTO SchoolPeriods (Id, SchoolId, [Key], Name, Months, ScheduleType, SlotMinutes, SlotsPerDay, AfternoonSlots, IsDefault, SortOrder)
                    SELECT @periodId, s.Id, 'ordinario', 'Jornada ordinaria', '[10,11,12,1,2,3,4,5]',
                           ISNULL(s.ScheduleType, 'continua'), ISNULL(s.SlotMinutes, 60), ISNULL(s.SlotsPerDay, 5),
                           CASE WHEN ISNULL(s.ScheduleType, 'continua') = 'partida' THEN 2 ELSE 0 END,
                           1, 0
                    FROM Schools s WHERE s.Id = @schoolId;
                    UPDATE CycleSchedules SET PeriodId = @periodId WHERE SchoolId = @schoolId;
                    FETCH NEXT FROM school_cursor INTO @schoolId;
                END;
                CLOSE school_cursor;
                DEALLOCATE school_cursor;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_CycleSchedules_SchoolPeriods_PeriodId",
                table: "CycleSchedules",
                column: "PeriodId",
                principalTable: "SchoolPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CycleSchedules_SchoolPeriods_PeriodId",
                table: "CycleSchedules");

            migrationBuilder.DropTable(
                name: "PeriodAssignmentHours");

            migrationBuilder.DropTable(
                name: "SchoolPeriods");

            migrationBuilder.DropIndex(
                name: "IX_CycleSchedules_PeriodId_Cycle",
                table: "CycleSchedules");

            migrationBuilder.DropIndex(
                name: "IX_CycleSchedules_SchoolId_Cycle",
                table: "CycleSchedules");

            migrationBuilder.DropColumn(
                name: "PeriodId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "PeriodId",
                table: "CycleSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_CycleSchedules_SchoolId_Cycle",
                table: "CycleSchedules",
                columns: new[] { "SchoolId", "Cycle" },
                unique: true);
        }
    }
}
