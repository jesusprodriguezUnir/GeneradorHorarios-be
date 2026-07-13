using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Services;

public static class SlotCalculator
{
    public static List<SlotInfo> Compute(
        int totalSlots,
        int slotMinutes,
        IReadOnlyList<(int AfterSlot, int Minutes)> breaks,
        int afternoonSlots,
        TimeOnly morningStart,
        TimeOnly? afternoonStart = null,
        bool isPartida = false)
    {
        var breakSet = breaks.ToDictionary(b => b.AfterSlot, b => b.Minutes);
        var slots = new List<SlotInfo>();
        var current = morningStart;

        int morningSlotsLimit = isPartida ? totalSlots - afternoonSlots : totalSlots;

        for (int i = 0; i < morningSlotsLimit; i++)
        {
            if (breakSet.TryGetValue(i, out var breakMinutes))
            {
                var breakStart = current;
                var breakEnd = current.AddMinutes(breakMinutes);
                slots.Add(new SlotInfo(-1,
                    breakStart.ToString("HH:mm"),
                    breakEnd.ToString("HH:mm"),
                    IsBreak: true,
                    StartMinute: (int)breakStart.ToTimeSpan().TotalMinutes,
                    EndMinute: (int)breakEnd.ToTimeSpan().TotalMinutes));
                current = breakEnd;
            }
            var end = current.AddMinutes(slotMinutes);
            slots.Add(new SlotInfo(i, current.ToString("HH:mm"), end.ToString("HH:mm"), IsBreak: false,
                StartMinute: (int)current.ToTimeSpan().TotalMinutes,
                EndMinute: (int)end.ToTimeSpan().TotalMinutes));
            current = end;
        }

        if (isPartida && morningSlotsLimit < totalSlots && afternoonStart.HasValue)
        {
            var afternoonCurrent = afternoonStart.Value;
            for (int i = morningSlotsLimit; i < totalSlots; i++)
            {
                var end = afternoonCurrent.AddMinutes(slotMinutes);
                slots.Add(new SlotInfo(i, afternoonCurrent.ToString("HH:mm"), end.ToString("HH:mm"), IsBreak: false,
                    StartMinute: (int)afternoonCurrent.ToTimeSpan().TotalMinutes,
                    EndMinute: (int)end.ToTimeSpan().TotalMinutes));
                afternoonCurrent = end;
            }
        }

        return slots;
    }

    public static List<SlotInfo> Compute(
        int totalSlots,
        int slotMinutes,
        int breakAfterSlot,
        int breakMinutes,
        int afternoonSlots,
        TimeOnly morningStart,
        TimeOnly? afternoonStart = null,
        bool isPartida = false)
        => Compute(totalSlots, slotMinutes, breakAfterSlot >= 0 ? [(breakAfterSlot, breakMinutes)] : [],
            afternoonSlots, morningStart, afternoonStart, isPartida);

    public static TimeOnly ComputeEndTime(
        int totalSlots,
        int slotMinutes,
        IReadOnlyList<(int AfterSlot, int Minutes)> breaks,
        int afternoonSlots,
        TimeOnly morningStart,
        TimeOnly? afternoonStart = null,
        bool isPartida = false)
    {
        var breakSet = breaks.ToDictionary(b => b.AfterSlot, b => b.Minutes);
        var current = morningStart;
        var finalAfternoonStart = afternoonStart;
        bool isPartidaActual = isPartida && finalAfternoonStart.HasValue && afternoonSlots > 0;
        int morningSlots = isPartidaActual ? totalSlots - afternoonSlots : totalSlots;

        for (int i = 0; i < morningSlots; i++)
        {
            if (breakSet.TryGetValue(i, out var breakMinutes))
                current = current.AddMinutes(breakMinutes);
            current = current.AddMinutes(slotMinutes);
        }

        if (isPartidaActual)
        {
            var afternoonCurrent = finalAfternoonStart!.Value;
            for (int i = morningSlots; i < totalSlots; i++)
                afternoonCurrent = afternoonCurrent.AddMinutes(slotMinutes);
            return afternoonCurrent;
        }

        return current;
    }

    public static TimeOnly ComputeEndTime(
        int totalSlots,
        int slotMinutes,
        int breakAfterSlot,
        int breakMinutes,
        int afternoonSlots,
        TimeOnly morningStart,
        TimeOnly? afternoonStart = null,
        bool isPartida = false)
        => ComputeEndTime(totalSlots, slotMinutes, breakAfterSlot >= 0 ? [(breakAfterSlot, breakMinutes)] : [],
            afternoonSlots, morningStart, afternoonStart, isPartida);

    public static List<SlotInfo> Compute(School s, int totalSlots)
        => Compute(totalSlots, s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes,
            s.AfternoonSlots, s.MorningStart, s.AfternoonStart, s.ScheduleType == "partida");

    public static List<SlotInfo> Compute(School s, int totalSlots, TimeOnly morningStart, TimeOnly? afternoonStart)
        => Compute(totalSlots, s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes,
            s.AfternoonSlots, morningStart, afternoonStart, afternoonStart.HasValue && s.AfternoonSlots > 0);

    /// <summary>
    /// Calcula las franjas lectivas y recreos a partir de las cuatro horas de jornada del ciclo.
    /// El número de franjas se deriva de (fin - inicio) / slotMinutes, respetando los recreos.
    /// Esta es la lógica principal para ciclos con las 4 horas configuradas explícitamente.
    /// </summary>
    public static List<SlotInfo> ComputeFromBoundaries(
        TimeOnly morningStart, TimeOnly morningEnd,
        TimeOnly? afternoonStart, TimeOnly? afternoonEnd,
        int slotMinutes,
        IReadOnlyList<(int AfterSlot, int Minutes)> breaks)
    {
        var breakSet = breaks.ToDictionary(b => b.AfterSlot, b => b.Minutes);
        var slots = new List<SlotInfo>();
        var current = morningStart;
        int index = 0;

        // — Mañana —
        while (true)
        {
            // Insertar recreo antes de la franja 'index' si corresponde
            if (breakSet.TryGetValue(index, out var breakMinutes))
            {
                var breakEnd = current.AddMinutes(breakMinutes);
                // Solo añadimos el recreo si cabe antes de morningEnd
                if (breakEnd <= morningEnd)
                {
                    slots.Add(new SlotInfo(-1,
                        current.ToString("HH:mm"), breakEnd.ToString("HH:mm"),
                        IsBreak: true,
                        StartMinute: (int)current.ToTimeSpan().TotalMinutes,
                        EndMinute: (int)breakEnd.ToTimeSpan().TotalMinutes));
                    current = breakEnd;
                }
            }
            var end = current.AddMinutes(slotMinutes);
            if (end > morningEnd) break;  // Franja parcial → no se añade
            slots.Add(new SlotInfo(index, current.ToString("HH:mm"), end.ToString("HH:mm"), IsBreak: false,
                StartMinute: (int)current.ToTimeSpan().TotalMinutes,
                EndMinute: (int)end.ToTimeSpan().TotalMinutes));
            current = end;
            index++;
        }

        // — Tarde —
        if (afternoonStart.HasValue && afternoonEnd.HasValue)
        {
            current = afternoonStart.Value;
            while (true)
            {
                var end = current.AddMinutes(slotMinutes);
                if (end > afternoonEnd.Value) break;
                slots.Add(new SlotInfo(index, current.ToString("HH:mm"), end.ToString("HH:mm"), IsBreak: false,
                    StartMinute: (int)current.ToTimeSpan().TotalMinutes,
                    EndMinute: (int)end.ToTimeSpan().TotalMinutes));
                current = end;
                index++;
            }
        }

        return slots;
    }

    /// <summary>
    /// Devuelve el nº de franjas lectivas (no-recreo) que caben entre las 4 horas del ciclo.
    /// </summary>
    public static int DeriveLectiveSlotCount(CycleSchedule c, int slotMinutes)
    {
        var breaks = c.Breaks
            .OrderBy(b => b.AfterSlot)
            .Select(b => (b.AfterSlot, b.Minutes))
            .ToList();
        return ComputeFromBoundaries(
                c.MorningStart, c.MorningEnd,
                c.AfternoonStart, c.AfternoonEnd,
                slotMinutes, breaks)
            .Count(s => !s.IsBreak);
    }

    public static List<SlotInfo> Compute(CycleSchedule c, School s)
    {
        var breaks = c.Breaks
            .OrderBy(b => b.AfterSlot)
            .Select(b => (b.AfterSlot, b.Minutes))
            .ToList();
        return ComputeFromBoundaries(
            c.MorningStart, c.MorningEnd,
            c.AfternoonStart, c.AfternoonEnd,
            s.SlotMinutes, breaks);
    }

    public static TimeOnly ComputeEndTime(CycleSchedule c, School s)
        => c.AfternoonEnd ?? c.MorningEnd;

    public static List<SlotInfo> Compute(CycleSchedule c, SchoolPeriod p)
    {
        var breaks = c.Breaks
            .OrderBy(b => b.AfterSlot)
            .Select(b => (b.AfterSlot, b.Minutes))
            .ToList();
        return ComputeFromBoundaries(
            c.MorningStart, c.MorningEnd,
            c.AfternoonStart, c.AfternoonEnd,
            p.SlotMinutes, breaks);
    }

    public static TimeOnly ComputeEndTime(CycleSchedule c, SchoolPeriod p)
        => c.AfternoonEnd ?? c.MorningEnd;

    public static IReadOnlyList<int> ParseWorkingDays(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<int>>(json) ?? [1, 2, 3, 4, 5];
        }
        catch
        {
            return [1, 2, 3, 4, 5];
        }
    }
}

public record SlotInfo(int Index, string StartTime, string EndTime, bool IsBreak, int StartMinute, int EndMinute);
