using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHistorialOdontograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialOdontograma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicaId = table.Column<int>(type: "int", nullable: false),
                    PacienteId = table.Column<int>(type: "int", nullable: false),
                    PiezaDentalId = table.Column<int>(type: "int", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EstadoNuevo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OdontologoId = table.Column<int>(type: "int", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialOdontograma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialOdontograma_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialOdontograma_Odontologos_ClinicaId_OdontologoId",
                        columns: x => new { x.ClinicaId, x.OdontologoId },
                        principalTable: "Odontologos",
                        principalColumns: new[] { "ClinicaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialOdontograma_Pacientes_ClinicaId_PacienteId",
                        columns: x => new { x.ClinicaId, x.PacienteId },
                        principalTable: "Pacientes",
                        principalColumns: new[] { "ClinicaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialOdontograma_PiezasDentales_PiezaDentalId",
                        column: x => x.PiezaDentalId,
                        principalTable: "PiezasDentales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialOdontograma_ClinicaId",
                table: "HistorialOdontograma",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialOdontograma_ClinicaId_OdontologoId",
                table: "HistorialOdontograma",
                columns: new[] { "ClinicaId", "OdontologoId" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialOdontograma_ClinicaId_PacienteId_PiezaDentalId_FechaHora",
                table: "HistorialOdontograma",
                columns: new[] { "ClinicaId", "PacienteId", "PiezaDentalId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialOdontograma_PiezaDentalId",
                table: "HistorialOdontograma",
                column: "PiezaDentalId");

            // Cada pieza que ya tenía registro entra al historial con su estado actual como
            // primera entrada. Los cambios anteriores a esta migración no se pueden recuperar.
            migrationBuilder.Sql("""
                INSERT INTO HistorialOdontograma
                    (ClinicaId, PacienteId, PiezaDentalId, EstadoAnterior, EstadoNuevo, Observaciones, OdontologoId, FechaHora)
                SELECT ClinicaId, PacienteId, PiezaDentalId, NULL, Estado, Observaciones, OdontologoId, FechaModificacion
                FROM RegistrosOdontograma;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialOdontograma");
        }
    }
}
