using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class ConflictExplanationBuilderTests
{
    [Fact]
    public void Build_ForUnassignedSession_ReturnsExplanation()
    {
        var session = TestData.Session(subjectName: "Matemáticas", groupLabel: "3A");
        var context = TestData.Context();

        var explanation = ConflictExplanationBuilder.Build(session, [], context);

        explanation.Should().NotBeNull();
        explanation.Description.Should().Contain("Matemáticas");
        explanation.Description.Should().Contain("3A");
        explanation.Suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public void Build_ForTeacherOverloaded_ReturnsTeacherConflict()
    {
        var teacher = TestData.Teacher1Id;
        var assigned = Enumerable.Range(0, 23)
            .Select(i => new AssignedSlot(
                Guid.NewGuid(), TestData.Group1Id, teacher,
                TestData.Allocation1Id, TestData.RegularClassroomId,
                (i % 5) + 1, i % 5))
            .ToList();

        var session = TestData.Session(teacherId: teacher, subjectName: "Lengua", groupLabel: "1A");
        var context = TestData.Context();

        var explanation = ConflictExplanationBuilder.Build(session, assigned, context);

        explanation.Type.Should().Be(ConflictType.Teacher);
        explanation.Severity.Should().Be(ConflictSeverity.Error);
        explanation.Description.Should().Contain("profesor ya tiene");
    }

    [Fact]
    public void Build_ForRequiredSpecialClassroom_ReturnsClassroomConflict()
    {
        var session = TestData.Session(requiredClassroomType: ClassroomType.Gym, subjectName: "E. Física");
        var context = TestData.Context();

        var explanation = ConflictExplanationBuilder.Build(session, [], context);

        explanation.Type.Should().Be(ConflictType.Classroom);
        explanation.Severity.Should().Be(ConflictSeverity.Error);
        explanation.Description.Should().Contain("aulas de tipo");
    }
}
