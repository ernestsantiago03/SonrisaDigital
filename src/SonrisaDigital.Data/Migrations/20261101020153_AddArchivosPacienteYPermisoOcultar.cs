using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivosPacienteYPermisoOcultar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivosPaciente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicaId = table.Column<int>(type: "int", nullable: false),
                    PacienteId = table.Column<int>(type: "int", nullable: false),
                    NombreOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ClaveArchivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ClaveMiniatura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TipoContenido = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Tamano = table.Column<long>(type: "bigint", nullable: false),
                    FechaEstudio = table.Column<DateOnly>(type: "date", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FechaSubida = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Oculto = table.Column<bool>(type: "bit", nullable: false),
                    FechaOcultado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OcultadoPorUsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosPaciente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosPaciente_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivosPaciente_Pacientes_ClinicaId_PacienteId",
                        columns: x => new { x.ClinicaId, x.PacienteId },
                        principalTable: "Pacientes",
                        principalColumns: new[] { "ClinicaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchivosPaciente_Usuarios_OcultadoPorUsuarioId",
                        column: x => x.OcultadoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchivosPaciente_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosPaciente_ClinicaId",
                table: "ArchivosPaciente",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosPaciente_ClinicaId_PacienteId_FechaEstudio",
                table: "ArchivosPaciente",
                columns: new[] { "ClinicaId", "PacienteId", "FechaEstudio" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosPaciente_OcultadoPorUsuarioId",
                table: "ArchivosPaciente",
                column: "OcultadoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosPaciente_UsuarioId",
                table: "ArchivosPaciente",
                column: "UsuarioId");

            // Permiso para ocultar y restaurar archivos: solo lo tiene el rol Dueño.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM Permisos WHERE Nombre = N'Archivos.Ocultar')
                    INSERT INTO Permisos (Nombre, Modulo, Descripcion)
                    VALUES (N'Archivos.Ocultar', N'Archivos del paciente', N'Ocultar y restaurar archivos del paciente (solo Dueño)');

                INSERT INTO RolesPermisos (RolId, PermisoId)
                SELECT r.Id, p.Id
                FROM Roles r
                CROSS JOIN Permisos p
                WHERE r.Name = N'Dueño'
                  AND p.Nombre = N'Archivos.Ocultar'
                  AND NOT EXISTS (SELECT 1 FROM RolesPermisos rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id);

                UPDATE u
                SET u.SecurityStamp = CONVERT(nvarchar(36), NEWID())
                FROM Usuarios u
                INNER JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE r.Name = N'Dueño';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM RolesPermisos
                WHERE PermisoId IN (SELECT Id FROM Permisos WHERE Nombre = N'Archivos.Ocultar');

                DELETE FROM Permisos WHERE Nombre = N'Archivos.Ocultar';
                """);

            migrationBuilder.DropTable(
                name: "ArchivosPaciente");
        }
    }
}
