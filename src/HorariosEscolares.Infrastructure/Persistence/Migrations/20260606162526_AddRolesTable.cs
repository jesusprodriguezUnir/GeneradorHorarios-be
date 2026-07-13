using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorariosEscolares.Infrastructure.Persistence.Migrations
{
    public partial class AddRolesTable : Migration
    {
        private static readonly Guid DirectorId         = Guid.Parse("00000000-0000-0000-0000-0000000000A1");
        private static readonly Guid JefeEstudiosId     = Guid.Parse("00000000-0000-0000-0000-0000000000A2");
        private static readonly Guid SecretarioId       = Guid.Parse("00000000-0000-0000-0000-0000000000A3");
        private static readonly Guid ProfesorId         = Guid.Parse("00000000-0000-0000-0000-0000000000B1");
        private static readonly Guid TutorId            = Guid.Parse("00000000-0000-0000-0000-0000000000B2");
        private static readonly Guid CoordinadorCicloId = Guid.Parse("00000000-0000-0000-0000-0000000000B3");
        private static readonly Guid OrientadorId       = Guid.Parse("00000000-0000-0000-0000-0000000000C1");

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create Roles table
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            // 2. Seed the 7 base roles with fixed GUIDs
            migrationBuilder.Sql($@"
                INSERT INTO Roles (Id, Code, Name, Kind, Description, SortOrder, IsSystem)
                VALUES
                    ('{DirectorId}',         'director',           'Director',               'Admin',   'Máxima responsabilidad del centro',                  1, 1),
                    ('{JefeEstudiosId}',     'jefe_estudios',      'Jefe de Estudios',       'Admin',   'Coordinación académica y horarios',                   2, 1),
                    ('{SecretarioId}',       'secretario',         'Secretario',             'Admin',   'Gestión administrativa y documental',                 3, 1),
                    ('{ProfesorId}',         'profesor',           'Profesor',               'Teacher', 'Docencia general',                                   4, 1),
                    ('{TutorId}',            'tutor',              'Tutor',                  'Teacher', 'Profesor con tutoría de un grupo',                    5, 1),
                    ('{CoordinadorCicloId}', 'coordinador_ciclo',  'Coordinador de ciclo',   'Teacher', 'Coordinación pedagógica de un ciclo educativo',       6, 1),
                    ('{OrientadorId}',       'orientador',         'Orientador',             'Other',   'Orientación educativa y psicopedagógica',             7, 1);");

            // 3. Add RoleId as nullable initially
            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "AppUsers",
                type: "uniqueidentifier",
                nullable: true);

            // 4. Migrate existing data: school_admin → Director, teacher → Profesor
            migrationBuilder.Sql(
                $"UPDATE AppUsers SET RoleId = '{DirectorId}' WHERE Role = 'school_admin'");
            migrationBuilder.Sql(
                $"UPDATE AppUsers SET RoleId = '{ProfesorId}' WHERE Role = 'teacher'");

            // 5. Make RoleId non-nullable (all rows now have a value)
            migrationBuilder.Sql(
                "ALTER TABLE AppUsers ALTER COLUMN RoleId uniqueidentifier NOT NULL");

            // 6. Drop the old Role string column
            migrationBuilder.DropColumn(
                name: "Role",
                table: "AppUsers");

            // 7. Add index on RoleId
            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_RoleId",
                table: "AppUsers",
                column: "RoleId");

            // 8. Add unique index on Role.Code
            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                table: "Roles",
                column: "Code",
                unique: true);

            // 9. Add FK
            migrationBuilder.AddForeignKey(
                name: "FK_AppUsers_Roles_RoleId",
                table: "AppUsers",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Re-add Role string column
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AppUsers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // 2. Reverse data migration
            migrationBuilder.Sql(
                $"UPDATE AppUsers SET Role = 'school_admin' WHERE RoleId = '{DirectorId}'");
            migrationBuilder.Sql(
                $"UPDATE AppUsers SET Role = 'teacher' WHERE RoleId = '{ProfesorId}'");

            // 3. Drop FK, index, RoleId
            migrationBuilder.DropForeignKey(
                name: "FK_AppUsers_Roles_RoleId",
                table: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_RoleId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "AppUsers");

            // 4. Drop Roles table
            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
