using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Infrastructure.Persistence;

/// <summary>
/// Aplica migraciones pendientes y siembra datos iniciales en la base de datos.
/// Se ejecuta en el arranque de la aplicación.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        // Aplicar migraciones pendientes automáticamente
        await db.Database.MigrateAsync();

        // Sembrar solo si la BD está vacía
        if (await db.Schools.AnyAsync()) return;

        // ── IDs fijos para poder referenciar entre entidades ────────────────
        var schoolId    = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var templateId  = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var adminUserId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var profUserId  = Guid.Parse("00000000-0000-0000-0000-000000000011");

        // ── Profesores IDs ───────────────────────────────────────────────────
        var t1 = Guid.Parse("00000000-0000-0000-0001-000000000001"); // Laura Fernández
        var t2 = Guid.Parse("00000000-0000-0000-0001-000000000002"); // Carlos Ruiz
        var t3 = Guid.Parse("00000000-0000-0000-0001-000000000003"); // María Gómez
        var t4 = Guid.Parse("00000000-0000-0000-0001-000000000004"); // David Soler
        var t5 = Guid.Parse("00000000-0000-0000-0001-000000000005"); // Ana Martín
        var t6 = Guid.Parse("00000000-0000-0000-0001-000000000006"); // Javier Pardo
        var t7 = Guid.Parse("00000000-0000-0000-0001-000000000007"); // Lucía Navarro
        var t8 = Guid.Parse("00000000-0000-0000-0001-000000000008"); // Pablo Vidal

        // ── Aulas IDs ────────────────────────────────────────────────────────
        var r1a = Guid.Parse("00000000-0000-0000-0002-000000000001");
        var r1b = Guid.Parse("00000000-0000-0000-0002-000000000002");
        var r3a = Guid.Parse("00000000-0000-0000-0002-000000000003");
        var r3b = Guid.Parse("00000000-0000-0000-0002-000000000004");
        var r5a = Guid.Parse("00000000-0000-0000-0002-000000000005");
        var r5b = Guid.Parse("00000000-0000-0000-0002-000000000006");
        var gim = Guid.Parse("00000000-0000-0000-0002-000000000007");
        var mus = Guid.Parse("00000000-0000-0000-0002-000000000008");
        var pla = Guid.Parse("00000000-0000-0000-0002-000000000009");
        var inf = Guid.Parse("00000000-0000-0000-0002-000000000010");

        // ── Grupos IDs ───────────────────────────────────────────────────────
        var g1a = Guid.Parse("00000000-0000-0000-0003-000000000001");
        var g1b = Guid.Parse("00000000-0000-0000-0003-000000000002");
        var g3a = Guid.Parse("00000000-0000-0000-0003-000000000003");
        var g3b = Guid.Parse("00000000-0000-0000-0003-000000000004");
        var g5a = Guid.Parse("00000000-0000-0000-0003-000000000005");
        var g5b = Guid.Parse("00000000-0000-0000-0003-000000000006");

        // ── Asignaturas IDs (SubjectAllocation) ──────────────────────────────
        var sLen = Guid.Parse("00000000-0000-0000-0004-000000000001"); // Lengua
        var sMat = Guid.Parse("00000000-0000-0000-0004-000000000002"); // Matemáticas
        var sCon = Guid.Parse("00000000-0000-0000-0004-000000000003"); // Conocimiento Medio
        var sIng = Guid.Parse("00000000-0000-0000-0004-000000000004"); // Inglés
        var sEf  = Guid.Parse("00000000-0000-0000-0004-000000000005"); // Ed. Física
        var sMus = Guid.Parse("00000000-0000-0000-0004-000000000006"); // Música
        var sPla = Guid.Parse("00000000-0000-0000-0004-000000000007"); // Plástica
        var sRel = Guid.Parse("00000000-0000-0000-0004-000000000008"); // Religión/Valores
        var sL2  = Guid.Parse("00000000-0000-0000-0004-000000000009"); // 2ª Lengua
        var sLib = Guid.Parse("00000000-0000-0000-0004-000000000010"); // Libre configuración

        // ════════════════════════════════════════════════════════════════════
        // Colegio demo
        // ════════════════════════════════════════════════════════════════════
        db.Schools.Add(new School
        {
            Id           = schoolId,
            Name         = "CEIP Miguel Hernández",
            Slug         = "ceip-miguel-hernandez",
            ScheduleType = "continua",
            MorningStart = new TimeOnly(9, 0),
            SlotMinutes  = 60,
            BreakAfterSlot = 2,
            BreakMinutes = 30,
        });

        // ── Usuarios demo ────────────────────────────────────────────────────
        db.AppUsers.Add(new AppUser
        {
            Id       = adminUserId,
            Email    = "elena.castro@ceip-miguel-hernandez.es",
            FullName = "Elena Castro",
            SchoolId = schoolId,
            Role     = "school_admin",
        });
        db.AppUsers.Add(new AppUser
        {
            Id        = profUserId,
            Email     = "laura.fernandez@ceip-miguel-hernandez.es",
            FullName  = "Laura Fernández",
            SchoolId  = schoolId,
            Role      = "teacher",
            TeacherId = t1,
        });

        // ── Plantilla LOMLOE Madrid 2024 ─────────────────────────────────────
        db.CurriculumTemplates.Add(new CurriculumTemplate
        {
            Id         = templateId,
            Name       = "LOMLOE Madrid 2024 — Primaria",
            Region     = "madrid",
            Stage      = "primaria",
            IsOfficial = true,
        });

        db.SubjectAllocations.AddRange([
            new() { Id = sLen, TemplateId = templateId, SubjectName = "Lengua Castellana y Literatura",
                    SubjectShort = "Lengua",    SubjectKey = "len",
                    WeeklyHoursMin = 4, WeeklyHoursMax = 6, WeeklyHoursDefault = 5 },
            new() { Id = sMat, TemplateId = templateId, SubjectName = "Matemáticas",
                    SubjectShort = "Mates",     SubjectKey = "mat",
                    WeeklyHoursMin = 4, WeeklyHoursMax = 6, WeeklyHoursDefault = 5 },
            new() { Id = sCon, TemplateId = templateId, SubjectName = "Conocimiento del Medio",
                    SubjectShort = "Naturales", SubjectKey = "cie",
                    WeeklyHoursMin = 3, WeeklyHoursMax = 4, WeeklyHoursDefault = 3 },
            new() { Id = sIng, TemplateId = templateId, SubjectName = "Inglés",
                    SubjectShort = "Inglés",    SubjectKey = "ing",
                    WeeklyHoursMin = 3, WeeklyHoursMax = 5, WeeklyHoursDefault = 4,
                    RequiresSpecialist = true },
            new() { Id = sEf,  TemplateId = templateId, SubjectName = "Educación Física",
                    SubjectShort = "E. Física", SubjectKey = "ef",
                    WeeklyHoursMin = 2, WeeklyHoursMax = 3, WeeklyHoursDefault = 3,
                    RequiresSpecialist = true, RequiredClassroomType = "gym",
                    MaxConsecutiveSlots = 1 },
            new() { Id = sMus, TemplateId = templateId, SubjectName = "Música",
                    SubjectShort = "Música",    SubjectKey = "mus",
                    WeeklyHoursMin = 1, WeeklyHoursMax = 2, WeeklyHoursDefault = 1,
                    RequiresSpecialist = true, RequiredClassroomType = "music",
                    MaxConsecutiveSlots = 1, SplittableAcrossDays = false },
            new() { Id = sPla, TemplateId = templateId, SubjectName = "Plástica y Educación Visual",
                    SubjectShort = "Plástica",  SubjectKey = "art",
                    WeeklyHoursMin = 1, WeeklyHoursMax = 2, WeeklyHoursDefault = 2 },
            new() { Id = sRel, TemplateId = templateId, SubjectName = "Religión / Valores Sociales y Cívicos",
                    SubjectShort = "Religión",  SubjectKey = "rel",
                    WeeklyHoursMin = 1, WeeklyHoursMax = 2, WeeklyHoursDefault = 1,
                    MaxConsecutiveSlots = 1, SplittableAcrossDays = false },
            new() { Id = sL2,  TemplateId = templateId, SubjectName = "Segunda Lengua Extranjera",
                    SubjectShort = "2ª Lengua", SubjectKey = "ing",
                    WeeklyHoursMin = 1, WeeklyHoursMax = 2, WeeklyHoursDefault = 1,
                    RequiresSpecialist = true, MaxConsecutiveSlots = 1 },
            new() { Id = sLib, TemplateId = templateId, SubjectName = "Libre Configuración del Centro",
                    SubjectShort = "Libre",     SubjectKey = "tut",
                    WeeklyHoursMin = 0, WeeklyHoursMax = 2, WeeklyHoursDefault = 1 },
        ]);

        // ── Aulas ────────────────────────────────────────────────────────────
        db.Classrooms.AddRange([
            new() { Id = r1a, SchoolId = schoolId, Name = "Aula 1ºA",  ClassroomType = "regular",  Capacity = 28 },
            new() { Id = r1b, SchoolId = schoolId, Name = "Aula 1ºB",  ClassroomType = "regular",  Capacity = 28 },
            new() { Id = r3a, SchoolId = schoolId, Name = "Aula 3ºA",  ClassroomType = "regular",  Capacity = 28 },
            new() { Id = r3b, SchoolId = schoolId, Name = "Aula 3ºB",  ClassroomType = "regular",  Capacity = 28 },
            new() { Id = r5a, SchoolId = schoolId, Name = "Aula 5ºA",  ClassroomType = "regular",  Capacity = 30 },
            new() { Id = r5b, SchoolId = schoolId, Name = "Aula 5ºB",  ClassroomType = "regular",  Capacity = 30 },
            new() { Id = gim, SchoolId = schoolId, Name = "Gimnasio",  ClassroomType = "gym",       Capacity = 50 },
            new() { Id = mus, SchoolId = schoolId, Name = "Aula de Música",     ClassroomType = "music", Capacity = 26 },
            new() { Id = pla, SchoolId = schoolId, Name = "Aula de Plástica",   ClassroomType = "regular", Capacity = 26 },
            new() { Id = inf, SchoolId = schoolId, Name = "Aula de Informática",ClassroomType = "it",     Capacity = 26 },
        ]);

        // ── Profesores ───────────────────────────────────────────────────────
        db.Teachers.AddRange([
            new() { Id = t1, SchoolId = schoolId, FullName = "Laura Fernández",
                    Email = "laura.fernandez@ceip-miguel-hernandez.es",
                    UserId = profUserId, TeacherType = "definitivo", MaxWeeklyHours = 25,
                    Specialties = "[\"Generalista\",\"Matemáticas\"]", ColorKey = "mat" },
            new() { Id = t2, SchoolId = schoolId, FullName = "Carlos Ruiz",
                    Email = "carlos.ruiz@ceip-miguel-hernandez.es",
                    TeacherType = "definitivo", MaxWeeklyHours = 25,
                    Specialties = "[\"Generalista\",\"C. Naturales\"]", ColorKey = "cie" },
            new() { Id = t3, SchoolId = schoolId, FullName = "María Gómez",
                    Email = "maria.gomez@ceip-miguel-hernandez.es",
                    TeacherType = "definitivo", MaxWeeklyHours = 25,
                    Specialties = "[\"Ed. Infantil/Primaria\"]", ColorKey = "len" },
            new() { Id = t4, SchoolId = schoolId, FullName = "David Soler",
                    Email = "david.soler@ceip-miguel-hernandez.es",
                    TeacherType = "especialista", MaxWeeklyHours = 25,
                    Specialties = "[\"Inglés (habilitación)\"]", ColorKey = "ing" },
            new() { Id = t5, SchoolId = schoolId, FullName = "Ana Martín",
                    Email = "ana.martin@ceip-miguel-hernandez.es",
                    TeacherType = "definitivo", MaxWeeklyHours = 25,
                    Specialties = "[\"Generalista\"]", ColorKey = "len" },
            new() { Id = t6, SchoolId = schoolId, FullName = "Javier Pardo",
                    Email = "javier.pardo@ceip-miguel-hernandez.es",
                    TeacherType = "especialista", MaxWeeklyHours = 25,
                    Specialties = "[\"Educación Física\"]", ColorKey = "ef" },
            new() { Id = t7, SchoolId = schoolId, FullName = "Lucía Navarro",
                    Email = "lucia.navarro@ceip-miguel-hernandez.es",
                    TeacherType = "definitivo", MaxWeeklyHours = 25,
                    Specialties = "[\"Generalista\",\"Música\"]", ColorKey = "mus" },
            new() { Id = t8, SchoolId = schoolId, FullName = "Pablo Vidal",
                    Email = "pablo.vidal@ceip-miguel-hernandez.es",
                    TeacherType = "especialista", MaxWeeklyHours = 20,
                    Specialties = "[\"Religión\"]", ColorKey = "rel" },
        ]);

        // ── Grupos ───────────────────────────────────────────────────────────
        db.CourseGroups.AddRange([
            new() { Id = g1a, SchoolId = schoolId, CourseLevel = 1, GroupLabel = "A", StudentCount = 24, TutorId = t3, HomeClassroomId = r1a },
            new() { Id = g1b, SchoolId = schoolId, CourseLevel = 1, GroupLabel = "B", StudentCount = 23, TutorId = t7, HomeClassroomId = r1b },
            new() { Id = g3a, SchoolId = schoolId, CourseLevel = 3, GroupLabel = "A", StudentCount = 25, TutorId = t1, HomeClassroomId = r3a },
            new() { Id = g3b, SchoolId = schoolId, CourseLevel = 3, GroupLabel = "B", StudentCount = 26, TutorId = t5, HomeClassroomId = r3b },
            new() { Id = g5a, SchoolId = schoolId, CourseLevel = 5, GroupLabel = "A", StudentCount = 27, TutorId = t2, HomeClassroomId = r5a },
            new() { Id = g5b, SchoolId = schoolId, CourseLevel = 5, GroupLabel = "B", StudentCount = 25, TutorId = null, HomeClassroomId = r5b },
        ]);

        // ── Asignaciones profesor → asignatura → grupo ────────────────────────
        // Ejemplo representativo para 3ºA y 3ºB (suficiente para generar horario demo)
        var assignments = new List<Assignment>();
        void Add(Guid tId, Guid gId, Guid aId, int hours) =>
            assignments.Add(new() { SchoolId = schoolId, TeacherId = tId, GroupId = gId, AllocationId = aId, WeeklyHours = hours });

        // 1ºA
        Add(t3, g1a, sLen, 5); Add(t3, g1a, sMat, 5); Add(t3, g1a, sCon, 3);
        Add(t4, g1a, sIng, 4); Add(t6, g1a, sEf,  3); Add(t7, g1a, sMus, 1);
        Add(t3, g1a, sPla, 2); Add(t8, g1a, sRel, 1);

        // 1ºB
        Add(t7, g1b, sLen, 5); Add(t7, g1b, sMat, 5); Add(t7, g1b, sCon, 3);
        Add(t4, g1b, sIng, 4); Add(t6, g1b, sEf,  3); Add(t7, g1b, sMus, 1);
        Add(t3, g1b, sPla, 2); Add(t8, g1b, sRel, 1);

        // 3ºA
        Add(t1, g3a, sLen, 5); Add(t1, g3a, sMat, 5); Add(t2, g3a, sCon, 3);
        Add(t4, g3a, sIng, 4); Add(t6, g3a, sEf,  3); Add(t7, g3a, sMus, 1);
        Add(t1, g3a, sPla, 2); Add(t8, g3a, sRel, 1);

        // 3ºB
        Add(t5, g3b, sLen, 5); Add(t5, g3b, sMat, 5); Add(t2, g3b, sCon, 3);
        Add(t4, g3b, sIng, 4); Add(t6, g3b, sEf,  3); Add(t7, g3b, sMus, 1);
        Add(t5, g3b, sPla, 2); Add(t8, g3b, sRel, 1);

        // 5ºA
        Add(t2, g5a, sLen, 5); Add(t2, g5a, sMat, 5); Add(t2, g5a, sCon, 3);
        Add(t4, g5a, sIng, 4); Add(t6, g5a, sEf,  3); Add(t7, g5a, sMus, 1);
        Add(t5, g5a, sPla, 2); Add(t8, g5a, sRel, 1);

        // 5ºB
        Add(t5, g5b, sLen, 5); Add(t1, g5b, sMat, 5); Add(t2, g5b, sCon, 3);
        Add(t4, g5b, sIng, 4); Add(t6, g5b, sEf,  3); Add(t7, g5b, sMus, 1);
        Add(t3, g5b, sPla, 2); Add(t8, g5b, sRel, 1);

        db.Assignments.AddRange(assignments);

        await db.SaveChangesAsync();
    }
}
