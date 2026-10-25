using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <summary>
    /// Sustituye el índice simple de paciente por uno que incluye fecha y hora de inicio.
    /// La consulta que detecta choques de horario filtra por esos tres campos.
    /// </summary>
    public partial class AddCitaPacienteFechaIndex : Migration
    {
        /// <summary>
        /// Crea el índice compuesto que usa la validación de agenda del paciente.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Citas_PacienteId",
                table: "Citas");

            migrationBuilder.CreateIndex(
                name: "IX_Citas_PacienteId_Fecha_HoraInicio",
                table: "Citas",
                columns: new[] { "PacienteId", "Fecha", "HoraInicio" });
        }

        /// <summary>
        /// Vuelve al índice simple de paciente si se revierte la migración.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Citas_PacienteId_Fecha_HoraInicio",
                table: "Citas");

            migrationBuilder.CreateIndex(
                name: "IX_Citas_PacienteId",
                table: "Citas",
                column: "PacienteId");
        }
    }
}
