using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<School> Schools => Set<School>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<CurriculumTemplate> CurriculumTemplates => Set<CurriculumTemplate>();
    public DbSet<SubjectAllocation> SubjectAllocations => Set<SubjectAllocation>();
    public DbSet<CourseGroup> CourseGroups => Set<CourseGroup>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<TeacherConstraint> TeacherConstraints => Set<TeacherConstraint>();
    public DbSet<ScheduleRecord> Schedules => Set<ScheduleRecord>();
    public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();
    public DbSet<ScheduleConflictRecord> ScheduleConflicts => Set<ScheduleConflictRecord>();
    public DbSet<CycleSchedule> CycleSchedules => Set<CycleSchedule>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── School ───────────────────────────────────────────────────────────
        mb.Entity<School>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            // Identificación
            e.Property(x => x.CenterCode).HasMaxLength(20);
            e.Property(x => x.Locality).HasMaxLength(150);
            e.Property(x => x.Community).HasMaxLength(50).HasDefaultValue("madrid");
            e.Property(x => x.Stage).HasMaxLength(30).HasDefaultValue("primaria");
            e.Property(x => x.AcademicYear).HasMaxLength(20).HasDefaultValue("2025/2026");
            // Jornada
            e.Property(x => x.ScheduleType).HasMaxLength(20).HasDefaultValue("continua");
            e.Property(x => x.MorningStart).HasColumnType("time");
            e.Property(x => x.AfternoonStart).HasColumnType("time");
            e.Property(x => x.WorkingDays).HasMaxLength(100).HasDefaultValue("[1,2,3,4,5]");
        });

        // ── AppUser ──────────────────────────────────────────────────────────
        mb.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasMaxLength(30).IsRequired();
        });

        // ── Teacher ──────────────────────────────────────────────────────────
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

        // ── Classroom ────────────────────────────────────────────────────────
        mb.Entity<Classroom>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.ClassroomType).HasMaxLength(20).HasDefaultValue("regular");
        });

        // ── CurriculumTemplate ───────────────────────────────────────────────
        mb.Entity<CurriculumTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Region).HasMaxLength(50).HasDefaultValue("madrid");
            e.Property(x => x.Stage).HasMaxLength(50).HasDefaultValue("primaria");
        });

        // ── SubjectAllocation ────────────────────────────────────────────────
        mb.Entity<SubjectAllocation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SubjectName).HasMaxLength(150).IsRequired();
            e.Property(x => x.SubjectShort).HasMaxLength(50);
            e.Property(x => x.SubjectKey).HasMaxLength(10);
            e.Property(x => x.RequiredClassroomType).HasMaxLength(20);
        });

        // ── CourseGroup ──────────────────────────────────────────────────────
        mb.Entity<CourseGroup>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SchoolId, x.CourseLevel, x.GroupLabel }).IsUnique();
            e.Property(x => x.GroupLabel).HasMaxLength(5).IsRequired();
            e.Ignore(x => x.DisplayName);
        });

        // ── Assignment ───────────────────────────────────────────────────────
        mb.Entity<Assignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
            e.HasIndex(x => new { x.TeacherId, x.GroupId, x.AllocationId }).IsUnique();
        });

        // ── TeacherConstraint ────────────────────────────────────────────────
        mb.Entity<TeacherConstraint>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
            e.Property(x => x.ConstraintType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(300);
        });

        // ── ScheduleRecord ───────────────────────────────────────────────────
        mb.Entity<ScheduleRecord>(e =>
        {
            e.ToTable("Schedules");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
            e.Property(x => x.AcademicYear).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("draft");
        });

        // ── ScheduleEntry ────────────────────────────────────────────────────
        mb.Entity<ScheduleEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId);
        });

        // ── ScheduleConflictRecord ───────────────────────────────────────────
        mb.Entity<ScheduleConflictRecord>(e =>
        {
            e.ToTable("ScheduleConflicts");
            e.HasKey(x => x.Id);
            e.Property(x => x.ConflictType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.Suggestions).HasMaxLength(2000).HasDefaultValue("[]");
        });

        // ── CycleSchedule ────────────────────────────────────────────────────
        mb.Entity<CycleSchedule>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SchoolId, x.Cycle }).IsUnique();
            e.Property(x => x.MorningStart).HasColumnType("time");
            e.Property(x => x.EndTime).HasColumnType("time");
            e.Property(x => x.AfternoonStart).HasColumnType("time");
        });
    }
}
