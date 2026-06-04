using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Infrastructure.Persistence;

public static class DbInitializer
{
    private static readonly Guid SchoolId    = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateId  = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid ProfUserId  = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid InfantilStageId   = Guid.Parse("00000000-0000-0000-0000-000000000030");
    private static readonly Guid PrimariaStageId   = Guid.Parse("00000000-0000-0000-0000-000000000031");
    private static readonly Guid SecundariaStageId = Guid.Parse("00000000-0000-0000-0000-000000000032");

    private static readonly Dictionary<string, string> SubjectShortNames = new()
    {
        ["len"] = "Lengua",
        ["mat"] = "Mates",
        ["cie"] = "Naturales",
        ["ing"] = "Inglés",
        ["ef"]  = "E. Física",
        ["mus"] = "Música",
        ["art"] = "Plástica",
        ["rel"] = "Religión",
        ["tut"] = "Libre",
    };

    private static readonly string[] TutorColorKeys = ["len", "mat", "cie", "art", "tut", "rel"];

    public static async Task InitializeAsync(AppDbContext db, SeedOptions? options = null)
    {
        await db.Database.MigrateAsync();
        if (await db.Schools.AnyAsync()) return;

        await SeedAsync(db, options ?? new SeedOptions());
    }

    public static async Task ReseedAsync(AppDbContext db, SeedOptions options)
    {
        await db.ScheduleConflicts.ExecuteDeleteAsync();
        await db.ScheduleEntries.ExecuteDeleteAsync();
        await db.Schedules.ExecuteDeleteAsync();
        await db.Assignments.ExecuteDeleteAsync();
        await db.TeacherConstraints.ExecuteDeleteAsync();
        await db.AppUsers.ExecuteDeleteAsync();
        await db.CourseGroups.ExecuteDeleteAsync();
        await db.Teachers.ExecuteDeleteAsync();
        await db.Classrooms.ExecuteDeleteAsync();
        await db.SubjectAllocations.ExecuteDeleteAsync();
        await db.CurriculumTemplates.ExecuteDeleteAsync();
        await db.PeriodAssignmentHours.ExecuteDeleteAsync();
        await db.CycleBreaks.ExecuteDeleteAsync();
        await db.CycleSchedules.ExecuteDeleteAsync();
        await db.SchoolPeriods.ExecuteDeleteAsync();
        await db.SchoolStages.ExecuteDeleteAsync();
        await db.Schools.ExecuteDeleteAsync();

        await SeedAsync(db, options);
    }

    private static async Task SeedAsync(AppDbContext db, SeedOptions opts)
    {
        var isBilingue = opts.Modality.Equals("bilingue", StringComparison.OrdinalIgnoreCase);
        var isPartida  = opts.ScheduleType.Equals("partida", StringComparison.OrdinalIgnoreCase);
        var totalGroups = opts.Levels * opts.LinesPerLevel;

        db.Schools.Add(new School
        {
            Id             = SchoolId,
            Name           = "CEIP Miguel Hernández",
            Slug           = "ceip-miguel-hernandez",
            CenterCode     = "28013291",
            Locality       = "Madrid",
            Community      = "madrid",
            Stage          = "primaria",
            MinCourseLevel = 1,
            MaxCourseLevel = opts.Levels,
            AcademicYear   = "2025/2026",
            ScheduleType   = opts.ScheduleType,
            MorningStart   = new TimeOnly(9, 0),
            AfternoonStart = isPartida ? new TimeOnly(15, 0) : null,
            SlotMinutes    = 60,
            BreakAfterSlot = 2,
            BreakMinutes   = LomloeMadrid.MinDailyBreakMinutes,
            SlotsPerDay    = 5,
            AfternoonSlots = isPartida ? 2 : 0,
            DaysPerWeek    = 5,
            WorkingDays    = "[1,2,3,4,5]",
        });

        // ── Etapas educativas (bloques) ──────────────────────────────────────
        var primariaStage = new SchoolStage
        {
            Id             = PrimariaStageId,
            SchoolId       = SchoolId,
            StageType      = StageTypes.Primaria,
            Name           = "Educación Primaria",
            MinLevel       = 1,
            MaxLevel       = opts.Levels,
            SortOrder      = 1,
            ScheduleType   = opts.ScheduleType,
            MorningStart   = new TimeOnly(9, 0),
            AfternoonStart = isPartida ? new TimeOnly(15, 0) : null,
            SlotMinutes    = 60,
            BreakAfterSlot = 2,
            BreakMinutes   = LomloeMadrid.MinDailyBreakMinutes,
            SlotsPerDay    = 5,
            AfternoonSlots = isPartida ? 2 : 0,
            DaysPerWeek    = 5,
            WorkingDays    = "[1,2,3,4,5]",
        };
        var infantilStage = new SchoolStage
        {
            Id        = InfantilStageId,
            SchoolId  = SchoolId,
            StageType = StageTypes.Infantil,
            Name      = "Educación Infantil",
            MinLevel  = 1,
            MaxLevel  = 3,
            SortOrder = 0,
        };
        var secundariaStage = new SchoolStage
        {
            Id        = SecundariaStageId,
            SchoolId  = SchoolId,
            StageType = StageTypes.Secundaria,
            Name      = "Educación Secundaria (ESO)",
            MinLevel  = 1,
            MaxLevel  = 4,
            SortOrder = 2,
        };
        db.SchoolStages.AddRange(primariaStage, infantilStage, secundariaStage);

        var defaultBreakList = new List<(int AfterSlot, int Minutes)> { (2, LomloeMadrid.MinDailyBreakMinutes) }.AsReadOnly();

        var ordinarioPeriod = new SchoolPeriod
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
            SchoolId = SchoolId,
            StageId = PrimariaStageId,
            Key = "ordinario",
            Name = "Jornada ordinaria",
            Months = "[10,11,12,1,2,3,4,5]",
            ScheduleType = opts.ScheduleType,
            SlotMinutes = 60,
            SlotsPerDay = 5,
            AfternoonSlots = isPartida ? 2 : 0,
            IsDefault = true,
            SortOrder = 0,
        };

        for (int c = 1; c <= 3; c++)
        {
            var cycleEnd = SlotCalculator.ComputeEndTime(
                totalSlots: 5,
                slotMinutes: 60,
                breaks: defaultBreakList,
                afternoonSlots: isPartida ? 2 : 0,
                morningStart: new TimeOnly(9, 0),
                afternoonStart: isPartida ? new TimeOnly(15, 0) : null,
                isPartida: isPartida);
            var cycleSchedule = new CycleSchedule
            {
                SchoolId       = SchoolId,
                StageId        = PrimariaStageId,
                PeriodId       = ordinarioPeriod.Id,
                Cycle          = c,
                MorningStart   = new TimeOnly(9, 0),
                EndTime        = cycleEnd,
                AfternoonStart = isPartida ? new TimeOnly(15, 0) : null,
            };
            cycleSchedule.Breaks.Add(new CycleBreak
            {
                CycleScheduleId = cycleSchedule.Id,
                AfterSlot = 2,
                Minutes = LomloeMadrid.MinDailyBreakMinutes,
            });
            ordinarioPeriod.Cycles.Add(cycleSchedule);
        }
        db.SchoolPeriods.Add(ordinarioPeriod);

        var junSepPeriod = new SchoolPeriod
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000021"),
            SchoolId = SchoolId,
            StageId = PrimariaStageId,
            Key = "jun-sep",
            Name = "Jornada de junio y septiembre",
            Months = "[6,9]",
            ScheduleType = "continua",
            SlotMinutes = 60,
            SlotsPerDay = 4,
            AfternoonSlots = 0,
            IsDefault = false,
            SortOrder = 1,
        };

        var junSepBreaks = new List<(int AfterSlot, int Minutes)> { (2, LomloeMadrid.MinDailyBreakMinutes) }.AsReadOnly();
        for (int c = 1; c <= 3; c++)
        {
            var cycleEnd = SlotCalculator.ComputeEndTime(
                totalSlots: 4,
                slotMinutes: 60,
                breaks: junSepBreaks,
                afternoonSlots: 0,
                morningStart: new TimeOnly(9, 0),
                afternoonStart: null,
                isPartida: false);
            var cycleSchedule = new CycleSchedule
            {
                SchoolId       = SchoolId,
                StageId        = PrimariaStageId,
                PeriodId       = junSepPeriod.Id,
                Cycle          = c,
                MorningStart   = new TimeOnly(9, 0),
                EndTime        = cycleEnd,
                AfternoonStart = null,
            };
            cycleSchedule.Breaks.Add(new CycleBreak
            {
                CycleScheduleId = cycleSchedule.Id,
                AfterSlot = 2,
                Minutes = LomloeMadrid.MinDailyBreakMinutes,
            });
            junSepPeriod.Cycles.Add(cycleSchedule);
        }
        db.SchoolPeriods.Add(junSepPeriod);

        db.AppUsers.Add(new AppUser
        {
            Id       = AdminUserId,
            Email    = "elena.castro@ceip-miguel-hernandez.es",
            FullName = "Elena Castro",
            SchoolId = SchoolId,
            Role     = "school_admin",
        });

        db.CurriculumTemplates.Add(new CurriculumTemplate
        {
            Id         = TemplateId,
            StageId    = PrimariaStageId,
            Name       = $"LOMLOE Madrid — Decreto 61/2022 (Primaria{(isBilingue ? ", sección bilingüe" : "")})",
            Region     = "madrid",
            Stage      = "primaria",
            IsOfficial = true,
        });

        var subjectNorms = LomloeMadrid.GetSubjects(opts.Modality);
        var allocations = subjectNorms.Select(n => new SubjectAllocation
        {
            Id                   = Guid.NewGuid(),
            TemplateId           = TemplateId,
            SubjectName          = n.SubjectName,
            SubjectShort         = SubjectShortNames.GetValueOrDefault(n.SubjectKey, n.SubjectKey),
            SubjectKey           = n.SubjectKey,
            WeeklyHoursMin       = n.MinH,
            WeeklyHoursMax       = n.MaxH,
            WeeklyHoursDefault   = n.DefaultH,
            RequiresSpecialist   = n.RequiresSpecialist,
            RequiredClassroomType = n.RequiredClassroomType,
            MaxConsecutiveSlots  = n.MaxConsecutiveSlots,
            SplittableAcrossDays = n.Splittable,
        }).ToList();
        db.SubjectAllocations.AddRange(allocations);

        var allocByKey = allocations.ToDictionary(a => a.SubjectKey);

        var classrooms = new List<Classroom>();
        char[] lineLabels = ['A', 'B', 'C', 'D', 'E'];

        for (int level = 1; level <= opts.Levels; level++)
        {
            for (int li = 0; li < opts.LinesPerLevel; li++)
            {
                classrooms.Add(new Classroom
                {
                    Id            = Guid.NewGuid(),
                    SchoolId      = SchoolId,
                    Name          = $"Aula {level}º{lineLabels[li]}",
                    ClassroomType = "regular",
                    Capacity      = 28,
                });
            }
        }

        var gymRooms = new[]
        {
            new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Gimnasio",            ClassroomType = "gym", Capacity = 60 },
            new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Pista Polideportiva", ClassroomType = "gym", Capacity = 80 },
            new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Patio Cubierto",      ClassroomType = "gym", Capacity = 60 },
        };
        classrooms.AddRange(gymRooms);

        var musicRoom = new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Aula de Música", ClassroomType = "music", Capacity = 30 };
        classrooms.Add(musicRoom);

        classrooms.Add(new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Aula de Informática", ClassroomType = "it", Capacity = 26 });

        db.Classrooms.AddRange(classrooms);

        var regularClassrooms = classrooms.Where(c => c.ClassroomType == "regular").ToList();

        var tutorNames = new[]
        {
            ("Ana García",         "ana.garcia"),
            ("Beatriz López",      "beatriz.lopez"),
            ("Carlos Martínez",    "carlos.martinez"),
            ("Diana Rodríguez",    "diana.rodriguez"),
            ("Eduardo Sánchez",    "eduardo.sanchez"),
            ("Fernanda Torres",    "fernanda.torres"),
            ("Guillermo Jiménez",  "guillermo.jimenez"),
            ("Helena Moreno",      "helena.moreno"),
            ("Ignacio Díaz",       "ignacio.diaz"),
            ("Julia Pérez",        "julia.perez"),
            ("Kevin Romero",       "kevin.romero"),
            ("Lorena Álvarez",     "lorena.alvarez"),
            ("Manuel Navarro",     "manuel.navarro"),
            ("Neus Serrano",       "neus.serrano"),
            ("Óscar Molina",       "oscar.molina"),
            ("Rafael Herrero",     "rafael.herrero"),
            ("Sara Vidal",         "sara.vidal"),
            ("Tomás Peña",         "tomas.pena"),
        };

        var tutors = new List<Teacher>();
        for (int i = 0; i < Math.Min(totalGroups, tutorNames.Length); i++)
        {
            var (name, emailUser) = tutorNames[i];
            tutors.Add(new Teacher
            {
                Id             = Guid.NewGuid(),
                SchoolId       = SchoolId,
                FullName       = name,
                Email          = $"{emailUser}@ceip-miguel-hernandez.es",
                TeacherType    = "definitivo",
                MaxWeeklyHours = 25,
                Specialties    = "[\"Generalista\"]",
                ColorKey       = TutorColorKeys[i % TutorColorKeys.Length],
            });
        }
        db.Teachers.AddRange(tutors);

        var ingNames = new[]
        {
            ("Laura Fernández",  "laura.fernandez"),
            ("David Soler",      "david.soler"),
            ("Sandra Blanco",    "sandra.blanco"),
            ("Alberto Rubio",    "alberto.rubio"),
            ("Marta Iglesias",   "marta.iglesias"),
        };
        var ingTeachers = ingNames.Select((t, _) => new Teacher
        {
            Id             = Guid.NewGuid(),
            SchoolId       = SchoolId,
            FullName       = t.Item1,
            Email          = $"{t.Item2}@ceip-miguel-hernandez.es",
            TeacherType    = "especialista",
            MaxWeeklyHours = 25,
            Specialties    = "[\"Inglés (habilitación)\"]",
            ColorKey       = "ing",
        }).ToList();
        db.Teachers.AddRange(ingTeachers);

        db.AppUsers.Add(new AppUser
        {
            Id        = ProfUserId,
            Email     = "laura.fernandez@ceip-miguel-hernandez.es",
            FullName  = "Laura Fernández",
            SchoolId  = SchoolId,
            Role      = "teacher",
            TeacherId = ingTeachers[0].Id,
        });

        var efNames = new[]
        {
            ("Javier Pardo",    "javier.pardo"),
            ("Patricia Castro", "patricia.castro"),
            ("Marcos Ruiz",     "marcos.ruiz"),
        };
        var efTeachers = efNames.Select(t => new Teacher
        {
            Id             = Guid.NewGuid(),
            SchoolId       = SchoolId,
            FullName       = t.Item1,
            Email          = $"{t.Item2}@ceip-miguel-hernandez.es",
            TeacherType    = "especialista",
            MaxWeeklyHours = 25,
            Specialties    = "[\"Educación Física\"]",
            ColorKey       = "ef",
        }).ToList();
        db.Teachers.AddRange(efTeachers);

        var musicTeacher = new Teacher
        {
            Id             = Guid.NewGuid(),
            SchoolId       = SchoolId,
            FullName       = "Lucía Navarro",
            Email          = "lucia.navarro@ceip-miguel-hernandez.es",
            TeacherType    = "definitivo",
            MaxWeeklyHours = 25,
            Specialties    = "[\"Generalista\",\"Música\"]",
            ColorKey       = "mus",
        };
        db.Teachers.Add(musicTeacher);

        var relTeacher = new Teacher
        {
            Id             = Guid.NewGuid(),
            SchoolId       = SchoolId,
            FullName       = "Pablo Vidal",
            Email          = "pablo.vidal@ceip-miguel-hernandez.es",
            TeacherType    = "especialista",
            MaxWeeklyHours = 20,
            Specialties    = "[\"Religión\"]",
            ColorKey       = "rel",
        };
        db.Teachers.Add(relTeacher);

        var groups = new List<CourseGroup>();
        int groupIdx = 0;

        for (int level = 1; level <= opts.Levels; level++)
        {
            for (int li = 0; li < opts.LinesPerLevel; li++)
            {
                var tutor     = groupIdx < tutors.Count ? tutors[groupIdx] : null;
                var classroom = groupIdx < regularClassrooms.Count ? regularClassrooms[groupIdx] : null;

                groups.Add(new CourseGroup
                {
                    Id              = Guid.NewGuid(),
                    SchoolId        = SchoolId,
                    StageId         = PrimariaStageId,
                    CourseLevel     = level,
                    GroupLabel      = lineLabels[li].ToString(),
                    StudentCount    = 25,
                    TutorId         = tutor?.Id,
                    HomeClassroomId = classroom?.Id,
                });
                groupIdx++;
            }
        }
        db.CourseGroups.AddRange(groups);

        var assignments = new List<Assignment>();
        void AddAssignment(Guid teacherId, Guid groupId, SubjectAllocation alloc, int hours) =>
            assignments.Add(new Assignment
            {
                Id          = Guid.NewGuid(),
                SchoolId    = SchoolId,
                TeacherId   = teacherId,
                GroupId     = groupId,
                AllocationId = alloc.Id,
                WeeklyHours = hours,
            });

        int ingSlots   = isBilingue ? 5 : 4;
        int ingMod     = isBilingue ? 5 : 3;

        for (int gi = 0; gi < groups.Count; gi++)
        {
            var group     = groups[gi];
            var tutor     = tutors[gi % tutors.Count];

            if (allocByKey.TryGetValue("len", out var lenAlloc)) AddAssignment(tutor.Id, group.Id, lenAlloc, 5);
            if (allocByKey.TryGetValue("mat", out var matAlloc)) AddAssignment(tutor.Id, group.Id, matAlloc, 5);
            if (allocByKey.TryGetValue("cie", out var cieAlloc)) AddAssignment(tutor.Id, group.Id, cieAlloc, 3);
            if (allocByKey.TryGetValue("art", out var artAlloc)) AddAssignment(tutor.Id, group.Id, artAlloc, 2);

            if (allocByKey.TryGetValue("ing", out var ingAlloc))
                AddAssignment(ingTeachers[gi % ingMod].Id, group.Id, ingAlloc, ingSlots);

            if (allocByKey.TryGetValue("ef", out var efAlloc))
                AddAssignment(efTeachers[gi % efTeachers.Count].Id, group.Id, efAlloc, 3);

            if (allocByKey.TryGetValue("mus", out var musAlloc))
                AddAssignment(musicTeacher.Id, group.Id, musAlloc, 1);

            if (allocByKey.TryGetValue("rel", out var relAlloc))
                AddAssignment(relTeacher.Id, group.Id, relAlloc, 1);
        }
        db.Assignments.AddRange(assignments);

        // ── Esqueletos de Infantil y Secundaria (estructura, sin asignaciones) ──
        AddStageSkeleton(db, infantilStage, cycles: 1);
        AddStageSkeleton(db, secundariaStage, cycles: 2);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Crea la estructura mínima de una etapa: un periodo ordinario con sus ciclos
    /// y un grupo (línea A) por nivel. Sin profesores ni asignaciones todavía.
    /// </summary>
    private static void AddStageSkeleton(AppDbContext db, SchoolStage stage, int cycles)
    {
        var breaks = new List<(int AfterSlot, int Minutes)> { (2, LomloeMadrid.MinDailyBreakMinutes) }.AsReadOnly();

        var period = new SchoolPeriod
        {
            Id = Guid.NewGuid(),
            SchoolId = SchoolId,
            StageId = stage.Id,
            Key = "ordinario",
            Name = "Jornada ordinaria",
            Months = "[10,11,12,1,2,3,4,5]",
            ScheduleType = "continua",
            SlotMinutes = 60,
            SlotsPerDay = stage.SlotsPerDay,
            AfternoonSlots = 0,
            IsDefault = true,
            SortOrder = 0,
        };

        for (int c = 1; c <= cycles; c++)
        {
            var cycleEnd = SlotCalculator.ComputeEndTime(
                totalSlots: stage.SlotsPerDay,
                slotMinutes: 60,
                breaks: breaks,
                afternoonSlots: 0,
                morningStart: new TimeOnly(9, 0),
                afternoonStart: null,
                isPartida: false);
            var cs = new CycleSchedule
            {
                SchoolId       = SchoolId,
                StageId        = stage.Id,
                PeriodId       = period.Id,
                Cycle          = c,
                MorningStart   = new TimeOnly(9, 0),
                EndTime        = cycleEnd,
                AfternoonStart = null,
            };
            cs.Breaks.Add(new CycleBreak { CycleScheduleId = cs.Id, AfterSlot = 2, Minutes = LomloeMadrid.MinDailyBreakMinutes });
            period.Cycles.Add(cs);
        }
        db.SchoolPeriods.Add(period);

        for (int level = stage.MinLevel; level <= stage.MaxLevel; level++)
        {
            db.CourseGroups.Add(new CourseGroup
            {
                Id           = Guid.NewGuid(),
                SchoolId     = SchoolId,
                StageId      = stage.Id,
                CourseLevel  = level,
                GroupLabel   = "A",
                StudentCount = 22,
            });
        }
    }
}
