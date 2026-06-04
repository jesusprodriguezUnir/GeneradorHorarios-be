using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;
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
    public void Compute_WithAllFields_CalculatesSlots()
    {
        var school = new School
        {
            Name = "Test School",
            Slug = "test",
            ScheduleType = "continua",
            MorningStart = new TimeOnly(9, 0),
            SlotMinutes = 60,
            BreakAfterSlot = 2,
            BreakMinutes = 30,
            SlotsPerDay = 5,
            DaysPerWeek = 5,
            WorkingDays = "[1,2,3,4,5]",
        };

        var slots = SlotCalculator.Compute(school, school.SlotsPerDay);

        slots.Should().NotBeEmpty();
        slots.Where(s => !s.IsBreak).Should().HaveCount(5);
        slots.First(s => !s.IsBreak).StartTime.Should().Be("09:00");
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
            AfternoonSlots = 2,
            DaysPerWeek = 5,
        };

        var slots = SlotCalculator.Compute(school, totalSlots: 5);

        slots.Should().HaveCount(6);
        slots[0].Should().BeEquivalentTo(new SlotInfo(0, "09:00", "10:00", false));
        slots[1].Should().BeEquivalentTo(new SlotInfo(1, "10:00", "11:00", false));
        slots[2].Should().BeEquivalentTo(new SlotInfo(-1, "11:00", "11:30", true));
        slots[3].Should().BeEquivalentTo(new SlotInfo(2, "11:30", "12:30", false));
        slots[4].Should().BeEquivalentTo(new SlotInfo(3, "15:00", "16:00", false));
        slots[5].Should().BeEquivalentTo(new SlotInfo(4, "16:00", "17:00", false));
    }

    [Fact]
    public void Compute_JornadaPartida_AfternoonSlotsZero_TreatsAsContinua()
    {
        var school = new School
        {
            Name = "Test",
            Slug = "test",
            ScheduleType = "partida",
            MorningStart = new TimeOnly(9, 0),
            AfternoonStart = new TimeOnly(15, 0),
            SlotMinutes = 60,
            BreakAfterSlot = 2,
            BreakMinutes = 30,
            SlotsPerDay = 5,
            AfternoonSlots = 0,
        };

        var slots = SlotCalculator.Compute(school, totalSlots: 5);

        slots.Should().HaveCount(6);
        slots.Should().NotContain(s => s.StartTime == "15:00");
    }

    [Fact]
    public void Compute_JornadaPartida_OnlyOneMorningSlot_ThenAfternoon()
    {
        var school = new School
        {
            Name = "Test",
            Slug = "test",
            ScheduleType = "partida",
            MorningStart = new TimeOnly(9, 0),
            AfternoonStart = new TimeOnly(15, 0),
            SlotMinutes = 60,
            BreakAfterSlot = 5,
            BreakMinutes = 30,
            SlotsPerDay = 4,
            AfternoonSlots = 3,
        };

        var slots = SlotCalculator.Compute(school, totalSlots: 4);

        var lectivos = slots.Where(s => !s.IsBreak).ToList();
        lectivos.Should().HaveCount(4);
        lectivos[0].StartTime.Should().Be("09:00");
        lectivos[1].StartTime.Should().Be("15:00");
        lectivos[2].StartTime.Should().Be("16:00");
        lectivos[3].StartTime.Should().Be("17:00");
    }

    [Fact]
    public void ParseWorkingDays_ValidJson_ReturnsCorrectList()
    {
        var result = SlotCalculator.ParseWorkingDays("[1,2,3,4,5]");
        result.Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Fact]
    public void ParseWorkingDays_InvalidJson_ReturnsFallback()
    {
        var result = SlotCalculator.ParseWorkingDays("not-json");
        result.Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Fact]
    public void ParseWorkingDays_PartialWeek_ReturnsSubset()
    {
        var result = SlotCalculator.ParseWorkingDays("[1,2,4,5]");
        result.Should().BeEquivalentTo([1, 2, 4, 5]);
        result.Should().NotContain(3);
    }
}
