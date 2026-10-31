using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SonrisaDigital.Data;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <summary>
    /// Quita del rol Administrador los permisos de Usuarios, Roles y permisos y Auditoría.
    /// Esas áreas quedan solo para el Dueño. Los permisos por defecto no se reaplican a una
    /// base existente, así que hace falta esta migración. Además cambia el SecurityStamp de
    /// los administradores para que sus sesiones abiertas recalculen los permisos.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261031120000_QuitarPermisosAdministradorUsuariosRolesAuditoria")]
    public partial class QuitarPermisosAdministradorUsuariosRolesAuditoria : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rp
                FROM RolesPermisos rp
                INNER JOIN Roles r ON r.Id = rp.RolId
                INNER JOIN Permisos p ON p.Id = rp.PermisoId
                WHERE r.Name = N'Administrador'
                  AND p.Nombre IN (N'Auditoria.Ver', N'Roles.Ver', N'Roles.Editar',
                                   N'Usuarios.Ver', N'Usuarios.Crear', N'Usuarios.Editar', N'Usuarios.Desactivar');

                UPDATE u
                SET u.SecurityStamp = CONVERT(nvarchar(36), NEWID())
                FROM Usuarios u
                INNER JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE r.Name = N'Administrador';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO RolesPermisos (RolId, PermisoId)
                SELECT r.Id, p.Id
                FROM Roles r
                CROSS JOIN Permisos p
                WHERE r.Name = N'Administrador'
                  AND p.Nombre IN (N'Auditoria.Ver', N'Roles.Ver', N'Roles.Editar',
                                   N'Usuarios.Ver', N'Usuarios.Crear', N'Usuarios.Editar', N'Usuarios.Desactivar')
                  AND NOT EXISTS (SELECT 1 FROM RolesPermisos rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id);

                UPDATE u
                SET u.SecurityStamp = CONVERT(nvarchar(36), NEWID())
                FROM Usuarios u
                INNER JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE r.Name = N'Administrador';
                """);
        }
    }
}
