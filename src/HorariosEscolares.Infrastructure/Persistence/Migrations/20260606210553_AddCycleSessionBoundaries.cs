using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleSessionBoundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Añadir columnas nuevas con default temporal para poder asignarlas a filas existentes
            migrationBuilder.AddColumn<TimeOnly>(
                name: "MorningEnd",
                table: "CycleSchedules",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "AfternoonEnd",
                table: "CycleSchedules",
                type: "time",
                nullable: true);

            // Back-fill: para filas existentes, calcular MorningEnd y AfternoonEnd.
            //
            // Continua  (AfternoonStart IS NULL):
            //   MorningEnd = EndTime, AfternoonEnd = NULL
            //
            // Partida (AfternoonStart IS NOT NULL):
            //   MorningEnd = MorningStart + (sesiones de mañana × SlotMinutes) + (recreos dentro de mañana)
            //              donde sesiones de mañana = p.SlotsPerDay - p.AfternoonSlots
            //   AfternoonEnd = EndTime
            //
            // El antiguo campo solo almacenaba AfternoonStart, NO el fin real de la mañana.
            // Usar AfternoonStart como MorningEnd causaría que morningEnd == afternoonStart,
            // lo que rompe las validaciones del frontend (requiere afternoonStart > morningEnd).
            migrationBuilder.Sql(@"
                UPDATE cs
                SET
                    cs.MorningEnd = CASE
                        WHEN cs.AfternoonStart IS NULL
                            THEN cs.EndTime
                        ELSE
                            CAST(DATEADD(
                                minute,
                                (p.SlotsPerDay - p.AfternoonSlots) * p.SlotMinutes
                                    + COALESCE(b.TotalBreakMinutes, 0),
                                CAST(cs.MorningStart AS datetime)
                            ) AS time)
                    END,
                    cs.AfternoonEnd = CASE
                        WHEN cs.AfternoonStart IS NULL THEN NULL
                        ELSE cs.EndTime
                    END
                FROM CycleSchedules cs
                JOIN SchoolPeriods p ON cs.PeriodId = p.Id
                OUTER APPLY (
                    SELECT COALESCE(SUM(cb.Minutes), 0) AS TotalBreakMinutes
                    FROM CycleBreaks cb
                    WHERE cb.CycleScheduleId = cs.Id
                      AND cb.AfterSlot < (p.SlotsPerDay - p.AfternoonSlots)
                ) b
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AfternoonEnd",
                table: "CycleSchedules");

            migrationBuilder.DropColumn(
                name: "MorningEnd",
                table: "CycleSchedules");
        }
    }
}
