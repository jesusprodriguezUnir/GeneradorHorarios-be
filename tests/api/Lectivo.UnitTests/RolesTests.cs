using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Features.Roles;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class RolesTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task GetRoles_ReturnsAllRoles_OrderedBySortOrderThenName()
    {
        await using var db = CreateDb();
        db.Roles.AddRange(
            new Role { Id = Guid.NewGuid(), Code = "profesor", Name = "Profesor", Kind = RoleKind.Teacher, SortOrder = 3 },
            new Role { Id = Guid.NewGuid(), Code = "director", Name = "Director", Kind = RoleKind.Admin, SortOrder = 1 },
            new Role { Id = Guid.NewGuid(), Code = "jefe_estudios", Name = "Jefe de Estudios", Kind = RoleKind.Admin, SortOrder = 2 });
        await db.SaveChangesAsync();

        var handler = new GetRolesHandler(db);
        var result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Code.Should().Be("director");
        result[1].Code.Should().Be("jefe_estudios");
        result[2].Code.Should().Be("profesor");
    }

    [Fact]
    public async Task GetRoles_ReturnsEmpty_WhenNoRoles()
    {
        await using var db = CreateDb();

        var handler = new GetRolesHandler(db);
        var result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
