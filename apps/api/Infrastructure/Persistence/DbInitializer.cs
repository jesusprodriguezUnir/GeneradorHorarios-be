using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Infrastructure.Persistence;

/// <summary>
/// Aplica migraciones pendientes y siembra datos iniciales en la base de datos.
/// Se ejecuta en el arranque de la aplicación.
///
/// Configuración (sección "Seed" en appsettings.Development.json):
///   - Levels:          1-6 cursos de primaria  (default 6)
///   - LinesPerLevel:   grupos por nivel (A,B,C…) (default 3)
///   - Modality:        "estandar" | "bilingue"  (default "estandar")
///   - ScheduleType:    "continua" | "partida"   (default "continua")
/// </summary>
public static class DbInitializer
{
    // ── IDs fijos para las entidades compartidas entre ejecuciones ───────────────
    // Solo schoolId, templateId y usuarios necesitan ser estables (se usan en login
    // y como referencia externa). El resto se genera dinámicamente.
    private static readonly Guid SchoolId    = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateId  = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid ProfUserId  = Guid.Parse("00000000-0000-0000-0000-000000000011");

    // ── Nombres cortos de asignaturas (para la columna SubjectShort) ─────────────
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

    // ── Color keys para tutores (ciclan) ─────────────────────────────────────────
    private static readonly string[] TutorColorKeys = ["len", "mat", "cie", "art", "tut", "rel"];

    // ── Entrada pública: arranque de la aplicación ───────────────────────────────

    /// <summary>
    /// Aplica migraciones y siembra datos si la BD está vacía.
    /// </summary>
    public static async Task InitializeAsync(AppDbContext db, SeedOptions? options = null)
    {
        await db.Database.MigrateAsync();
        if (await db.Schools.AnyAsync()) return;

        await SeedAsync(db, options ?? new SeedOptions());
    }

    // ── Reseed para endpoint dev ──────────────────────────────────────────────────

    /// <summary>
    /// Elimina todos los datos del colegio demo y vuelve a sembrar con las nuevas opciones.
    /// Solo para uso en Development (llamado desde POST /api/dev/reseed).
    /// </summary>
    public static async Task ReseedAsync(AppDbContext db, SeedOptions options)
    {
        // Eliminar en orden FK-safe (dependientes antes que principales)
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
        await db.Schools.ExecuteDeleteAsync();

        await SeedAsync(db, options);
    }

    // ── Lógica de siembra ─────────────────────────────────────────────────────────

    private static async Task SeedAsync(AppDbContext db, SeedOptions opts)
    {
        var isBilingue = opts.Modality.Equals("bilingue", StringComparison.OrdinalIgnoreCase);
        var isPartida  = opts.ScheduleType.Equals("partida", StringComparison.OrdinalIgnoreCase);
        var totalGroups = opts.Levels * opts.LinesPerLevel;

        // ── 1. Colegio ──────────────────────────────────────────────────────────
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
            // ── Jornada ────────────────────────────────────────────────────────
            // Jornada continua:  9:00-10:00, 10:00-11:00, RECREO 11:00-11:30,
            //                    11:30-12:30, 12:30-13:30, 13:30-14:30 (5 lectivos)
            // Jornada partida:   9:00-10:00, 10:00-11:00, RECREO 11:00-11:30,
            //                    11:30-12:30 (3 mañana) + 15:00-16:00, 16:00-17:00 (2 tarde)
            ScheduleType   = opts.ScheduleType,
            MorningStart   = new TimeOnly(9, 0),
            AfternoonStart = isPartida ? new TimeOnly(15, 0) : null,
            SlotMinutes    = 60,
            BreakAfterSlot = 2,
            BreakMinutes   = LomloeMadrid.MinDailyBreakMinutes, // 30 min — mínimo legal
            SlotsPerDay    = 5,
            AfternoonSlots = isPartida ? 2 : 0,
            DaysPerWeek    = 5,
            WorkingDays    = "[1,2,3,4,5]",
        });

        // ── 2. Usuarios demo ────────────────────────────────────────────────────
        // Laura Fernández se crea junto con los profesores (ver abajo) y su ID se
        // enlaza al usuario teacher. Elena Castro es la jefatura (sin TeacherId).
        db.AppUsers.Add(new AppUser
        {
            Id       = AdminUserId,
            Email    = "elena.castro@ceip-miguel-hernandez.es",
            FullName = "Elena Castro",
            SchoolId = SchoolId,
            Role     = "school_admin",
        });

        // ── 3. Plantilla curricular — Decreto 61/2022 de Madrid ─────────────────
        db.CurriculumTemplates.Add(new CurriculumTemplate
        {
            Id         = TemplateId,
            Name       = $"LOMLOE Madrid — Decreto 61/2022 (Primaria{(isBilingue ? ", sección bilingüe" : "")})",
            Region     = "madrid",
            Stage      = "primaria",
            IsOfficial = true,
        });

        // ── 4. Asignaturas (SubjectAllocations) desde LomloeMadrid ──────────────
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

        // Acceso rápido por SubjectKey para construir asignaciones
        var allocByKey = allocations.ToDictionary(a => a.SubjectKey);

        // ── 5. Aulas ─────────────────────────────────────────────────────────────
        //
        // Capacidad de aulas especiales para 18 grupos (totalGroups):
        //   EF:     totalGroups × 3 h/semana = hasta 54 sesiones/semana
        //           3 gyms × 25 slots/semana = 75 capacidad → suficiente
        //   Música: totalGroups × 1 h/semana ≤ 25 slots/semana → 1 aula basta
        //   TIC:    opcional, para libre configuración
        var classrooms = new List<Classroom>();
        char[] lineLabels = ['A', 'B', 'C', 'D', 'E'];

        // Aulas regulares: una por grupo
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

        // Gimnasios (3): suficiente para todos los grupos a la vez
        var gymRooms = new[]
        {
            new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Gimnasio",            ClassroomType = "gym", Capacity = 60 },
            new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Pista Polideportiva", ClassroomType = "gym", Capacity = 80 },
            new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Patio Cubierto",      ClassroomType = "gym", Capacity = 60 },
        };
        classrooms.AddRange(gymRooms);

        // Aula de Música (1 es suficiente: ≤18 sesiones/semana < 25 slots disponibles)
        var musicRoom = new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Aula de Música", ClassroomType = "music", Capacity = 30 };
        classrooms.Add(musicRoom);

        // Aula de Informática
        classrooms.Add(new Classroom { Id = Guid.NewGuid(), SchoolId = SchoolId, Name = "Aula de Informática", ClassroomType = "it", Capacity = 26 });

        db.Classrooms.AddRange(classrooms);

        // Mapa de aulas regulares por posición (índice grupo → aula)
        var regularClassrooms = classrooms.Where(c => c.ClassroomType == "regular").ToList();

        // ── 6. Profesores ─────────────────────────────────────────────────────────
        //
        // Diseño de plantilla docente para un colegio de primaria de 3 líneas (LOMLOE):
        //   - 1 tutor generalista por grupo: enseña Lengua+Mates+Cono+Plástica (15 h/semana)
        //   - Especialistas de Inglés: distribución round-robin entre grupos
        //       estándar: 3 profesores × 6 grupos × 4 h = 24 h/profe (max 25)
        //       bilingüe: 5 profesores ×  distribución   ≤ 20 h/profe
        //   - Especialistas de EF: 3 profesores × 6 grupos × 3 h = 18 h/profe
        //   - Especialista de Música: 1 profesor × 18 grupos × 1 h = 18 h
        //   - Especialista de Religión: 1 profesor × 18 grupos × 1 h = 18 h

        // 6a. Tutores generalistas (nombre por nivel+línea)
        var tutorNames = new[]
        {
            // Nivel 1
            ("Ana García",         "ana.garcia"),
            ("Beatriz López",      "beatriz.lopez"),
            ("Carlos Martínez",    "carlos.martinez"),
            // Nivel 2
            ("Diana Rodríguez",    "diana.rodriguez"),
            ("Eduardo Sánchez",    "eduardo.sanchez"),
            ("Fernanda Torres",    "fernanda.torres"),
            // Nivel 3
            ("Guillermo Jiménez",  "guillermo.jimenez"),
            ("Helena Moreno",      "helena.moreno"),
            ("Ignacio Díaz",       "ignacio.diaz"),
            // Nivel 4
            ("Julia Pérez",        "julia.perez"),
            ("Kevin Romero",       "kevin.romero"),
            ("Lorena Álvarez",     "lorena.alvarez"),
            // Nivel 5
            ("Manuel Navarro",     "manuel.navarro"),
            ("Neus Serrano",       "neus.serrano"),
            ("Óscar Molina",       "oscar.molina"),
            // Nivel 6
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

        // 6b. Especialistas de Inglés
        //   Siempre creamos 5 profesores; los no utilizados (sin asignaciones) son inertes.
        //   Estándar: activos 0,1,2 (6+6+6 grupos × 4h = 24h cada uno)
        //   Bilingüe: activos 0-4 (4+4+4+3+3 grupos × 5h = 20+20+20+15+15h)
        var ingNames = new[]
        {
            ("Laura Fernández",  "laura.fernandez"),   // [0] — vinculada al usuario teacher
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

        // El usuario "teacher" demo se enlaza al primer especialista de Inglés (Laura Fernández)
        db.AppUsers.Add(new AppUser
        {
            Id        = ProfUserId,
            Email     = "laura.fernandez@ceip-miguel-hernandez.es",
            FullName  = "Laura Fernández",
            SchoolId  = SchoolId,
            Role      = "teacher",
            TeacherId = ingTeachers[0].Id,
        });

        // 6c. Especialistas de Educación Física (3)
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

        // 6d. Especialista de Música (1)
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

        // 6e. Especialista de Religión (1)
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

        // ── 7. Grupos (CourseGroups) ──────────────────────────────────────────────
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

        // ── 8. Asignaciones (profesor → asignatura → grupo) ───────────────────────
        //
        // Por cada grupo:
        //   Tutor     → Lengua (5h), Mates (5h), Cono (3h), Plástica (2h)  = 15 h
        //   Ing esp   → Inglés (4h estándar / 5h bilingüe)
        //   EF esp    → Ed. Física (3h)
        //   Música    → Música (1h)
        //   Religión  → Religión/Valores (1h)
        //   Total estándar: 15+4+3+1+1 = 24 h  (1 slot libre/semana → margen para el solver)
        //   Total bilingüe: 15+5+3+1+1 = 25 h  (capacidad exacta de la rejilla 5×5)
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

        // Distribución de especialistas de Inglés según modalidad
        // Estándar: 3 activos, cada uno cubre 1/3 de los grupos (round-robin módulo 3)
        // Bilingüe: 5 activos, distribución ~20h máx por profesor (round-robin módulo 5)
        int ingSlots   = isBilingue ? 5 : 4;   // horas de inglés por grupo
        int ingMod     = isBilingue ? 5 : 3;   // número de especialistas activos

        for (int gi = 0; gi < groups.Count; gi++)
        {
            var group     = groups[gi];
            var tutor     = tutors[gi % tutors.Count]; // tutores tienen siempre 1 grupo

            // Materias del tutor (generalista)
            if (allocByKey.TryGetValue("len", out var lenAlloc)) AddAssignment(tutor.Id, group.Id, lenAlloc, 5);
            if (allocByKey.TryGetValue("mat", out var matAlloc)) AddAssignment(tutor.Id, group.Id, matAlloc, 5);
            if (allocByKey.TryGetValue("cie", out var cieAlloc)) AddAssignment(tutor.Id, group.Id, cieAlloc, 3);
            if (allocByKey.TryGetValue("art", out var artAlloc)) AddAssignment(tutor.Id, group.Id, artAlloc, 2);

            // Inglés — round-robin entre especialistas activos
            if (allocByKey.TryGetValue("ing", out var ingAlloc))
                AddAssignment(ingTeachers[gi % ingMod].Id, group.Id, ingAlloc, ingSlots);

            // Educación Física — round-robin entre 3 especialistas
            if (allocByKey.TryGetValue("ef", out var efAlloc))
                AddAssignment(efTeachers[gi % efTeachers.Count].Id, group.Id, efAlloc, 3);

            // Música — único especialista para todos los grupos
            if (allocByKey.TryGetValue("mus", out var musAlloc))
                AddAssignment(musicTeacher.Id, group.Id, musAlloc, 1);

            // Religión / Valores — único especialista
            if (allocByKey.TryGetValue("rel", out var relAlloc))
                AddAssignment(relTeacher.Id, group.Id, relAlloc, 1);

            // Libre Configuración: NO se asigna en el demo para mantener 1 slot libre por semana
            // en modalidad estándar, lo que da margen al motor de backtracking.
            // En bilingüe ya se llega a 25 h (capacidad total), por lo que tampoco se asigna.
            // El centro puede añadir estas horas manualmente desde la UI de Asignaturas.
        }
        db.Assignments.AddRange(assignments);

        await db.SaveChangesAsync();
    }
}
