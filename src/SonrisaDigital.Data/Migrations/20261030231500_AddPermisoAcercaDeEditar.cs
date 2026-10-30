using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SonrisaDigital.Data;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <summary>
    /// Agrega el permiso AcercaDe.Editar y lo concede a los roles Administrador y Dueño que ya existen.
    /// Los permisos por defecto solo se asignan al crear un rol, así que en una base existente hace falta esta migración.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261030231500_AddPermisoAcercaDeEditar")]
    public partial class AddPermisoAcercaDeEditar : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM Permisos WHERE Nombre = N'AcercaDe.Editar')
                    INSERT INTO Permisos (Nombre, Modulo, Descripcion)
                    VALUES (N'AcercaDe.Editar', N'Acerca De', N'Editar la información de la clínica (Administrador y Dueño)');

                INSERT INTO RolesPermisos (RolId, PermisoId)
                SELECT r.Id, p.Id
                FROM Roles r
                CROSS JOIN Permisos p
                WHERE r.Name IN (N'Administrador', N'Dueño')
                  AND p.Nombre = N'AcercaDe.Editar'
                  AND NOT EXISTS (SELECT 1 FROM RolesPermisos rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM RolesPermisos
                WHERE PermisoId IN (SELECT Id FROM Permisos WHERE Nombre = N'AcercaDe.Editar');

                DELETE FROM Permisos WHERE Nombre = N'AcercaDe.Editar';
                """);
        }
    }
}
