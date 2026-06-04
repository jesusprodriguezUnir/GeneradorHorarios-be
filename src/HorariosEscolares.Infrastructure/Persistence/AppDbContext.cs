using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<School> Schools => Set<School>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<CurriculumTemplate> CurriculumTemplates => Set<CurriculumTemplate>();
    public DbSet<SubjectAllocation> SubjectAllocations => Set<SubjectAllocation>();
    public DbSet<CourseGroup> CourseGroups => Set<CourseGroup>();
    public DbSet<GroupSubjectHour> GroupSubjectHours => Set<GroupSubjectHour>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<TeacherConstraint> TeacherConstraints => Set<TeacherConstraint>();
    public DbSet<ScheduleRecord> Schedules => Set<ScheduleRecord>();
    public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();
    public DbSet<ScheduleConflictRecord> ScheduleConflicts => Set<ScheduleConflictRecord>();
    public DbSet<CycleSchedule> CycleSchedules => Set<CycleSchedule>();
    public DbSet<CycleBreak> CycleBreaks => Set<CycleBreak>();
    public DbSet<SchoolPeriod> SchoolPeriods => Set<SchoolPeriod>();
    public DbSet<PeriodAssignmentHours> PeriodAssignmentHours => Set<PeriodAssignmentHours>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<School>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            e.Property(x => x.CenterCode).HasMaxLength(20);
            e.Property(x => x.Locality).HasMaxLength(150);
            e.Property(x => x.Community).HasMaxLength(50).HasDefaultValue("madrid");
            e.Property(x => x.Stage).HasMaxLength(30).HasDefaultValue("primaria");
            e.Property(x => x.AcademicYear).HasMaxLength(20).HasDefaultValue("2025/2026");
            e.Property(x => x.ScheduleType).HasMaxLength(20).HasDefaultValue("continua");
            e.Property(x => x.MorningStart).HasColumnType("time");
            e.Property(x => x.AfternoonStart).HasColumnType("time");
            e.Property(x => x.WorkingDays).HasMaxLength(100).HasDefaultValue("[1,2,3,4,5]");
        });

        mb.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasMaxLength(30).IsRequired();
        });

        mb.Entity<Teacher>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SchoolId, x.Email }).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.TeacherType).HasMaxLength(30).HasDefaultValue("definitivo");
            e.Property(x => x.ColorKey).HasMaxLength(10).HasDefaultValue("mat");
            e.Property(x => x.Specialties).HasMaxLength(500).HasDefaultValue("[]");
        });

        mb.Entity<Classroom>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.ClassroomType).HasMaxLength(20).HasDefaultValue("regular");
        });

        mb.Entity<CurriculumTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Region).HasMaxLength(50).HasDefaultValue("madrid");
            e.Property(x => x.Stage).HasMaxLength(50).HasDefaultValue("primaria");
        });

        mb.Entity<SubjectAllocation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SubjectName).HasMaxLength(150).IsRequired();
            e.Property(x => x.SubjectShort).HasMaxLength(50);
            e.Property(x => x.SubjectKey).HasMaxLength(10);
            e.Property(x => x.RequiredClassroomType).HasMaxLength(20);
        });

        mb.Entity<CourseGroup>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SchoolId, x.CourseLevel, x.GroupLabel }).IsUnique();
            e.Property(x => x.GroupLabel).HasMaxLength(5).IsRequired();
            e.Ignore(x => x.DisplayName);
            e.Ignore(x => x.Cycle);
        });

        mb.Entity<GroupSubjectHour>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.GroupId, x.SubjectKey }).IsUnique();
            e.Property(x => x.SubjectKey).HasMaxLength(10).IsRequired();
            e.HasOne(x => x.Group)
             .WithMany(x => x.SubjectHoursList)
             .HasForeignKey(x => x.GroupId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Assignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
            e.HasIndex(x => new { x.TeacherId, x.GroupId, x.AllocationId }).IsUnique();
        });

        mb.Entity<TeacherConstraint>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
            e.Property(x => x.ConstraintType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(300);
        });

        mb.Entity<ScheduleRecord>(e =>
        {
            e.ToTable("Schedules");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
            e.Property(x => x.AcademicYear).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("draft");
        });

        mb.Entity<ScheduleEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
        });

        mb.Entity<ScheduleConflictRecord>(e =>
        {
            e.ToTable("ScheduleConflicts");
            e.HasKey(x => x.Id);
            e.Property(x => x.ConflictType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.Suggestions).HasMaxLength(2000).HasDefaultValue("[]");
        });

        mb.Entity<SchoolPeriod>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
            e.HasIndex(x => new { x.SchoolId, x.Key }).IsUnique();
            e.Property(x => x.Key).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Months).HasMaxLength(100).HasDefaultValue("[10,11,12,1,2,3,4,5]");
            e.Property(x => x.ScheduleType).HasMaxLength(20).HasDefaultValue("continua");
            e.HasMany(x => x.Cycles)
             .WithOne(x => x.Period)
             .HasForeignKey(x => x.PeriodId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PeriodAssignmentHours>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PeriodId, x.AssignmentId }).IsUnique();
        });

        mb.Entity<CycleSchedule>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PeriodId, x.Cycle }).IsUnique();
            e.HasIndex(x => new { x.SchoolId, x.Cycle });
            e.Property(x => x.MorningStart).HasColumnType("time");
            e.Property(x => x.EndTime).HasColumnType("time");
            e.Property(x => x.AfternoonStart).HasColumnType("time");
            e.HasMany(x => x.Breaks)
             .WithOne(x => x.Cycle)
             .HasForeignKey(x => x.CycleScheduleId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<CycleBreak>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CycleScheduleId);
            e.Property(x => x.Minutes).HasDefaultValue(30);
        });
    }
}
