using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolPeriods_SchoolId_Key",
                table: "SchoolPeriods");

            migrationBuilder.DropIndex(
                name: "IX_CourseGroups_SchoolId_CourseLevel_GroupLabel",
                table: "CourseGroups");

            // 1. Crear la tabla de etapas antes del back-fill.
            migrationBuilder.CreateTable(
                name: "SchoolStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinLevel = table.Column<int>(type: "int", nullable: false),
                    MaxLevel = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ScheduleType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "continua"),
                    MorningStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    AfternoonStart = table.Column<TimeOnly>(type: "time", nullable: true),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    BreakAfterSlot = table.Column<int>(type: "int", nullable: false),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false),
                    SlotsPerDay = table.Column<int>(type: "int", nullable: false),
                    AfternoonSlots = table.Column<int>(type: "int", nullable: false),
                    DaysPerWeek = table.Column<int>(type: "int", nullable: false),
                    WorkingDays = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "[1,2,3,4,5]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolStages", x => x.Id);
                });

            // 2. Añadir StageId como NULLABLE para poder rellenarlo sin romper datos.
            migrationBuilder.AddColumn<Guid>(
                name: "StageId", table: "SchoolPeriods",
                type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "StageId", table: "Schedules",
                type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "StageId", table: "CycleSchedules",
                type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "StageId", table: "CurriculumTemplates",
                type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "StageId", table: "CourseGroups",
                type: "uniqueidentifier", nullable: true);

            // 3. Back-fill: una etapa 'primaria' por colegio existente, copiando su jornada,
            //    y reasignar los hijos. Las plantillas oficiales (SchoolId NULL) quedan sin etapa.
            migrationBuilder.Sql(@"
DECLARE @schoolId uniqueidentifier;
DECLARE @stageId uniqueidentifier;
DECLARE school_cursor CURSOR FOR SELECT Id FROM Schools;
OPEN school_cursor;
FETCH NEXT FROM school_cursor INTO @schoolId;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @stageId = NEWID();
    INSERT INTO SchoolStages (Id, SchoolId, StageType, Name, MinLevel, MaxLevel, SortOrder, ScheduleType, MorningStart, AfternoonStart, SlotMinutes, BreakAfterSlot, BreakMinutes, SlotsPerDay, AfternoonSlots, DaysPerWeek, WorkingDays)
    SELECT @stageId, s.Id, 'primaria', N'Educación Primaria', s.MinCourseLevel, s.MaxCourseLevel, 1, s.ScheduleType, s.MorningStart, s.AfternoonStart, s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes, s.SlotsPerDay, s.AfternoonSlots, s.DaysPerWeek, s.WorkingDays
    FROM Schools s WHERE s.Id = @schoolId;

    UPDATE CourseGroups       SET StageId = @stageId WHERE SchoolId = @schoolId;
    UPDATE SchoolPeriods      SET StageId = @stageId WHERE SchoolId = @schoolId;
    UPDATE CycleSchedules     SET StageId = @stageId WHERE SchoolId = @schoolId;
    UPDATE Schedules          SET StageId = @stageId WHERE SchoolId = @schoolId;
    UPDATE CurriculumTemplates SET StageId = @stageId WHERE SchoolId = @schoolId;

    FETCH NEXT FROM school_cursor INTO @schoolId;
END
CLOSE school_cursor;
DEALLOCATE school_cursor;
");

            // 4. Endurecer a NOT NULL (CurriculumTemplates.StageId permanece nullable).
            migrationBuilder.AlterColumn<Guid>(
                name: "StageId", table: "SchoolPeriods",
                type: "uniqueidentifier", nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "StageId", table: "Schedules",
                type: "uniqueidentifier", nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "StageId", table: "CycleSchedules",
                type: "uniqueidentifier", nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "StageId", table: "CourseGroups",
                type: "uniqueidentifier", nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);

            // 5. Índices y claves foráneas.
            migrationBuilder.CreateIndex(
                name: "IX_SchoolPeriods_StageId",
                table: "SchoolPeriods",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolPeriods_StageId_Key",
                table: "SchoolPeriods",
                columns: new[] { "StageId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_StageId",
                table: "Schedules",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleSchedules_StageId",
                table: "CycleSchedules",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumTemplates_StageId",
                table: "CurriculumTemplates",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseGroups_SchoolId",
                table: "CourseGroups",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseGroups_StageId_CourseLevel_GroupLabel",
                table: "CourseGroups",
                columns: new[] { "StageId", "CourseLevel", "GroupLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStages_SchoolId",
                table: "SchoolStages",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStages_SchoolId_StageType",
                table: "SchoolStages",
                columns: new[] { "SchoolId", "StageType" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseGroups_SchoolStages_StageId",
                table: "CourseGroups",
                column: "StageId",
                principalTable: "SchoolStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CurriculumTemplates_SchoolStages_StageId",
                table: "CurriculumTemplates",
                column: "StageId",
                principalTable: "SchoolStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CycleSchedules_SchoolStages_StageId",
                table: "CycleSchedules",
                column: "StageId",
                principalTable: "SchoolStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_SchoolStages_StageId",
                table: "Schedules",
                column: "StageId",
                principalTable: "SchoolStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolPeriods_SchoolStages_StageId",
                table: "SchoolPeriods",
                column: "StageId",
                principalTable: "SchoolStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseGroups_SchoolStages_StageId",
                table: "CourseGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumTemplates_SchoolStages_StageId",
                table: "CurriculumTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_CycleSchedules_SchoolStages_StageId",
                table: "CycleSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_SchoolStages_StageId",
                table: "Schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolPeriods_SchoolStages_StageId",
                table: "SchoolPeriods");

            migrationBuilder.DropTable(
                name: "SchoolStages");

            migrationBuilder.DropIndex(
                name: "IX_SchoolPeriods_StageId",
                table: "SchoolPeriods");

            migrationBuilder.DropIndex(
                name: "IX_SchoolPeriods_StageId_Key",
                table: "SchoolPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_StageId",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_CycleSchedules_StageId",
                table: "CycleSchedules");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumTemplates_StageId",
                table: "CurriculumTemplates");

            migrationBuilder.DropIndex(
                name: "IX_CourseGroups_SchoolId",
                table: "CourseGroups");

            migrationBuilder.DropIndex(
                name: "IX_CourseGroups_StageId_CourseLevel_GroupLabel",
                table: "CourseGroups");

            migrationBuilder.DropColumn(
                name: "StageId",
                table: "SchoolPeriods");

            migrationBuilder.DropColumn(
                name: "StageId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "StageId",
                table: "CycleSchedules");

            migrationBuilder.DropColumn(
                name: "StageId",
                table: "CurriculumTemplates");

            migrationBuilder.DropColumn(
                name: "StageId",
                table: "CourseGroups");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolPeriods_SchoolId_Key",
                table: "SchoolPeriods",
                columns: new[] { "SchoolId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseGroups_SchoolId_CourseLevel_GroupLabel",
                table: "CourseGroups",
                columns: new[] { "SchoolId", "CourseLevel", "GroupLabel" },
                unique: true);
        }
    }
}
