using HorariosEscolares.Features.Schools;
using HorariosEscolares.Infrastructure.Persistence.Entities;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class SlotCalculatorTests
{
    [Fact]
    public void Compute_MorningSchedule_ReturnsExpectedSlots()
    {
        var school = new School
        {
            Name = "Test",
            Slug = "test",
            MorningStart = new TimeOnly(9, 0),
            SlotMinutes = 60,
            BreakAfterSlot = 2,
            BreakMinutes = 30,
        };

        var slots = SlotCalculator.Compute(school, totalSlots: 5);

        slots.Should().HaveCount(6); // 5 slots + 1 break
        slots[0].IsBreak.Should().BeFalse();
        slots[0].Index.Should().Be(0);
        slots[0].StartTime.Should().Be("09:00");
        slots[2].IsBreak.Should().BeTrue(); // break after slot 2
        slots[2].Index.Should().Be(-1);
    }

    [Fact]
    public void Compute_SlotMinutes50_CalculatesCorrectEndTimes()
    {
        var school = new School
        {
            Name = "Test",
            Slug = "test",
            MorningStart = new TimeOnly(8, 30),
            SlotMinutes = 50,
            BreakAfterSlot = 2,
            BreakMinutes = 20,
        };

        var slots = SlotCalculator.Compute(school, totalSlots: 4);

        slots.First(s => s.Index == 0).StartTime.Should().Be("08:30");
        slots.First(s => s.Index == 0).EndTime.Should().Be("09:20");
    }

    [Fact]
    public void ToDto_MapsAllFields()
    {
        var school = new School
        {
            Id = Guid.NewGuid(),
            Name = "Test School",
            Slug = "test",
            ScheduleType = "continua",
            MorningStart = new TimeOnly(9, 0),
            SlotMinutes = 60,
            BreakAfterSlot = 2,
            BreakMinutes = 30,
            SlotsPerDay = 5,
            DaysPerWeek = 5
        };

        var dto = SlotCalculator.ToDto(school);

        dto.Id.Should().Be(school.Id);
        dto.Name.Should().Be("Test School");
        dto.MorningStart.Should().Be("09:00");
        dto.AfternoonStart.Should().BeNull();
        dto.SlotsPerDay.Should().Be(5);
        dto.DaysPerWeek.Should().Be(5);
        dto.ComputedSlots.Should().NotBeEmpty();
    }

    [Fact]
    public void Compute_JornadaPartida_ReturnsMorningAndAfternoonSlots()
    {
        var school = new School
        {
            Name = "Test Partida",
            Slug = "test-partida",
            ScheduleType = "partida",
            MorningStart = new TimeOnly(9, 0),
            AfternoonStart = new TimeOnly(15, 0),
            SlotMinutes = 60,
            BreakAfterSlot = 2,
            BreakMinutes = 30,
            SlotsPerDay = 5,
            DaysPerWeek = 5
        };

        var slots = SlotCalculator.Compute(school, totalSlots: 5);

        // Debería tener 6 elementos: 3 mañana (0, 1, 2), 1 recreo mañana, 2 tarde (3, 4)
        slots.Should().HaveCount(6);
        
        // Slot 0: 09:00 - 10:00
        slots[0].Index.Should().Be(0);
        slots[0].StartTime.Should().Be("09:00");
        slots[0].EndTime.Should().Be("10:00");
        slots[0].IsBreak.Should().BeFalse();

        // Slot 1: 10:00 - 11:00
        slots[1].Index.Should().Be(1);
        slots[1].StartTime.Should().Be("10:00");
        slots[1].EndTime.Should().Be("11:00");
        slots[1].IsBreak.Should().BeFalse();

        // Recreo mañana: 11:00 - 11:30
        slots[2].Index.Should().Be(-1);
        slots[2].StartTime.Should().Be("11:00");
        slots[2].EndTime.Should().Be("11:30");
        slots[2].IsBreak.Should().BeTrue();

        // Slot 2: 11:30 - 12:30
        slots[3].Index.Should().Be(2);
        slots[3].StartTime.Should().Be("11:30");
        slots[3].EndTime.Should().Be("12:30");
        slots[3].IsBreak.Should().BeFalse();

        // Slot 3: 15:00 - 16:00 (Tarde)
        slots[4].Index.Should().Be(3);
        slots[4].StartTime.Should().Be("15:00");
        slots[4].EndTime.Should().Be("16:00");
        slots[4].IsBreak.Should().BeFalse();

        // Slot 4: 16:00 - 17:00 (Tarde)
        slots[5].Index.Should().Be(4);
        slots[5].StartTime.Should().Be("16:00");
        slots[5].EndTime.Should().Be("17:00");
        slots[5].IsBreak.Should().BeFalse();
    }
}
