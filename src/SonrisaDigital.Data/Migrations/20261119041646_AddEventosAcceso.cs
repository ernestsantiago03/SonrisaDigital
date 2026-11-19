using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventosAcceso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventosAcceso",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Resultado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DireccionIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Agente = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClinicaId = table.Column<int>(type: "int", nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosAcceso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosAcceso_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosAcceso_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosAcceso_ClinicaId",
                table: "EventosAcceso",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosAcceso_Correo_FechaHora",
                table: "EventosAcceso",
                columns: new[] { "Correo", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosAcceso_FechaHora",
                table: "EventosAcceso",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_EventosAcceso_UsuarioId_FechaHora",
                table: "EventosAcceso",
                columns: new[] { "UsuarioId", "FechaHora" });

            // La bitácora de accesos también es append-only. El disparador aplica a
            // cualquier usuario de la base, igual que el de RegistrosAuditoria.
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER dbo.TR_EventosAcceso_SoloLectura
                ON dbo.EventosAcceso
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    THROW 50002, N'La bitácora de accesos no se puede modificar ni borrar.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_EventosAcceso_SoloLectura;");

            migrationBuilder.DropTable(
                name: "EventosAcceso");
        }
    }
}
