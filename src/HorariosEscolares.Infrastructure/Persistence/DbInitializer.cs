using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Infrastructure.Persistence;

public static class DbInitializer
{
    private static readonly Guid SchoolId    = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateId  = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid InfantilTemplateId   = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid SecundariaTemplateId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid AdminUserId          = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid ProfUserId           = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid JefeEstudiosUserId    = Guid.Parse("00000000-0000-0000-0000-000000000012");
    private static readonly Guid SecretarioUserId      = Guid.Parse("00000000-0000-0000-0000-000000000013");
    private static readonly Guid TutorUserId           = Guid.Parse("00000000-0000-0000-0000-000000000014");
    private static readonly Guid CoordinadorCicloUserId = Guid.Parse("00000000-0000-0000-0000-000000000015");
    private static readonly Guid OrientadorUserId      = Guid.Parse("00000000-0000-0000-0000-000000000016");
    private static readonly Guid InfantilStageId   = Guid.Parse("00000000-0000-0000-0000-000000000030");
    private static readonly Guid PrimariaStageId   = Guid.Parse("00000000-0000-0000-0000-000000000031");
    private static readonly Guid SecundariaStageId = Guid.Parse("00000000-0000-0000-0000-000000000032");
    private static readonly Guid InfantilPeriodId  = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private static readonly Guid SecundariaPeriodId = Guid.Parse("00000000-0000-0000-0000-000000000023");

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
        ["crec"] = "Crecimiento",
        ["desc"] = "Entorno",
        ["com"]  = "Lenguajes",
        ["gh"]   = "Geografía",
        ["bg"]   = "Biología",
        ["fq"]   = "Física/Q.",
        ["tec"]  = "Tecnología",
        ["val"]  = "Valores",
        ["opt"]  = "Optativa",
    };

    private static readonly string[] TutorColorKeys = ["len", "mat", "cie", "art", "tut", "rel"];

    private const string EmailDomain = "ceipso-antonio-machado.es";

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
        await db.Roles.ExecuteDeleteAsync();
        await db.CourseGroups.ExecuteDeleteAsync();
        await db.TeacherSubjectHours.ExecuteDeleteAsync();
        await db.TeacherStageAssignments.ExecuteDeleteAsync();
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
        var primariaLines = opts.PrimariaLines ?? opts.LinesPerLevel;
        var totalPrimariaGroups = opts.Levels * primariaLines;

        SeedRoles(db);

        db.Schools.Add(new School
        {
            Id             = SchoolId,
            Name           = "CEIPSO Antonio Machado",
            Slug           = "ceipso-antonio-machado",
            CenterCode     = "28099001",
            Locality       = "Alcalá de Henares",
            Community      = "madrid",
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
            Id             = InfantilStageId,
            SchoolId       = SchoolId,
            StageType      = StageTypes.Infantil,
            Name           = "Educación Infantil",
            MinLevel       = 1,
            MaxLevel       = 3,
            SortOrder      = 0,
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
        var secundariaStage = new SchoolStage
        {
            Id             = SecundariaStageId,
            SchoolId       = SchoolId,
            StageType      = StageTypes.Secundaria,
            Name           = "Educación Secundaria (ESO)",
            MinLevel       = 1,
            MaxLevel       = 4,
            SortOrder      = 2,
            ScheduleType   = "continua",
            MorningStart   = new TimeOnly(8, 30),
            AfternoonStart = null,
            SlotMinutes    = 60,
            BreakAfterSlot = 3,
            BreakMinutes   = LomloeMadrid.MinDailyBreakMinutes,
            SlotsPerDay    = 6,
            AfternoonSlots = 0,
            DaysPerWeek    = 5,
            WorkingDays    = "[1,2,3,4,5]",
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

        // Jornada ordinaria: continua 09:00–14:00 | partida 09:00–13:00 + 15:00–17:00
        for (int c = 1; c <= 3; c++)
        {
            var morningEnd   = isPartida ? new TimeOnly(13, 0) : new TimeOnly(14, 0);
            var afternoonEnd = isPartida ? new TimeOnly(17, 0) : (TimeOnly?)null;
            var cycleSchedule = new CycleSchedule
            {
                SchoolId       = SchoolId,
                StageId        = PrimariaStageId,
                PeriodId       = ordinarioPeriod.Id,
                Cycle          = c,
                MorningStart   = new TimeOnly(9, 0),
                MorningEnd     = morningEnd,
                AfternoonStart = isPartida ? new TimeOnly(15, 0) : null,
                AfternoonEnd   = afternoonEnd,
                EndTime        = afternoonEnd ?? morningEnd,
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

        // Jornada junio/septiembre: continua 09:00–13:00 (4 franjas × 60 min = 4 h lectivas)
        for (int c = 1; c <= 3; c++)
        {
            var cycleSchedule = new CycleSchedule
            {
                SchoolId       = SchoolId,
                StageId        = PrimariaStageId,
                PeriodId       = junSepPeriod.Id,
                Cycle          = c,
                MorningStart   = new TimeOnly(9, 0),
                MorningEnd     = new TimeOnly(13, 0),
                AfternoonStart = null,
                AfternoonEnd   = null,
                EndTime        = new TimeOnly(13, 0),
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
            Email    = $"elena.castro@{EmailDomain}",
            FullName = "Elena Castro",
            SchoolId = SchoolId,
            RoleId   = RoleIds.Director,
        });

        // --- Primaria Template & Allocations ---
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
            Cycle                = n.Cycle,
            CourseLevel          = n.CourseLevel,
        }).ToList();
        db.SubjectAllocations.AddRange(allocations);

        // --- Infantil Template & Allocations ---
        db.CurriculumTemplates.Add(new CurriculumTemplate
        {
            Id         = InfantilTemplateId,
            StageId    = InfantilStageId,
            Name       = "LOMLOE Madrid — Decreto 36/2022 (Infantil)",
            Region     = "madrid",
            Stage      = "infantil",
            IsOfficial = true,
        });

        var infantilNorm = new InfantilNormative();
        var infantilSubjectNorms = infantilNorm.GetSubjects(opts.Modality);
        var infantilAllocations = infantilSubjectNorms.Select(n => new SubjectAllocation
        {
            Id                   = Guid.NewGuid(),
            TemplateId           = InfantilTemplateId,
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
            Cycle                = n.Cycle,
            CourseLevel          = n.CourseLevel,
        }).ToList();
        db.SubjectAllocations.AddRange(infantilAllocations);

        // --- Secundaria Template & Allocations ---
        db.CurriculumTemplates.Add(new CurriculumTemplate
        {
            Id         = SecundariaTemplateId,
            StageId    = SecundariaStageId,
            Name       = "LOMLOE Madrid — Decreto 65/2022 (Secundaria)",
            Region     = "madrid",
            Stage      = "secundaria",
            IsOfficial = true,
        });

        var secundariaNorm = new SecundariaNormative();
        var secundariaSubjectNorms = secundariaNorm.GetSubjects(opts.Modality);
        var secundariaAllocations = secundariaSubjectNorms.Select(n => new SubjectAllocation
        {
            Id                   = Guid.NewGuid(),
            TemplateId           = SecundariaTemplateId,
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
            Cycle                = n.Cycle,
            CourseLevel          = n.CourseLevel,
        }).ToList();
        db.SubjectAllocations.AddRange(secundariaAllocations);

        var classrooms = new List<Classroom>();
        char[] lineLabels = ['A', 'B', 'C', 'D', 'E'];

        for (int level = 1; level <= opts.Levels; level++)
        {
            for (int li = 0; li < primariaLines; li++)
            {
                classrooms.Add(new Classroom
                {
                    Id            = Guid.NewGuid(),
                    SchoolId      = SchoolId,
                    Name          = $"Aula {level}º{lineLabels[li]}",
                    ClassroomType = "regular",
                    Capacity      = 28,
                    StageId       = PrimariaStageId,
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
        for (int i = 0; i < Math.Min(totalPrimariaGroups, tutorNames.Length); i++)
        {
            var (name, emailUser) = tutorNames[i];
            tutors.Add(new Teacher
            {
                Id             = Guid.NewGuid(),
                SchoolId       = SchoolId,
                FullName       = name,
                Email          = $"{emailUser}@{EmailDomain}",
                TeacherType    = "definitivo",
                MaxWeeklyHours = 25,
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
            Email          = $"{t.Item2}@{EmailDomain}",
            TeacherType    = "especialista",
            MaxWeeklyHours = 25,
            ColorKey       = "ing",
        }).ToList();
        db.Teachers.AddRange(ingTeachers);

        db.AppUsers.Add(new AppUser
        {
            Id        = ProfUserId,
            Email     = $"laura.fernandez@{EmailDomain}",
            FullName  = "Laura Fernández",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.Profesor,
            TeacherId = ingTeachers[0].Id,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = JefeEstudiosUserId,
            Email     = $"maria.garcia@{EmailDomain}",
            FullName  = "María García",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.JefeEstudios,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = SecretarioUserId,
            Email     = $"antonio.lopez@{EmailDomain}",
            FullName  = "Antonio López",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.Secretario,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = TutorUserId,
            Email     = $"ana.garcia@{EmailDomain}",
            FullName  = "Ana García",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.Tutor,
            TeacherId = tutors[0].Id,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = CoordinadorCicloUserId,
            Email     = $"beatriz.lopez@{EmailDomain}",
            FullName  = "Beatriz López",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.CoordinadorCiclo,
            TeacherId = tutors[1].Id,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = OrientadorUserId,
            Email     = $"lucia.martin@{EmailDomain}",
            FullName  = "Lucía Martín",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.Orientador,
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
            Email          = $"{t.Item2}@{EmailDomain}",
            TeacherType    = "especialista",
            MaxWeeklyHours = 25,
            ColorKey       = "ef",
        }).ToList();
        db.Teachers.AddRange(efTeachers);

        var musicTeacher = new Teacher
        {
            Id             = Guid.NewGuid(),
            SchoolId       = SchoolId,
            FullName       = "Lucía Navarro",
            Email          = $"lucia.navarro@{EmailDomain}",
            TeacherType    = "definitivo",
            MaxWeeklyHours = 25,
            ColorKey       = "mus",
        };
        db.Teachers.Add(musicTeacher);

        var relTeacher = new Teacher
        {
            Id             = Guid.NewGuid(),
            SchoolId       = SchoolId,
            FullName       = "Pablo Vidal",
            Email          = $"pablo.vidal@{EmailDomain}",
            TeacherType    = "especialista",
            MaxWeeklyHours = 20,
            ColorKey       = "rel",
        };
        db.Teachers.Add(relTeacher);

        var groups = new List<CourseGroup>();
        int groupIdx = 0;

        for (int level = 1; level <= opts.Levels; level++)
        {
            for (int li = 0; li < primariaLines; li++)
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

        SubjectAllocation? GetAlloc(string key, int courseLevel)
        {
            int cycle = (courseLevel + 1) / 2;
            return allocations.FirstOrDefault(a => a.SubjectKey == key && (a.Cycle == cycle || a.CourseLevel == courseLevel))
                ?? allocations.FirstOrDefault(a => a.SubjectKey == key);
        }

        for (int gi = 0; gi < groups.Count; gi++)
        {
            var group     = groups[gi];
            var tutor     = tutors[gi % tutors.Count];

            var lenAlloc = GetAlloc("len", group.CourseLevel); if (lenAlloc != null) AddAssignment(tutor.Id, group.Id, lenAlloc, 5);
            var matAlloc = GetAlloc("mat", group.CourseLevel); if (matAlloc != null) AddAssignment(tutor.Id, group.Id, matAlloc, 5);
            var cieAlloc = GetAlloc("cie", group.CourseLevel); if (cieAlloc != null) AddAssignment(tutor.Id, group.Id, cieAlloc, 3);
            var artAlloc = GetAlloc("art", group.CourseLevel); if (artAlloc != null) AddAssignment(tutor.Id, group.Id, artAlloc, 2);

            var ingAlloc = GetAlloc("ing", group.CourseLevel); if (ingAlloc != null)
                AddAssignment(ingTeachers[gi % ingMod].Id, group.Id, ingAlloc, ingSlots);

            var efAlloc = GetAlloc("ef", group.CourseLevel); if (efAlloc != null)
                AddAssignment(efTeachers[gi % efTeachers.Count].Id, group.Id, efAlloc, 3);

            var musAlloc = GetAlloc("mus", group.CourseLevel); if (musAlloc != null)
                AddAssignment(musicTeacher.Id, group.Id, musAlloc, 1);

            var relAlloc = GetAlloc("rel", group.CourseLevel); if (relAlloc != null)
                AddAssignment(relTeacher.Id, group.Id, relAlloc, 1);
                
            var valAlloc = GetAlloc("val", group.CourseLevel); if (valAlloc != null)
                AddAssignment(tutor.Id, group.Id, valAlloc, 1);
        }
        db.Assignments.AddRange(assignments);

        // ── Asociar profesores a etapas/ciclos ──────────────────────────────
        var stageAssignments = new List<TeacherStageAssignment>();

        // Tutores de Primaria: asignar ciclo según niveles que imparten.
        // Índices 0-5 (niveles 1-2) → Ciclo 1, 6-11 (niveles 3-4) → Ciclo 2, 12-17 (niveles 5-6) → Ciclo 3.
        for (int i = 0; i < tutors.Count; i++)
        {
            stageAssignments.Add(new TeacherStageAssignment
            {
                TeacherId = tutors[i].Id,
                StageId   = PrimariaStageId,
                Cycle     = (i / 6) + 1,
            });
        }

        // Especialistas de inglés: todos los ciclos de Primaria de forma explícita
        foreach (var t in ingTeachers)
        {
            for (int c = 1; c <= 3; c++)
                stageAssignments.Add(new TeacherStageAssignment { TeacherId = t.Id, StageId = PrimariaStageId, Cycle = c });
        }

        // Especialistas de EF: todos los ciclos de Primaria de forma explícita
        foreach (var t in efTeachers)
        {
            for (int c = 1; c <= 3; c++)
                stageAssignments.Add(new TeacherStageAssignment { TeacherId = t.Id, StageId = PrimariaStageId, Cycle = c });
        }

        // Música: Primaria, ciclos 1, 2 y 3 explícitos.
        stageAssignments.Add(new TeacherStageAssignment { TeacherId = musicTeacher.Id, StageId = PrimariaStageId, Cycle = 1 });
        stageAssignments.Add(new TeacherStageAssignment { TeacherId = musicTeacher.Id, StageId = PrimariaStageId, Cycle = 2 });
        stageAssignments.Add(new TeacherStageAssignment { TeacherId = musicTeacher.Id, StageId = PrimariaStageId, Cycle = 3 });

        // Religión: todos los ciclos de Primaria de forma explícita
        for (int c = 1; c <= 3; c++)
            stageAssignments.Add(new TeacherStageAssignment { TeacherId = relTeacher.Id, StageId = PrimariaStageId, Cycle = c });

        db.TeacherStageAssignments.AddRange(stageAssignments);

        // ── Seed de Infantil y Secundaria (con datos completos) ──
        SeedInfantilStage(db, infantilStage, infantilAllocations, ingTeachers, opts);
        SeedSecundariaStage(db, secundariaStage, secundariaAllocations, musicTeacher, relTeacher, opts);

        // ── TeacherSubjectHours: derivar de todas las asignaciones añadidas al contexto ──
        // Se construye un mapa de allocations (todas las etapas) para obtener el SubjectKey.
        var allAllocById = allocations
            .Concat(infantilAllocations)
            .Concat(secundariaAllocations)
            .ToDictionary(a => a.Id, a => a.SubjectKey);

        // Agregar (TeacherId, SubjectKey) → suma de horas desde los Assignment del contexto.
        var subjectHoursAccumulator = new Dictionary<(Guid TeacherId, string SubjectKey), int>();
        foreach (var entry in db.ChangeTracker.Entries<Assignment>())
        {
            var a = entry.Entity;
            if (!allAllocById.TryGetValue(a.AllocationId, out var subjectKey)) continue;
            var key = (a.TeacherId, subjectKey);
            subjectHoursAccumulator[key] = subjectHoursAccumulator.GetValueOrDefault(key, 0) + a.WeeklyHours;
        }

        foreach (var (k, totalHours) in subjectHoursAccumulator)
        {
            db.TeacherSubjectHours.Add(new TeacherSubjectHour
            {
                TeacherId   = k.TeacherId,
                SubjectKey  = k.SubjectKey,
                WeeklyHours = totalHours,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Crea la estructura, aulas, profesores y asignaciones para el 2.º ciclo de Infantil (3-6 años) basados en la normativa de Madrid.
    /// </summary>
    private static void SeedInfantilStage(AppDbContext db, SchoolStage stage, List<SubjectAllocation> allocations, List<Teacher> ingTeachers, SeedOptions opts)
    {
        var infantilLines = opts.InfantilLines ?? opts.LinesPerLevel;
        var isPartida = opts.ScheduleType.Equals("partida", StringComparison.OrdinalIgnoreCase);
        var breaks = new List<(int AfterSlot, int Minutes)> { (2, LomloeMadrid.MinDailyBreakMinutes) }.AsReadOnly();

        var period = new SchoolPeriod
        {
            Id = InfantilPeriodId,
            SchoolId = SchoolId,
            StageId = stage.Id,
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

        // Infantil: continua 09:00–14:00 | partida 09:00–13:00 + 15:00–17:00
        var infantilMorningEnd   = isPartida ? new TimeOnly(13, 0) : new TimeOnly(14, 0);
        var infantilAfternoonEnd = isPartida ? new TimeOnly(17, 0) : (TimeOnly?)null;
        var cs = new CycleSchedule
        {
            SchoolId       = SchoolId,
            StageId        = stage.Id,
            PeriodId       = period.Id,
            Cycle          = 1,
            MorningStart   = new TimeOnly(9, 0),
            MorningEnd     = infantilMorningEnd,
            AfternoonStart = isPartida ? new TimeOnly(15, 0) : null,
            AfternoonEnd   = infantilAfternoonEnd,
            EndTime        = infantilAfternoonEnd ?? infantilMorningEnd,
        };
        cs.Breaks.Add(new CycleBreak { CycleScheduleId = cs.Id, AfterSlot = 2, Minutes = LomloeMadrid.MinDailyBreakMinutes });
        period.Cycles.Add(cs);
        db.SchoolPeriods.Add(period);

        // Aulas de Infantil: 3 niveles (3, 4, 5 años) × LinesPerLevel
        var classrooms = new List<Classroom>();
        char[] lineLabels = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
        for (int level = 1; level <= 3; level++)
        {
            for (int li = 0; li < infantilLines; li++)
            {
                classrooms.Add(new Classroom
                {
                    Id            = Guid.NewGuid(),
                    SchoolId      = SchoolId,
                    Name          = $"Aula Infantil {level + 2} años {lineLabels[li]}",
                    ClassroomType = "regular",
                    Capacity      = 22,
                });
            }
        }
        db.Classrooms.AddRange(classrooms);

        // Profesores tutores de Infantil (1 por aula)
        var infantilTutorNames = new[]
        {
            ("Carmen Ruiz",      "carmen.ruiz"),
            ("Rocío Gómez",      "rocio.gomez"),
            ("Silvia Muñoz",     "silvia.munoz"),
            ("Isabel Ortega",    "isabel.ortega"),
            ("Clara Benítez",    "clara.benitez"),
            ("Alicia Soler",     "alicia.soler"),
            ("Cristina Merino",  "cristina.merino"),
            ("Pilar Rubio",      "pilar.rubio"),
            ("Teresa Montes",    "teresa.montes"),
            ("Nuria Gil",        "nuria.gil"),
            ("Paula Marín",      "paula.marin"),
            ("Rosa Moya",        "rosa.moya"),
            ("Sofía Medina",     "sofia.medina"),
            ("Ángela Guerrero",  "angela.guerrero"),
            ("Margarita Cruz",   "margarita.cruz"),
            ("Estela Ortiz",     "estela.ortiz"),
            ("Inés Pastor",      "ines.pastor"),
            ("Olga Flores",      "olga.flores"),
            ("Gloria Cano",      "gloria.cano"),
            ("Marina Herranz",   "marina.herranz"),
            ("Victoria León",    "victoria.leon"),
            ("Sonia Gallego",    "sonia.gallego"),
            ("Raquel Peña",      "raquel.pena"),
            ("Esther Blanco",    "esther.blanco")
        };

        var teachers = new List<Teacher>();
        int totalInfantilTutors = 3 * infantilLines;
        for (int i = 0; i < totalInfantilTutors; i++)
        {
            var (name, email) = i < infantilTutorNames.Length
                ? infantilTutorNames[i]
                : ($"Profesor/a Infantil {i + 1}", $"profesor.infantil.{i + 1}");

            teachers.Add(new Teacher
            {
                Id             = Guid.NewGuid(),
                SchoolId       = SchoolId,
                FullName       = name,
                Email          = $"{email}@{EmailDomain}",
                TeacherType    = "definitivo",
                MaxWeeklyHours = 25,
                ColorKey       = TutorColorKeys[i % TutorColorKeys.Length],
            });
        }
        db.Teachers.AddRange(teachers);

        // Grupos de Infantil: 3 niveles (3, 4, 5 años) × LinesPerLevel
        var groups = new List<CourseGroup>();
        int groupIdx = 0;
        for (int level = 1; level <= 3; level++)
        {
            for (int li = 0; li < infantilLines; li++)
            {
                var tutor = teachers[groupIdx];
                var classroom = classrooms[groupIdx];

                groups.Add(new CourseGroup
                {
                    Id              = Guid.NewGuid(),
                    SchoolId        = SchoolId,
                    StageId         = stage.Id,
                    CourseLevel     = level,
                    GroupLabel      = lineLabels[li].ToString(),
                    StudentCount    = 20,
                    TutorId         = tutor.Id,
                    HomeClassroomId = classroom.Id,
                });
                groupIdx++;
            }
        }
        db.CourseGroups.AddRange(groups);

        // Asignaciones de Infantil (crec, desc, com, ing, rel)
        SubjectAllocation? GetAlloc(string key, int cycle)
        {
            return allocations.FirstOrDefault(a => a.SubjectKey == key && a.Cycle == cycle)
                ?? allocations.FirstOrDefault(a => a.SubjectKey == key && a.Cycle == null);
        }

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

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var tutor = teachers[i];

            var crecAlloc = GetAlloc("crec", 2); if (crecAlloc != null) AddAssignment(tutor.Id, group.Id, crecAlloc, 6);
            var descAlloc = GetAlloc("desc", 2); if (descAlloc != null) AddAssignment(tutor.Id, group.Id, descAlloc, 6);
            var comAlloc  = GetAlloc("com", 2);  if (comAlloc != null)  AddAssignment(tutor.Id, group.Id, comAlloc, 8);
            
            var ingAlloc  = GetAlloc("ing", 2);  if (ingAlloc != null)
            {
                var ingTeacher = ingTeachers[i % ingTeachers.Count];
                AddAssignment(ingTeacher.Id, group.Id, ingAlloc, 2);
            }
            var relAlloc  = GetAlloc("rel", 2);  if (relAlloc != null) AddAssignment(tutor.Id, group.Id, relAlloc, 1);
        }
        db.Assignments.AddRange(assignments);

        // Asociar profesores tutores a la etapa
        foreach (var teacher in teachers)
        {
            db.TeacherStageAssignments.Add(new TeacherStageAssignment
            {
                TeacherId = teacher.Id,
                StageId   = stage.Id,
                Cycle     = null,
            });
        }

        // También asociar profesores especialistas de inglés compartidos con Infantil
        foreach (var ingTeacher in ingTeachers)
        {
            db.TeacherStageAssignments.Add(new TeacherStageAssignment
            {
                TeacherId = ingTeacher.Id,
                StageId   = stage.Id,
                Cycle     = null,
            });
        }
    }

    /// <summary>
    /// Crea la estructura, aulas, profesores especialistas y asignaciones de Secundaria (ESO).
    /// </summary>
    private static void SeedSecundariaStage(
        AppDbContext db,
        SchoolStage stage,
        List<SubjectAllocation> allocations,
        Teacher musicTeacher,
        Teacher relTeacher,
        SeedOptions opts)
    {
        var linesPerLevel = opts.SecundariaLines ?? opts.LinesPerLevel;
        char[] lineLabels = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
        var breaks = new List<(int AfterSlot, int Minutes)> { (3, LomloeMadrid.MinDailyBreakMinutes) }.AsReadOnly();

        // Secundaria siempre jornada continua (30 h/semana, 6 slots/día), independientemente del centro
        var period = new SchoolPeriod
        {
            Id             = SecundariaPeriodId,
            SchoolId       = SchoolId,
            StageId        = stage.Id,
            Key            = "ordinario",
            Name           = "Jornada ordinaria",
            Months         = "[10,11,12,1,2,3,4,5]",
            ScheduleType   = "continua",
            SlotMinutes    = 60,
            SlotsPerDay    = 6,
            AfternoonSlots = 0,
            IsDefault      = true,
            SortOrder      = 0,
        };

        for (int c = 1; c <= 2; c++)
        {
            // Secundaria: continua 08:30–14:30 (6 franjas × 60 min = 6 h lectivas)
            var cs = new CycleSchedule
            {
                SchoolId       = SchoolId,
                StageId        = stage.Id,
                PeriodId       = period.Id,
                Cycle          = c,
                MorningStart   = new TimeOnly(8, 30),
                MorningEnd     = new TimeOnly(14, 30),
                AfternoonStart = null,
                AfternoonEnd   = null,
                EndTime        = new TimeOnly(14, 30),
            };
            cs.Breaks.Add(new CycleBreak { CycleScheduleId = cs.Id, AfterSlot = 3, Minutes = LomloeMadrid.MinDailyBreakMinutes });
            period.Cycles.Add(cs);
        }
        db.SchoolPeriods.Add(period);

        // Aulas: una por grupo (nivel × línea)
        var classrooms = new List<Classroom>();
        for (int level = 1; level <= 4; level++)
        {
            for (int li = 0; li < linesPerLevel; li++)
            {
                classrooms.Add(new Classroom
                {
                    Id            = Guid.NewGuid(),
                    SchoolId      = SchoolId,
                    Name          = $"Aula {level}º ESO {lineLabels[li]}",
                    ClassroomType = "regular",
                    Capacity      = 30,
                });
            }
        }
        db.Classrooms.AddRange(classrooms);

        // Pool de especialistas: 7 posiciones × N líneas.
        // Cada línea tiene su propio equipo de 7 profesores para no superar las 20 h/semana.
        // El email lleva sufijo ".esoa"/".esob" para garantizar unicidad en la escuela.
        string[] specColorKeys = ["mat", "len", "cie", "art", "tut", "ing", "opt"];
        (string Name, string Email)[,] specNamePool = {
            { ("Santiago Ortiz",   "santiago.ortiz"),   ("Miguel Fuentes",   "miguel.fuentes")   },
            { ("Isabel Sanz",      "isabel.sanz"),       ("Patricia Méndez",  "patricia.mendez")  },
            { ("Francisco Javier", "francisco.javier"),  ("Rodrigo Castillo", "rodrigo.castillo") },
            { ("Teresa Castro",    "teresa.castro"),     ("Sergio Mora",      "sergio.mora")      },
            { ("Jorge García",     "jorge.garcia"),      ("Hugo Blanco",      "hugo.blanco")      },
            { ("María José",       "maria.jose"),        ("Valentina Cruz",   "valentina.cruz")   },
            { ("Andrés Gómez",     "andres.gomez"),      ("Rebeca Prieto",    "rebeca.prieto")    },
        };
        int poolSize = specNamePool.GetLength(1);

        var secTeams = new List<List<Teacher>>();
        for (int li = 0; li < linesPerLevel; li++)
        {
            var team = new List<Teacher>();
            for (int pos = 0; pos < 7; pos++)
            {
                var (name, emailUser) = specNamePool[pos, li % poolSize];
                string lineSuffix = char.ToLower(lineLabels[li], System.Globalization.CultureInfo.InvariantCulture).ToString();
                team.Add(new Teacher
                {
                    Id             = Guid.NewGuid(),
                    SchoolId       = SchoolId,
                    FullName       = name,
                    Email          = $"{emailUser}.eso{lineSuffix}@{EmailDomain}",
                    TeacherType    = "definitivo",
                    MaxWeeklyHours = 20,
                    ColorKey       = specColorKeys[pos],
                });
            }
            secTeams.Add(team);
            db.Teachers.AddRange(team);
        }

        // Grupos: 4 niveles × linesPerLevel líneas
        var groups = new List<CourseGroup>();
        for (int level = 1; level <= 4; level++)
        {
            for (int li = 0; li < linesPerLevel; li++)
            {
                int classroomIdx = (level - 1) * linesPerLevel + li;
                groups.Add(new CourseGroup
                {
                    Id              = Guid.NewGuid(),
                    SchoolId        = SchoolId,
                    StageId         = stage.Id,
                    CourseLevel     = level,
                    GroupLabel      = lineLabels[li].ToString(),
                    StudentCount    = 25,
                    TutorId         = secTeams[li][0].Id,
                    HomeClassroomId = classrooms[classroomIdx].Id,
                });
            }
        }
        db.CourseGroups.AddRange(groups);

        // Asignaciones: cada grupo usa el equipo de su línea (gi % linesPerLevel)
        SubjectAllocation? GetAlloc(string key, int courseLevel)
        {
            return allocations.FirstOrDefault(a => a.SubjectKey == key && a.CourseLevel == courseLevel)
                ?? allocations.FirstOrDefault(a => a.SubjectKey == key && a.CourseLevel == null);
        }
        
        var assignments  = new List<Assignment>();

        for (int gi = 0; gi < groups.Count; gi++)
        {
            var group = groups[gi];
            var team  = secTeams[gi % linesPerLevel];
            var lvl   = group.CourseLevel;

            var tMat = team[0]; var tLen = team[1]; var tGh  = team[2];
            var tSci = team[3]; var tTec = team[4]; var tIng = team[5];
            var tOpt = team[6];

            // Materias comunes a todos los niveles
            var lenAlloc = GetAlloc("len", lvl); if (lenAlloc != null)
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tLen.Id, GroupId = group.Id, AllocationId = lenAlloc.Id, WeeklyHours = lvl == 1 ? 5 : 4 });

            var matAlloc = GetAlloc("mat", lvl); if (matAlloc != null)
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tMat.Id, GroupId = group.Id, AllocationId = matAlloc.Id, WeeklyHours = 4 });

            var ingAlloc = GetAlloc("ing", lvl); if (ingAlloc != null)
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tIng.Id, GroupId = group.Id, AllocationId = ingAlloc.Id, WeeklyHours = 3 });

            var ghAlloc = GetAlloc("gh", lvl); if (ghAlloc != null)
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tGh.Id, GroupId = group.Id, AllocationId = ghAlloc.Id, WeeklyHours = 3 });

            var efAlloc = GetAlloc("ef", lvl); if (efAlloc != null)
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id, GroupId = group.Id, AllocationId = efAlloc.Id, WeeklyHours = lvl == 4 ? 2 : 3 });

            var tutAlloc = GetAlloc("tut", lvl); if (tutAlloc != null)
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = group.TutorId ?? tMat.Id, GroupId = group.Id, AllocationId = tutAlloc.Id, WeeklyHours = 1 });

            // Materias específicas por nivel
            if (lvl == 1)
            {
                var bgAlloc = GetAlloc("bg", lvl);   if (bgAlloc != null)  assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tSci.Id,          GroupId = group.Id, AllocationId = bgAlloc.Id,  WeeklyHours = 3 });
                var artAlloc = GetAlloc("art", lvl); if (artAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id,          GroupId = group.Id, AllocationId = artAlloc.Id, WeeklyHours = 2 });
                var musAlloc = GetAlloc("mus", lvl); if (musAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = musicTeacher.Id,  GroupId = group.Id, AllocationId = musAlloc.Id, WeeklyHours = 2 });
                var relAlloc = GetAlloc("rel", lvl); if (relAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = relTeacher.Id,    GroupId = group.Id, AllocationId = relAlloc.Id, WeeklyHours = 2 });
                var optAlloc = GetAlloc("opt", lvl); if (optAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tOpt.Id,          GroupId = group.Id, AllocationId = optAlloc.Id, WeeklyHours = 2 });
            }
            else if (lvl == 2)
            {
                var fqAlloc = GetAlloc("fq", lvl);   if (fqAlloc != null)  assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tSci.Id,          GroupId = group.Id, AllocationId = fqAlloc.Id,  WeeklyHours = 3 });
                var tecAlloc = GetAlloc("tec", lvl); if (tecAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id,          GroupId = group.Id, AllocationId = tecAlloc.Id, WeeklyHours = 3 });
                var artAlloc = GetAlloc("art", lvl); if (artAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id,          GroupId = group.Id, AllocationId = artAlloc.Id, WeeklyHours = 2 });
                var valAlloc = GetAlloc("val", lvl); if (valAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tLen.Id,          GroupId = group.Id, AllocationId = valAlloc.Id, WeeklyHours = 1 });
                var relAlloc = GetAlloc("rel", lvl); if (relAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = relTeacher.Id,    GroupId = group.Id, AllocationId = relAlloc.Id, WeeklyHours = 1 });
                var optAlloc = GetAlloc("opt", lvl); if (optAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tOpt.Id,          GroupId = group.Id, AllocationId = optAlloc.Id, WeeklyHours = 2 });
            }
            else if (lvl == 3)
            {
                var bgAlloc = GetAlloc("bg", lvl);   if (bgAlloc != null)  assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tSci.Id,          GroupId = group.Id, AllocationId = bgAlloc.Id,  WeeklyHours = 2 });
                var fqAlloc = GetAlloc("fq", lvl);   if (fqAlloc != null)  assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tSci.Id,          GroupId = group.Id, AllocationId = fqAlloc.Id,  WeeklyHours = 3 });
                var tecAlloc = GetAlloc("tec", lvl); if (tecAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id,          GroupId = group.Id, AllocationId = tecAlloc.Id, WeeklyHours = 2 });
                var musAlloc = GetAlloc("mus", lvl); if (musAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = musicTeacher.Id,  GroupId = group.Id, AllocationId = musAlloc.Id, WeeklyHours = 2 });
                var relAlloc = GetAlloc("rel", lvl); if (relAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = relTeacher.Id,    GroupId = group.Id, AllocationId = relAlloc.Id, WeeklyHours = 1 });
                var optAlloc = GetAlloc("opt", lvl); if (optAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tOpt.Id,          GroupId = group.Id, AllocationId = optAlloc.Id, WeeklyHours = 2 });
            }
            else if (lvl == 4)
            {
                var relAlloc = GetAlloc("rel", lvl); if (relAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = relTeacher.Id, GroupId = group.Id, AllocationId = relAlloc.Id, WeeklyHours = 2 });
                var optAlloc = GetAlloc("opt", lvl); if (optAlloc != null) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tOpt.Id,       GroupId = group.Id, AllocationId = optAlloc.Id, WeeklyHours = 11 });
            }
        }
        db.Assignments.AddRange(assignments);

        // Asociar todos los especialistas de ESO a la etapa
        foreach (var team in secTeams)
        {
            foreach (var teacher in team)
            {
                db.TeacherStageAssignments.Add(new TeacherStageAssignment
                {
                    TeacherId = teacher.Id,
                    StageId   = stage.Id,
                    Cycle     = null,
                });
            }
        }

        // Asociar Música y Religión (compartidos con Primaria) a ESO
        db.TeacherStageAssignments.Add(new TeacherStageAssignment { TeacherId = musicTeacher.Id, StageId = stage.Id, Cycle = null });
        db.TeacherStageAssignments.Add(new TeacherStageAssignment { TeacherId = relTeacher.Id,   StageId = stage.Id, Cycle = null });
    }

    private static void SeedRoles(AppDbContext db)
    {
        if (db.Roles.Any()) return;

        db.Roles.AddRange(
            new Role { Id = RoleIds.Director,         Code = RoleCodes.Director,         Name = "Director",         Kind = RoleKind.Admin,   Description = "Máxima responsabilidad del centro",         SortOrder = 1, IsSystem = true },
            new Role { Id = RoleIds.JefeEstudios,     Code = RoleCodes.JefeEstudios,     Name = "Jefe de Estudios", Kind = RoleKind.Admin,   Description = "Coordinación académica y horarios",            SortOrder = 2, IsSystem = true },
            new Role { Id = RoleIds.Secretario,       Code = RoleCodes.Secretario,       Name = "Secretario",       Kind = RoleKind.Admin,   Description = "Gestión administrativa y documental",          SortOrder = 3, IsSystem = true },
            new Role { Id = RoleIds.Profesor,         Code = RoleCodes.Profesor,         Name = "Profesor",         Kind = RoleKind.Teacher, Description = "Docencia general",                            SortOrder = 4, IsSystem = true },
            new Role { Id = RoleIds.Tutor,            Code = RoleCodes.Tutor,            Name = "Tutor",            Kind = RoleKind.Teacher, Description = "Profesor con tutoría de un grupo",             SortOrder = 5, IsSystem = true },
            new Role { Id = RoleIds.CoordinadorCiclo, Code = RoleCodes.CoordinadorCiclo, Name = "Coordinador de ciclo", Kind = RoleKind.Teacher, Description = "Coordinación pedagógica de un ciclo educativo", SortOrder = 6, IsSystem = true },
            new Role { Id = RoleIds.Orientador,       Code = RoleCodes.Orientador,       Name = "Orientador",       Kind = RoleKind.Other,   Description = "Orientación educativa y psicopedagógica",      SortOrder = 7, IsSystem = true }
        );
    }
}
