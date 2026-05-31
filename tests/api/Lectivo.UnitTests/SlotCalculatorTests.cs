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
        };

        var dto = SlotCalculator.ToDto(school);

        dto.Id.Should().Be(school.Id);
        dto.Name.Should().Be("Test School");
        dto.MorningStart.Should().Be("09:00");
        dto.ComputedSlots.Should().NotBeEmpty();
    }
}
