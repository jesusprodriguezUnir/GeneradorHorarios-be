using System.Net;
using FluentAssertions;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lectivo.IntegrationTests;

/// <summary>
/// Verifica que el admin de un centro no puede leer ni modificar datos de otro centro.
/// Prueba la guarda: if (template.SchoolId != user.SchoolId) return 403.
/// El admin del centro 1 (seed) intenta operar sobre los recursos del centro 2 (inserción propia).
/// </summary>
[Collection("MsSql collection")]
public class CrossTenantIsolationTests : IAsyncLifetime
{
    private readonly LectivoApiFactory _factory;

    // Centro 2 — insertado por este test, IDs fijos para idempotencia
    private static readonly Guid School2Id          = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly Guid School2AdminId     = Guid.Parse("00000000-0000-0000-0000-000000000098");
    private static readonly Guid School2TemplateId  = Guid.Parse("00000000-0000-0099-0000-000000000001");
    private static readonly Guid School2SubjectId   = Guid.Parse("00000000-0000-0099-0000-000000000002");
    private static readonly Guid School2StageId     = Guid.Parse("00000000-0000-0099-0000-000000000003");
    private static readonly Guid School2TeacherId   = Guid.Parse("00000000-0000-0099-0000-000000000004");
    private static readonly Guid School2GroupId     = Guid.Parse("00000000-0000-0099-0000-000000000005");
    private static readonly Guid School2AssignmentId= Guid.Parse("00000000-0000-0099-0000-000000000006");
    private static readonly Guid School2ConstraintId= Guid.Parse("00000000-0000-0099-0000-000000000007");
    private static readonly Guid School2ClassroomId = Guid.Parse("00000000-0000-0099-0000-000000000008");

    public CrossTenantIsolationTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Insertar centro 2 completo si no existe (idempotente)
        if (!await db.Schools.AnyAsync(s => s.Id == School2Id))
        {
            if (!await db.Roles.AnyAsync())
            {
                db.Roles.AddRange(
                    new Role { Id = RoleIds.Director,         Code = RoleCodes.Director,         Name = "Director",         Kind = RoleKind.Admin },
                    new Role { Id = RoleIds.Profesor,         Code = RoleCodes.Profesor,         Name = "Profesor",         Kind = RoleKind.Teacher });
            }

            db.Schools.Add(new School
            {
                Id   = School2Id,
                Name = "CEIP Prueba Aislamiento",
                Slug = "ceip-prueba-aislamiento",
            });
            db.AppUsers.Add(new AppUser
            {
                Id       = School2AdminId,
                Email    = "admin@ceip-prueba-aislamiento.es",
                FullName = "Admin Otro Centro",
                SchoolId = School2Id,
                RoleId   = RoleIds.Director,
            });
            db.CurriculumTemplates.Add(new CurriculumTemplate
            {
                Id         = School2TemplateId,
                SchoolId   = School2Id,
                Name       = "Plantilla Aislamiento",
                IsOfficial = false,
            });
            db.SubjectAllocations.Add(new SubjectAllocation
            {
                Id                 = School2SubjectId,
                TemplateId         = School2TemplateId,
                SubjectName        = "Matematicas (Aislamiento)",
                SubjectShort       = "MAT",
                SubjectKey         = "mat",
                WeeklyHoursMin     = 4,
                WeeklyHoursMax     = 8,
                WeeklyHoursDefault = 5,
                IsOfficial         = false,
            });
            db.SchoolStages.Add(new SchoolStage
            {
                Id = School2StageId,
                SchoolId = School2Id,
                StageType = StageTypes.Primaria,
                Name = "Primaria Aislamiento",
            });
            db.Teachers.Add(new Teacher
            {
                Id = School2TeacherId,
                SchoolId = School2Id,
                FullName = "Profe Aislamiento",
                Email = "profe@ceip-prueba-aislamiento.es",
            });
            db.Classrooms.Add(new Classroom
            {
                Id = School2ClassroomId,
                SchoolId = School2Id,
                Name = "Aula Aislamiento",
                ClassroomType = "Classroom",
                Capacity = 25,
                IsShared = false,
            });
            db.CourseGroups.Add(new CourseGroup
            {
                Id = School2GroupId,
                SchoolId = School2Id,
                StageId = School2StageId,
                CourseLevel = 1,
                GroupLabel = "A",
                StudentCount = 20,
            });
            db.Assignments.Add(new Assignment
            {
                Id = School2AssignmentId,
                SchoolId = School2Id,
                TeacherId = School2TeacherId,
                GroupId = School2GroupId,
                AllocationId = School2SubjectId,
                WeeklyHours = 4,
            });
            db.TeacherConstraints.Add(new TeacherConstraint
            {
                Id = School2ConstraintId,
                SchoolId = School2Id,
                TeacherId = School2TeacherId,
                ConstraintType = "Preferred",
                DayOfWeek = 1,
                SlotIndex = 0,
                Weight = 10,
            });
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Subjects ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutHours_OnOtherSchoolSubject_ReturnsForbidden()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.MapPut(
            $"/api/subjects/{School2SubjectId}/hours",
            new { weeklyHoursDefault = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutSubject_OnOtherSchoolSubject_ReturnsForbidden()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.MapPut(
            $"/api/subjects/{School2SubjectId}",
            new { subjectName = "Hackeo" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteSubject_OnOtherSchoolSubject_ReturnsForbidden()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.DeleteAsync($"/api/subjects/{School2SubjectId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutHours_OnOwnSchoolSubject_ReturnsOk()
    {
        var school2Client = _factory.CreateClientForUserId(School2AdminId);
        var response = await school2Client.MapPut(
            $"/api/subjects/{School2SubjectId}/hours",
            new { weeklyHoursDefault = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Classrooms ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PutClassroom_OnOtherSchoolClassroom_ReturnsNotFound()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.MapPut(
            $"/api/classrooms/{School2ClassroomId}",
            new { name = "Aula Hackeada" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteClassroom_OnOtherSchoolClassroom_ReturnsNotFound()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.DeleteAsync($"/api/classrooms/{School2ClassroomId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutClassroom_OnOwnSchoolClassroom_ReturnsOk()
    {
        var school2Client = _factory.CreateClientForUserId(School2AdminId);
        var response = await school2Client.MapPut(
            $"/api/classrooms/{School2ClassroomId}",
            new { name = "Aula Actualizada" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Groups ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutGroup_OnOtherSchoolGroup_ReturnsNotFound()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.MapPut(
            $"/api/groups/{School2GroupId}",
            new { groupLabel = "Hackeado" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGroup_OnOtherSchoolGroup_ReturnsNotFound()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.DeleteAsync($"/api/groups/{School2GroupId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutGroup_OnOwnSchoolGroup_ReturnsOk()
    {
        var school2Client = _factory.CreateClientForUserId(School2AdminId);
        var response = await school2Client.MapPut(
            $"/api/groups/{School2GroupId}",
            new { groupLabel = "B" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Assignments ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAssignment_OnOtherSchoolAssignment_ReturnsNotFound()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.DeleteAsync($"/api/assignments/{School2AssignmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Constraints ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteConstraint_OnOtherSchoolConstraint_ReturnsNotFound()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.DeleteAsync($"/api/constraints/{School2ConstraintId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
