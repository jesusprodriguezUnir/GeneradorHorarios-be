using HorariosEscolares.Domain.Entities;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class CourseGroupTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    public void Cycle_FromCourseLevel_ReturnsCorrectCycle(int courseLevel, int expectedCycle)
    {
        var group = new CourseGroup
        {
            SchoolId = Guid.NewGuid(),
            CourseLevel = courseLevel,
            GroupLabel = "A",
        };

        group.Cycle.Should().Be(expectedCycle);
    }
}