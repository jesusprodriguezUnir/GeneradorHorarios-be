using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Abstractions;

public interface IAppDbContext
{
    DbSet<School> Schools { get; }
    DbSet<SchoolStage> SchoolStages { get; }
    DbSet<AppUser> AppUsers { get; }
    DbSet<Teacher> Teachers { get; }
    DbSet<Classroom> Classrooms { get; }
    DbSet<CurriculumTemplate> CurriculumTemplates { get; }
    DbSet<SubjectAllocation> SubjectAllocations { get; }
    DbSet<CourseGroup> CourseGroups { get; }
    DbSet<GroupSubjectHour> GroupSubjectHours { get; }
    DbSet<Assignment> Assignments { get; }
    DbSet<TeacherConstraint> TeacherConstraints { get; }
    DbSet<ScheduleRecord> Schedules { get; }
    DbSet<ScheduleEntry> ScheduleEntries { get; }
    DbSet<ScheduleConflictRecord> ScheduleConflicts { get; }
    DbSet<CycleSchedule> CycleSchedules { get; }
    DbSet<CycleBreak> CycleBreaks { get; }
    DbSet<SchoolPeriod> SchoolPeriods { get; }
    DbSet<PeriodAssignmentHours> PeriodAssignmentHours { get; }
    DbSet<TeacherStageAssignment> TeacherStageAssignments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
