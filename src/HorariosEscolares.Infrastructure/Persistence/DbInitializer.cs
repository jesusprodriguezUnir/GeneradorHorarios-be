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
        var totalGroups = opts.Levels * opts.LinesPerLevel;

        SeedRoles(db);

        db.Schools.Add(new School
        {
            Id             = SchoolId,
            Name           = "CEIP Miguel Hernández",
            Slug           = "ceip-miguel-hernandez",
            CenterCode     = "28013291",
            Locality       = "Madrid",
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
            Id        = InfantilStageId,
            SchoolId  = SchoolId,
            StageType = StageTypes.Infantil,
            Name      = "Educación Infantil",
            MinLevel  = 1,
            MaxLevel  = 2,
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
        }).ToList();
        db.SubjectAllocations.AddRange(secundariaAllocations);

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
            RoleId    = RoleIds.Profesor,
            TeacherId = ingTeachers[0].Id,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = JefeEstudiosUserId,
            Email     = "maria.garcia@ceip-miguel-hernandez.es",
            FullName  = "María García",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.JefeEstudios,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = SecretarioUserId,
            Email     = "antonio.lopez@ceip-miguel-hernandez.es",
            FullName  = "Antonio López",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.Secretario,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = TutorUserId,
            Email     = "ana.garcia@ceip-miguel-hernandez.es",
            FullName  = "Ana García",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.Tutor,
            TeacherId = tutors[0].Id,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = CoordinadorCicloUserId,
            Email     = "beatriz.lopez@ceip-miguel-hernandez.es",
            FullName  = "Beatriz López",
            SchoolId  = SchoolId,
            RoleId    = RoleIds.CoordinadorCiclo,
            TeacherId = tutors[1].Id,
        });

        db.AppUsers.Add(new AppUser
        {
            Id        = OrientadorUserId,
            Email     = "lucia.martin@ceip-miguel-hernandez.es",
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

        // ── Asociar profesores a etapas/ciclos ──────────────────────────────
        var stageAssignments = new List<TeacherStageAssignment>();

        // Tutores: Primaria, todos los ciclos (null)
        foreach (var tutor in tutors)
        {
            stageAssignments.Add(new TeacherStageAssignment
            {
                TeacherId = tutor.Id,
                StageId   = PrimariaStageId,
                Cycle     = null,
            });
        }

        // Especialistas de inglés: Primaria, todos los ciclos
        foreach (var t in ingTeachers)
        {
            stageAssignments.Add(new TeacherStageAssignment
            {
                TeacherId = t.Id,
                StageId   = PrimariaStageId,
                Cycle     = null,
            });
        }

        // Especialistas de EF: Primaria, todos los ciclos
        foreach (var t in efTeachers)
        {
            stageAssignments.Add(new TeacherStageAssignment
            {
                TeacherId = t.Id,
                StageId   = PrimariaStageId,
                Cycle     = null,
            });
        }

        // Música y Religión: Primaria, todos los ciclos
        stageAssignments.Add(new TeacherStageAssignment
        {
            TeacherId = musicTeacher.Id,
            StageId   = PrimariaStageId,
            Cycle     = null,
        });
        stageAssignments.Add(new TeacherStageAssignment
        {
            TeacherId = relTeacher.Id,
            StageId   = PrimariaStageId,
            Cycle     = null,
        });

        db.TeacherStageAssignments.AddRange(stageAssignments);

        // ── Seed de Infantil y Secundaria (con datos completos) ──
        SeedInfantilStage(db, infantilStage, infantilAllocations, ingTeachers, opts);
        SeedSecundariaStage(db, secundariaStage, secundariaAllocations, musicTeacher, relTeacher);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Crea la estructura, aulas, profesores y asignaciones para el 2.º ciclo de Infantil (3-6 años) basados en la normativa de Madrid.
    /// </summary>
    private static void SeedInfantilStage(AppDbContext db, SchoolStage stage, List<SubjectAllocation> allocations, List<Teacher> ingTeachers, SeedOptions opts)
    {
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

        var cycleEnd = SlotCalculator.ComputeEndTime(
            totalSlots: 5,
            slotMinutes: 60,
            breaks: breaks,
            afternoonSlots: isPartida ? 2 : 0,
            morningStart: new TimeOnly(9, 0),
            afternoonStart: isPartida ? new TimeOnly(15, 0) : null,
            isPartida: isPartida);
        var cs = new CycleSchedule
        {
            SchoolId       = SchoolId,
            StageId        = stage.Id,
            PeriodId       = period.Id,
            Cycle          = 1,
            MorningStart   = new TimeOnly(9, 0),
            EndTime        = cycleEnd,
            AfternoonStart = isPartida ? new TimeOnly(15, 0) : null,
        };
        cs.Breaks.Add(new CycleBreak { CycleScheduleId = cs.Id, AfterSlot = 2, Minutes = LomloeMadrid.MinDailyBreakMinutes });
        period.Cycles.Add(cs);
        db.SchoolPeriods.Add(period);

        // Aulas de Infantil: 3 niveles (3, 4, 5 años) × LinesPerLevel
        var classrooms = new List<Classroom>();
        char[] lineLabels = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
        for (int level = 1; level <= 3; level++)
        {
            for (int li = 0; li < opts.LinesPerLevel; li++)
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
        int totalInfantilTutors = 3 * opts.LinesPerLevel;
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
                Email          = $"{email}@ceip-miguel-hernandez.es",
                TeacherType    = "definitivo",
                MaxWeeklyHours = 25,
                Specialties    = "[\"Generalista Infantil\"]",
                ColorKey       = TutorColorKeys[i % TutorColorKeys.Length],
            });
        }
        db.Teachers.AddRange(teachers);

        // Grupos de Infantil: 3 niveles (3, 4, 5 años) × LinesPerLevel
        var groups = new List<CourseGroup>();
        int groupIdx = 0;
        for (int level = 1; level <= 3; level++)
        {
            for (int li = 0; li < opts.LinesPerLevel; li++)
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
        var allocByKey = allocations.ToDictionary(a => a.SubjectKey);
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

            if (allocByKey.TryGetValue("crec", out var crecAlloc)) AddAssignment(tutor.Id, group.Id, crecAlloc, 6);
            if (allocByKey.TryGetValue("desc", out var descAlloc)) AddAssignment(tutor.Id, group.Id, descAlloc, 6);
            if (allocByKey.TryGetValue("com",  out var comAlloc))  AddAssignment(tutor.Id, group.Id, comAlloc, 8);
            if (allocByKey.TryGetValue("ing",  out var ingAlloc))
            {
                var ingTeacher = ingTeachers[i % ingTeachers.Count];
                AddAssignment(ingTeacher.Id, group.Id, ingAlloc, 2);
            }
            if (allocByKey.TryGetValue("rel",  out var relAlloc)) AddAssignment(tutor.Id, group.Id, relAlloc, 1);
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
        Teacher relTeacher)
    {
        var breaks = new List<(int AfterSlot, int Minutes)> { (3, LomloeMadrid.MinDailyBreakMinutes) }.AsReadOnly();

        var period = new SchoolPeriod
        {
            Id = SecundariaPeriodId,
            SchoolId = SchoolId,
            StageId = stage.Id,
            Key = "ordinario",
            Name = "Jornada ordinaria",
            Months = "[10,11,12,1,2,3,4,5]",
            ScheduleType = "continua",
            SlotMinutes = 60,
            SlotsPerDay = 6, // 6 periodos diarios en Secundaria (30h/semana)
            AfternoonSlots = 0,
            IsDefault = true,
            SortOrder = 0,
        };

        for (int c = 1; c <= 2; c++)
        {
            var cycleEnd = SlotCalculator.ComputeEndTime(
                totalSlots: 6,
                slotMinutes: 60,
                breaks: breaks,
                afternoonSlots: 0,
                morningStart: new TimeOnly(8, 30),
                afternoonStart: null,
                isPartida: false);
            var cs = new CycleSchedule
            {
                SchoolId       = SchoolId,
                StageId        = stage.Id,
                PeriodId       = period.Id,
                Cycle          = c,
                MorningStart   = new TimeOnly(8, 30),
                EndTime        = cycleEnd,
                AfternoonStart = null,
            };
            cs.Breaks.Add(new CycleBreak { CycleScheduleId = cs.Id, AfterSlot = 3, Minutes = LomloeMadrid.MinDailyBreakMinutes });
            period.Cycles.Add(cs);
        }
        db.SchoolPeriods.Add(period);

        // Aulas de Secundaria
        var classrooms = new List<Classroom>();
        for (int level = 1; level <= 4; level++)
        {
            classrooms.Add(new Classroom
            {
                Id            = Guid.NewGuid(),
                SchoolId      = SchoolId,
                Name          = $"Aula {level}º ESO A",
                ClassroomType = "regular",
                Capacity      = 30,
            });
        }
        db.Classrooms.AddRange(classrooms);

        // Profesores especialistas de Secundaria
        var secTeachers = new List<Teacher>
        {
            new() { Id = Guid.NewGuid(), SchoolId = SchoolId, FullName = "Santiago Ortiz",     Email = "santiago.ortiz@ceip-miguel-hernandez.es",     TeacherType = "definitivo", MaxWeeklyHours = 20, Specialties = "[\"Matemáticas\"]", ColorKey = "mat" },
            new() { Id = Guid.NewGuid(), SchoolId = SchoolId, FullName = "Isabel Sanz",         Email = "isabel.sanz@ceip-miguel-hernandez.es",         TeacherType = "definitivo", MaxWeeklyHours = 20, Specialties = "[\"Lengua\"]", ColorKey = "len" },
            new() { Id = Guid.NewGuid(), SchoolId = SchoolId, FullName = "Francisco Javier",    Email = "francisco.javier@ceip-miguel-hernandez.es",    TeacherType = "definitivo", MaxWeeklyHours = 20, Specialties = "[\"Geografía e Historia\"]", ColorKey = "cie" },
            new() { Id = Guid.NewGuid(), SchoolId = SchoolId, FullName = "Teresa Castro",       Email = "teresa.castro@ceip-miguel-hernandez.es",       TeacherType = "definitivo", MaxWeeklyHours = 20, Specialties = "[\"Física y Química\",\"Biología\"]", ColorKey = "art" },
            new() { Id = Guid.NewGuid(), SchoolId = SchoolId, FullName = "Jorge García",        Email = "jorge.garcia@ceip-miguel-hernandez.es",        TeacherType = "definitivo", MaxWeeklyHours = 20, Specialties = "[\"Tecnología\",\"Educación Física\"]", ColorKey = "tut" },
            new() { Id = Guid.NewGuid(), SchoolId = SchoolId, FullName = "María José",          Email = "maria.jose@ceip-miguel-hernandez.es",          TeacherType = "definitivo", MaxWeeklyHours = 20, Specialties = "[\"Inglés\"]", ColorKey = "ing" },
            new() { Id = Guid.NewGuid(), SchoolId = SchoolId, FullName = "Andrés Gómez",        Email = "andres.gomez@ceip-miguel-hernandez.es",        TeacherType = "definitivo", MaxWeeklyHours = 20, Specialties = "[\"Generalista\"]", ColorKey = "opt" }
        };
        db.Teachers.AddRange(secTeachers);

        // Grupos de Secundaria
        var groups = new List<CourseGroup>();
        for (int level = 1; level <= 4; level++)
        {
            groups.Add(new CourseGroup
            {
                Id              = Guid.NewGuid(),
                SchoolId        = SchoolId,
                StageId         = stage.Id,
                CourseLevel     = level,
                GroupLabel      = "A",
                StudentCount    = 25,
                TutorId         = secTeachers[(level - 1) % secTeachers.Count].Id,
                HomeClassroomId = classrooms[level - 1].Id,
            });
        }
        db.CourseGroups.AddRange(groups);

        // Asignaciones
        var allocByKey = allocations.ToDictionary(a => a.SubjectKey);
        var assignments = new List<Assignment>();

        var tMat = secTeachers[0];
        var tLen = secTeachers[1];
        var tGh  = secTeachers[2];
        var tSci = secTeachers[3];
        var tTec = secTeachers[4];
        var tIng = secTeachers[5];
        var tOpt = secTeachers[6];

        foreach (var group in groups)
        {
            var lvl = group.CourseLevel;

            // Materias comunes
            if (allocByKey.TryGetValue("len", out var lenAlloc)) 
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tLen.Id, GroupId = group.Id, AllocationId = lenAlloc.Id, WeeklyHours = lvl == 1 ? 5 : 4 });
            
            if (allocByKey.TryGetValue("mat", out var matAlloc)) 
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tMat.Id, GroupId = group.Id, AllocationId = matAlloc.Id, WeeklyHours = 4 });
            
            if (allocByKey.TryGetValue("ing", out var ingAlloc)) 
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tIng.Id, GroupId = group.Id, AllocationId = ingAlloc.Id, WeeklyHours = 3 });
            
            if (allocByKey.TryGetValue("gh", out var ghAlloc)) 
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tGh.Id, GroupId = group.Id, AllocationId = ghAlloc.Id, WeeklyHours = 3 });
            
            if (allocByKey.TryGetValue("ef", out var efAlloc)) 
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id, GroupId = group.Id, AllocationId = efAlloc.Id, WeeklyHours = lvl == 4 ? 2 : 3 });
            
            if (allocByKey.TryGetValue("tut", out var tutAlloc)) 
                assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = group.TutorId ?? tMat.Id, GroupId = group.Id, AllocationId = tutAlloc.Id, WeeklyHours = 1 });

            // Materias específicas por nivel
            if (lvl == 1)
            {
                if (allocByKey.TryGetValue("bg", out var bgAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tSci.Id, GroupId = group.Id, AllocationId = bgAlloc.Id, WeeklyHours = 3 });
                if (allocByKey.TryGetValue("art", out var artAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id, GroupId = group.Id, AllocationId = artAlloc.Id, WeeklyHours = 2 });
                if (allocByKey.TryGetValue("mus", out var musAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = musicTeacher.Id, GroupId = group.Id, AllocationId = musAlloc.Id, WeeklyHours = 2 });
                if (allocByKey.TryGetValue("rel", out var relAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = relTeacher.Id, GroupId = group.Id, AllocationId = relAlloc.Id, WeeklyHours = 2 });
                if (allocByKey.TryGetValue("opt", out var optAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tOpt.Id, GroupId = group.Id, AllocationId = optAlloc.Id, WeeklyHours = 2 });
            }
            else if (lvl == 2)
            {
                if (allocByKey.TryGetValue("fq", out var fqAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tSci.Id, GroupId = group.Id, AllocationId = fqAlloc.Id, WeeklyHours = 3 });
                if (allocByKey.TryGetValue("tec", out var tecAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id, GroupId = group.Id, AllocationId = tecAlloc.Id, WeeklyHours = 3 });
                if (allocByKey.TryGetValue("art", out var artAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id, GroupId = group.Id, AllocationId = artAlloc.Id, WeeklyHours = 2 });
                if (allocByKey.TryGetValue("val", out var valAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tLen.Id, GroupId = group.Id, AllocationId = valAlloc.Id, WeeklyHours = 1 });
                if (allocByKey.TryGetValue("rel", out var relAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = relTeacher.Id, GroupId = group.Id, AllocationId = relAlloc.Id, WeeklyHours = 1 });
                if (allocByKey.TryGetValue("opt", out var optAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tOpt.Id, GroupId = group.Id, AllocationId = optAlloc.Id, WeeklyHours = 2 });
            }
            else if (lvl == 3)
            {
                if (allocByKey.TryGetValue("bg", out var bgAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tSci.Id, GroupId = group.Id, AllocationId = bgAlloc.Id, WeeklyHours = 2 });
                if (allocByKey.TryGetValue("fq", out var fqAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tSci.Id, GroupId = group.Id, AllocationId = fqAlloc.Id, WeeklyHours = 3 });
                if (allocByKey.TryGetValue("tec", out var tecAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tTec.Id, GroupId = group.Id, AllocationId = tecAlloc.Id, WeeklyHours = 2 });
                if (allocByKey.TryGetValue("mus", out var musAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = musicTeacher.Id, GroupId = group.Id, AllocationId = musAlloc.Id, WeeklyHours = 2 });
                if (allocByKey.TryGetValue("rel", out var relAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = relTeacher.Id, GroupId = group.Id, AllocationId = relAlloc.Id, WeeklyHours = 1 });
                if (allocByKey.TryGetValue("opt", out var optAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tOpt.Id, GroupId = group.Id, AllocationId = optAlloc.Id, WeeklyHours = 2 });
            }
            else if (lvl == 4)
            {
                if (allocByKey.TryGetValue("rel", out var relAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = relTeacher.Id, GroupId = group.Id, AllocationId = relAlloc.Id, WeeklyHours = 2 });
                if (allocByKey.TryGetValue("opt", out var optAlloc)) assignments.Add(new Assignment { SchoolId = SchoolId, TeacherId = tOpt.Id, GroupId = group.Id, AllocationId = optAlloc.Id, WeeklyHours = 11 }); // Materias optativas / de opción
            }
        }
        db.Assignments.AddRange(assignments);

        // Asociar profesores especialistas a la etapa secundaria
        foreach (var teacher in secTeachers)
        {
            db.TeacherStageAssignments.Add(new TeacherStageAssignment
            {
                TeacherId = teacher.Id,
                StageId   = stage.Id,
                Cycle     = null,
            });
        }

        // Asociar profesores de Música y Religión (compartidos) con Secundaria
        db.TeacherStageAssignments.Add(new TeacherStageAssignment { TeacherId = musicTeacher.Id, StageId = stage.Id, Cycle = null });
        db.TeacherStageAssignments.Add(new TeacherStageAssignment { TeacherId = relTeacher.Id, StageId = stage.Id, Cycle = null });
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
