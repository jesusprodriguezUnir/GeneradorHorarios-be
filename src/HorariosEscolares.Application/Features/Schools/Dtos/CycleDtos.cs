namespace HorariosEscolares.Application.Features.Schools;

public record CycleBreakDto(int AfterSlot, int Minutes);

public record CycleScheduleDto(
    int Cycle,
    string MorningStart,
    string MorningEnd,
    string? AfternoonStart,
    string? AfternoonEnd,
    /// <summary>Alias de AfternoonEnd ?? MorningEnd; se mantiene por compatibilidad con consumidores existentes.</summary>
    string EndTime,
    IReadOnlyList<SlotDto> ComputedSlots,
    IReadOnlyList<CycleBreakDto> Breaks);
