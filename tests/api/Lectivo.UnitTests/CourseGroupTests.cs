using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class CycleResolverTests
{
    private static readonly CycleResolver Resolver = new();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    public void Primaria_FromCourseLevel_ReturnsCorrectCycle(int courseLevel, int expectedCycle)
    {
        Resolver.ResolveCycle(StageTypes.Primaria, courseLevel).Should().Be(expectedCycle);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Infantil_AllLevels_ReturnSingleCycle(int courseLevel)
    {
        Resolver.ResolveCycle(StageTypes.Infantil, courseLevel).Should().Be(1);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    public void Secundaria_FromCourseLevel_ReturnsCorrectCycle(int courseLevel, int expectedCycle)
    {
        Resolver.ResolveCycle(StageTypes.Secundaria, courseLevel).Should().Be(expectedCycle);
    }
}
