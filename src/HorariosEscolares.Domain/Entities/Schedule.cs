namespace HorariosEscolares.Domain.Entities;

public enum ScheduleStatus { Draft, Generated, Published, Archived }
public enum ConflictType { Teacher, Classroom, Normative, Soft, Coverage }
public enum ConflictSeverity { Error, Warning }

public class Schedule
{
    public Guid Id { get; private set; }
    public Guid SchoolId { get; private set; }
    public string AcademicYear { get; private set; } = string.Empty;
    public ScheduleStatus Status { get; private set; }
    public DateTime? GeneratedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int TotalConflicts { get; private set; }

    private readonly List<ScheduleSlot> _entries = [];
    private readonly List<ScheduleIssue> _conflicts = [];

    public IReadOnlyList<ScheduleSlot> Entries => _entries.AsReadOnly();
    public IReadOnlyList<ScheduleIssue> Conflicts => _conflicts.AsReadOnly();

    private Schedule() { }

    public static Schedule Create(Guid schoolId, string academicYear)
        => new()
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            AcademicYear = academicYear,
            Status = ScheduleStatus.Draft
        };

    public void SetGenerated(IEnumerable<ScheduleSlot> entries, IEnumerable<ScheduleIssue> conflicts)
    {
        if (Status != ScheduleStatus.Draft)
            throw new InvalidOperationException("Solo se puede completar un horario en estado Draft.");

        _entries.Clear();
        _entries.AddRange(entries);
        _conflicts.Clear();
        _conflicts.AddRange(conflicts);

        TotalConflicts = _conflicts.Count(c => c.Severity == ConflictSeverity.Error);
        GeneratedAt = DateTime.UtcNow;
        Status = ScheduleStatus.Generated;
    }

    public void Publish()
    {
        if (Status != ScheduleStatus.Generated)
            throw new InvalidOperationException("Solo se puede publicar un horario generado.");
        Status = ScheduleStatus.Published;
        PublishedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status != ScheduleStatus.Published)
            throw new InvalidOperationException("Solo se puede archivar un horario publicado.");
        Status = ScheduleStatus.Archived;
    }
}

public record ScheduleSlot(
    Guid Id, Guid ScheduleId, Guid SchoolId, Guid GroupId,
    Guid AllocationId, Guid TeacherId, Guid ClassroomId,
    int DayOfWeek, int SlotIndex, bool IsManualOverride = false);

public record ScheduleIssue(
    Guid Id, Guid ScheduleId, ConflictType Type, ConflictSeverity Severity,
    string Description, string[] Suggestions, Guid? GroupId = null,
    Guid? TeacherId = null, int? DayOfWeek = null, int? SlotIndex = null);
