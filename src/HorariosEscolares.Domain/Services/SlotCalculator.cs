using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Services;

public static class SlotCalculator
{
    public static List<SlotInfo> Compute(
        int totalSlots,
        int slotMinutes,
        int breakAfterSlot,
        int breakMinutes,
        int afternoonSlots,
        TimeOnly morningStart,
        TimeOnly? afternoonStart = null,
        bool isPartida = false)
    {
        var slots = new List<SlotInfo>();
        var current = morningStart;

        int morningSlotsLimit = isPartida ? totalSlots - afternoonSlots : totalSlots;

        for (int i = 0; i < morningSlotsLimit; i++)
        {
            if (i == breakAfterSlot)
            {
                slots.Add(new SlotInfo(-1,
                    current.ToString("HH:mm"),
                    current.AddMinutes(breakMinutes).ToString("HH:mm"),
                    IsBreak: true));
                current = current.AddMinutes(breakMinutes);
            }
            var end = current.AddMinutes(slotMinutes);
            slots.Add(new SlotInfo(i, current.ToString("HH:mm"), end.ToString("HH:mm"), IsBreak: false));
            current = end;
        }

        if (isPartida && morningSlotsLimit < totalSlots && afternoonStart.HasValue)
        {
            var afternoonCurrent = afternoonStart.Value;
            for (int i = morningSlotsLimit; i < totalSlots; i++)
            {
                var end = afternoonCurrent.AddMinutes(slotMinutes);
                slots.Add(new SlotInfo(i, afternoonCurrent.ToString("HH:mm"), end.ToString("HH:mm"), IsBreak: false));
                afternoonCurrent = end;
            }
        }

        return slots;
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
    {
        var current = morningStart;
        var finalAfternoonStart = afternoonStart;
        bool isPartidaActual = isPartida && finalAfternoonStart.HasValue && afternoonSlots > 0;
        int morningSlots = isPartidaActual ? totalSlots - afternoonSlots : totalSlots;

        for (int i = 0; i < morningSlots; i++)
        {
            if (i == breakAfterSlot)
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

    public static List<SlotInfo> Compute(School s, int totalSlots)
        => Compute(totalSlots, s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes,
            s.AfternoonSlots, s.MorningStart, s.AfternoonStart, s.ScheduleType == "partida");

    public static List<SlotInfo> Compute(School s, int totalSlots, TimeOnly morningStart, TimeOnly? afternoonStart)
        => Compute(totalSlots, s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes,
            s.AfternoonSlots, morningStart, afternoonStart, afternoonStart.HasValue && s.AfternoonSlots > 0);

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

public record SlotInfo(int Index, string StartTime, string EndTime, bool IsBreak);
