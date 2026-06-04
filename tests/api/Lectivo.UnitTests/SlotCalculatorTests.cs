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
        slots[0].Should().BeEquivalentTo(new SlotInfo(0, "09:00", "10:00", false, 540, 600));
        slots[1].Should().BeEquivalentTo(new SlotInfo(1, "10:00", "11:00", false, 600, 660));
        slots[2].Should().BeEquivalentTo(new SlotInfo(-1, "11:00", "11:30", true, 660, 690));
        slots[3].Should().BeEquivalentTo(new SlotInfo(2, "11:30", "12:30", false, 690, 750));
        slots[4].Should().BeEquivalentTo(new SlotInfo(3, "15:00", "16:00", false, 900, 960));
        slots[5].Should().BeEquivalentTo(new SlotInfo(4, "16:00", "17:00", false, 960, 1020));
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

    [Fact]
    public void Compute_MultipleBreaks_InsertsAllBreaks()
    {
        var breaks = new List<(int AfterSlot, int Minutes)> { (2, 15), (4, 20) };
        var slots = SlotCalculator.Compute(
            totalSlots: 5, slotMinutes: 60, breaks: breaks,
            afternoonSlots: 0, morningStart: new TimeOnly(9, 0));

        slots.Should().HaveCount(7);
        slots[0].Should().BeEquivalentTo(new SlotInfo(0, "09:00", "10:00", false, 540, 600));
        slots[1].Should().BeEquivalentTo(new SlotInfo(1, "10:00", "11:00", false, 600, 660));
        slots[2].IsBreak.Should().BeTrue();
        slots[2].StartTime.Should().Be("11:00");
        slots[2].EndTime.Should().Be("11:15");
        slots[2].StartMinute.Should().Be(660);
        slots[2].EndMinute.Should().Be(675);
        slots[3].Should().BeEquivalentTo(new SlotInfo(2, "11:15", "12:15", false, 675, 735));
        slots[4].Should().BeEquivalentTo(new SlotInfo(3, "12:15", "13:15", false, 735, 795));
        slots[5].IsBreak.Should().BeTrue();
        slots[5].StartTime.Should().Be("13:15");
        slots[5].EndTime.Should().Be("13:35");
        slots[5].StartMinute.Should().Be(795);
        slots[5].EndMinute.Should().Be(815);
        slots[6].Should().BeEquivalentTo(new SlotInfo(4, "13:35", "14:35", false, 815, 875));
    }

    [Fact]
    public void ComputeEndTime_MultipleBreaks_ReturnsCorrectEnd()
    {
        var breaks = new List<(int AfterSlot, int Minutes)> { (2, 15), (4, 20) };
        var end = SlotCalculator.ComputeEndTime(
            totalSlots: 5, slotMinutes: 60, breaks: breaks,
            afternoonSlots: 0, morningStart: new TimeOnly(9, 0));

        end.Should().Be(new TimeOnly(14, 35));
    }

    [Fact]
    public void Compute_FromCycleSchedule_IncludesBreaks()
    {
        var school = new School
        {
            Name = "Test", Slug = "test",
            SlotMinutes = 60, SlotsPerDay = 5,
            ScheduleType = "continua",
        };
        var cycle = new CycleSchedule
        {
            SchoolId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), Cycle = 1,
            MorningStart = new TimeOnly(9, 0),
        };
        cycle.Breaks.Add(new CycleBreak { CycleScheduleId = cycle.Id, AfterSlot = 2, Minutes = 30 });

        var slots = SlotCalculator.Compute(cycle, school);

        slots.Should().HaveCount(6);
        slots.Count(s => s.IsBreak).Should().Be(1);
        slots.First(s => s.IsBreak).StartTime.Should().Be("11:00");
        slots.First(s => s.IsBreak).EndTime.Should().Be("11:30");
    }

    [Fact]
    public void Compute_NoBreaks_ReturnsOnlyLectivoSlots()
    {
        var breaks = Array.Empty<(int AfterSlot, int Minutes)>();
        var slots = SlotCalculator.Compute(
            totalSlots: 5, slotMinutes: 60, breaks: breaks,
            afternoonSlots: 0, morningStart: new TimeOnly(9, 0));

        slots.Should().HaveCount(5);
        slots.Should().AllSatisfy(s => s.IsBreak.Should().BeFalse());
    }

    [Fact]
    public void Compute_StartMinuteEndMinute_AreCorrect()
    {
        var school = new School
        {
            Name = "Test", Slug = "test",
            MorningStart = new TimeOnly(8, 0),
            SlotMinutes = 45, BreakAfterSlot = 2, BreakMinutes = 15,
        };

        var slots = SlotCalculator.Compute(school, totalSlots: 4);

        slots.First(s => s.Index == 0).StartMinute.Should().Be(480);  // 8:00 = 480 min
        slots.First(s => s.Index == 0).EndMinute.Should().Be(525);    // 8:45 = 525 min
        slots.First(s => s.Index == 1).StartMinute.Should().Be(525);  // 8:45
        slots.First(s => s.Index == 1).EndMinute.Should().Be(570);    // 9:30
        var breakSlot = slots.First(s => s.IsBreak);
        breakSlot.StartMinute.Should().Be(570);                       // 9:30
        breakSlot.EndMinute.Should().Be(585);                         // 9:45
        slots.First(s => s.Index == 2).StartMinute.Should().Be(585);  // 9:45
        slots.First(s => s.Index == 2).EndMinute.Should().Be(630);    // 10:30
    }
}
