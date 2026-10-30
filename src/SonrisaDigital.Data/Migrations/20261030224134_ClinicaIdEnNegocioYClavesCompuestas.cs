using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClinicaIdEnNegocioYClavesCompuestas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Citas_Odontologos_OdontologoId",
                table: "Citas");

            migrationBuilder.DropForeignKey(
                name: "FK_Citas_Pacientes_PacienteId",
                table: "Citas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultasClinicas_Citas_CitaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultasClinicas_HistoriasClinicas_HistoriaClinicaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultasClinicas_Odontologos_OdontologoId",
                table: "ConsultasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultasClinicas_Pacientes_PacienteId",
                table: "ConsultasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_HistoriasClinicas_Pacientes_PacienteId",
                table: "HistoriasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_TratamientosRealizados_TratamientoRealizadoId",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosOdontograma_Odontologos_OdontologoId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosOdontograma_Pacientes_PacienteId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosRealizados_ConsultasClinicas_ConsultaId",
                table: "TratamientosRealizados");

            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosRealizados_Odontologos_OdontologoId",
                table: "TratamientosRealizados");

            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosRealizados_Pacientes_PacienteId",
                table: "TratamientosRealizados");

            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosRealizados_Tratamientos_TratamientoId",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_TratamientosRealizados_ConsultaId",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_TratamientosRealizados_OdontologoId",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_TratamientosRealizados_PacienteId",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_TratamientosRealizados_TratamientoId",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_Tratamientos_Nombre",
                table: "Tratamientos");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosOdontograma_OdontologoId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropIndex(
                name: "IX_Pacientes_Identificacion",
                table: "Pacientes");

            migrationBuilder.DropIndex(
                name: "IX_Odontologos_NumeroLicencia",
                table: "Odontologos");

            migrationBuilder.DropIndex(
                name: "IX_ConsultasClinicas_HistoriaClinicaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_ConsultasClinicas_OdontologoId",
                table: "ConsultasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_ConsultasClinicas_PacienteId",
                table: "ConsultasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_Citas_OdontologoId_Fecha_HoraInicio",
                table: "Citas");

            migrationBuilder.DropIndex(
                name: "IX_Citas_PacienteId_Fecha_HoraInicio",
                table: "Citas");

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "TratamientosRealizados",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "Tratamientos",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "RegistrosOdontograma",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "RegistrosAuditoria",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "Pagos",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "Pacientes",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "Odontologos",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "HistoriasClinicas",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "ConsultasClinicas",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "Citas",
                type: "int",
                nullable: true,
                defaultValue: 0);

                        // Relleno de datos: las filas existentes pasan a la clínica demo antes de volver obligatoria la columna.
            migrationBuilder.Sql("UPDATE TratamientosRealizados SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.Sql("UPDATE Tratamientos SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.Sql("UPDATE RegistrosOdontograma SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.Sql("UPDATE RegistrosAuditoria SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.Sql("UPDATE Pagos SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.Sql("UPDATE Pacientes SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.Sql("UPDATE Odontologos SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.Sql("UPDATE HistoriasClinicas SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.Sql("UPDATE ConsultasClinicas SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.Sql("UPDATE Citas SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo') WHERE ClinicaId IS NULL");
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "TratamientosRealizados", type: "int", nullable: false);
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "Tratamientos", type: "int", nullable: false);
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "RegistrosOdontograma", type: "int", nullable: false);
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "RegistrosAuditoria", type: "int", nullable: false);
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "Pagos", type: "int", nullable: false);
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "Pacientes", type: "int", nullable: false);
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "Odontologos", type: "int", nullable: false);
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "HistoriasClinicas", type: "int", nullable: false);
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "ConsultasClinicas", type: "int", nullable: false);
            migrationBuilder.AlterColumn<int>(name: "ClinicaId", table: "Citas", type: "int", nullable: false);

migrationBuilder.AddUniqueConstraint(
                name: "AK_TratamientosRealizados_ClinicaId_Id",
                table: "TratamientosRealizados",
                columns: new[] { "ClinicaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Tratamientos_ClinicaId_Id",
                table: "Tratamientos",
                columns: new[] { "ClinicaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Pacientes_ClinicaId_Id",
                table: "Pacientes",
                columns: new[] { "ClinicaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Odontologos_ClinicaId_Id",
                table: "Odontologos",
                columns: new[] { "ClinicaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_HistoriasClinicas_ClinicaId_Id",
                table: "HistoriasClinicas",
                columns: new[] { "ClinicaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ConsultasClinicas_ClinicaId_Id",
                table: "ConsultasClinicas",
                columns: new[] { "ClinicaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Citas_ClinicaId_Id",
                table: "Citas",
                columns: new[] { "ClinicaId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosRealizados_ClinicaId",
                table: "TratamientosRealizados",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosRealizados_ClinicaId_ConsultaId",
                table: "TratamientosRealizados",
                columns: new[] { "ClinicaId", "ConsultaId" });

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosRealizados_ClinicaId_OdontologoId",
                table: "TratamientosRealizados",
                columns: new[] { "ClinicaId", "OdontologoId" });

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosRealizados_ClinicaId_PacienteId",
                table: "TratamientosRealizados",
                columns: new[] { "ClinicaId", "PacienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosRealizados_ClinicaId_TratamientoId",
                table: "TratamientosRealizados",
                columns: new[] { "ClinicaId", "TratamientoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tratamientos_ClinicaId",
                table: "Tratamientos",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tratamientos_ClinicaId_Nombre",
                table: "Tratamientos",
                columns: new[] { "ClinicaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosOdontograma_ClinicaId",
                table: "RegistrosOdontograma",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosOdontograma_ClinicaId_OdontologoId",
                table: "RegistrosOdontograma",
                columns: new[] { "ClinicaId", "OdontologoId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosOdontograma_ClinicaId_PacienteId",
                table: "RegistrosOdontograma",
                columns: new[] { "ClinicaId", "PacienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_ClinicaId",
                table: "RegistrosAuditoria",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_ClinicaId_FechaHora",
                table: "RegistrosAuditoria",
                columns: new[] { "ClinicaId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ClinicaId",
                table: "Pagos",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ClinicaId_Fecha",
                table: "Pagos",
                columns: new[] { "ClinicaId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ClinicaId_TratamientoRealizadoId",
                table: "Pagos",
                columns: new[] { "ClinicaId", "TratamientoRealizadoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pacientes_ClinicaId",
                table: "Pacientes",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pacientes_ClinicaId_Identificacion",
                table: "Pacientes",
                columns: new[] { "ClinicaId", "Identificacion" },
                unique: true,
                filter: "[Identificacion] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Odontologos_ClinicaId",
                table: "Odontologos",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_Odontologos_ClinicaId_NumeroLicencia",
                table: "Odontologos",
                columns: new[] { "ClinicaId", "NumeroLicencia" },
                unique: true,
                filter: "[NumeroLicencia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriasClinicas_ClinicaId",
                table: "HistoriasClinicas",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriasClinicas_ClinicaId_PacienteId",
                table: "HistoriasClinicas",
                columns: new[] { "ClinicaId", "PacienteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasClinicas_ClinicaId",
                table: "ConsultasClinicas",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasClinicas_ClinicaId_CitaId",
                table: "ConsultasClinicas",
                columns: new[] { "ClinicaId", "CitaId" },
                unique: true,
                filter: "[CitaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasClinicas_ClinicaId_HistoriaClinicaId",
                table: "ConsultasClinicas",
                columns: new[] { "ClinicaId", "HistoriaClinicaId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasClinicas_ClinicaId_OdontologoId",
                table: "ConsultasClinicas",
                columns: new[] { "ClinicaId", "OdontologoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasClinicas_ClinicaId_PacienteId",
                table: "ConsultasClinicas",
                columns: new[] { "ClinicaId", "PacienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Citas_ClinicaId_OdontologoId_Fecha_HoraInicio",
                table: "Citas",
                columns: new[] { "ClinicaId", "OdontologoId", "Fecha", "HoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Citas_ClinicaId_PacienteId_Fecha_HoraInicio",
                table: "Citas",
                columns: new[] { "ClinicaId", "PacienteId", "Fecha", "HoraInicio" });

            migrationBuilder.AddForeignKey(
                name: "FK_Citas_Clinicas_ClinicaId",
                table: "Citas",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Citas_Odontologos_ClinicaId_OdontologoId",
                table: "Citas",
                columns: new[] { "ClinicaId", "OdontologoId" },
                principalTable: "Odontologos",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Citas_Pacientes_ClinicaId_PacienteId",
                table: "Citas",
                columns: new[] { "ClinicaId", "PacienteId" },
                principalTable: "Pacientes",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultasClinicas_Citas_ClinicaId_CitaId",
                table: "ConsultasClinicas",
                columns: new[] { "ClinicaId", "CitaId" },
                principalTable: "Citas",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultasClinicas_Clinicas_ClinicaId",
                table: "ConsultasClinicas",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultasClinicas_HistoriasClinicas_ClinicaId_HistoriaClinicaId",
                table: "ConsultasClinicas",
                columns: new[] { "ClinicaId", "HistoriaClinicaId" },
                principalTable: "HistoriasClinicas",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultasClinicas_Odontologos_ClinicaId_OdontologoId",
                table: "ConsultasClinicas",
                columns: new[] { "ClinicaId", "OdontologoId" },
                principalTable: "Odontologos",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultasClinicas_Pacientes_ClinicaId_PacienteId",
                table: "ConsultasClinicas",
                columns: new[] { "ClinicaId", "PacienteId" },
                principalTable: "Pacientes",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HistoriasClinicas_Clinicas_ClinicaId",
                table: "HistoriasClinicas",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HistoriasClinicas_Pacientes_ClinicaId_PacienteId",
                table: "HistoriasClinicas",
                columns: new[] { "ClinicaId", "PacienteId" },
                principalTable: "Pacientes",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Odontologos_Clinicas_ClinicaId",
                table: "Odontologos",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pacientes_Clinicas_ClinicaId",
                table: "Pacientes",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Clinicas_ClinicaId",
                table: "Pagos",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_TratamientosRealizados_ClinicaId_TratamientoRealizadoId",
                table: "Pagos",
                columns: new[] { "ClinicaId", "TratamientoRealizadoId" },
                principalTable: "TratamientosRealizados",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosAuditoria_Clinicas_ClinicaId",
                table: "RegistrosAuditoria",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosOdontograma_Clinicas_ClinicaId",
                table: "RegistrosOdontograma",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosOdontograma_Odontologos_ClinicaId_OdontologoId",
                table: "RegistrosOdontograma",
                columns: new[] { "ClinicaId", "OdontologoId" },
                principalTable: "Odontologos",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosOdontograma_Pacientes_ClinicaId_PacienteId",
                table: "RegistrosOdontograma",
                columns: new[] { "ClinicaId", "PacienteId" },
                principalTable: "Pacientes",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tratamientos_Clinicas_ClinicaId",
                table: "Tratamientos",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosRealizados_Clinicas_ClinicaId",
                table: "TratamientosRealizados",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosRealizados_ConsultasClinicas_ClinicaId_ConsultaId",
                table: "TratamientosRealizados",
                columns: new[] { "ClinicaId", "ConsultaId" },
                principalTable: "ConsultasClinicas",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosRealizados_Odontologos_ClinicaId_OdontologoId",
                table: "TratamientosRealizados",
                columns: new[] { "ClinicaId", "OdontologoId" },
                principalTable: "Odontologos",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosRealizados_Pacientes_ClinicaId_PacienteId",
                table: "TratamientosRealizados",
                columns: new[] { "ClinicaId", "PacienteId" },
                principalTable: "Pacientes",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosRealizados_Tratamientos_ClinicaId_TratamientoId",
                table: "TratamientosRealizados",
                columns: new[] { "ClinicaId", "TratamientoId" },
                principalTable: "Tratamientos",
                principalColumns: new[] { "ClinicaId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Citas_Clinicas_ClinicaId",
                table: "Citas");

            migrationBuilder.DropForeignKey(
                name: "FK_Citas_Odontologos_ClinicaId_OdontologoId",
                table: "Citas");

            migrationBuilder.DropForeignKey(
                name: "FK_Citas_Pacientes_ClinicaId_PacienteId",
                table: "Citas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultasClinicas_Citas_ClinicaId_CitaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultasClinicas_Clinicas_ClinicaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultasClinicas_HistoriasClinicas_ClinicaId_HistoriaClinicaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultasClinicas_Odontologos_ClinicaId_OdontologoId",
                table: "ConsultasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultasClinicas_Pacientes_ClinicaId_PacienteId",
                table: "ConsultasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_HistoriasClinicas_Clinicas_ClinicaId",
                table: "HistoriasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_HistoriasClinicas_Pacientes_ClinicaId_PacienteId",
                table: "HistoriasClinicas");

            migrationBuilder.DropForeignKey(
                name: "FK_Odontologos_Clinicas_ClinicaId",
                table: "Odontologos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pacientes_Clinicas_ClinicaId",
                table: "Pacientes");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Clinicas_ClinicaId",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_TratamientosRealizados_ClinicaId_TratamientoRealizadoId",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosAuditoria_Clinicas_ClinicaId",
                table: "RegistrosAuditoria");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosOdontograma_Clinicas_ClinicaId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosOdontograma_Odontologos_ClinicaId_OdontologoId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosOdontograma_Pacientes_ClinicaId_PacienteId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropForeignKey(
                name: "FK_Tratamientos_Clinicas_ClinicaId",
                table: "Tratamientos");

            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosRealizados_Clinicas_ClinicaId",
                table: "TratamientosRealizados");

            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosRealizados_ConsultasClinicas_ClinicaId_ConsultaId",
                table: "TratamientosRealizados");

            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosRealizados_Odontologos_ClinicaId_OdontologoId",
                table: "TratamientosRealizados");

            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosRealizados_Pacientes_ClinicaId_PacienteId",
                table: "TratamientosRealizados");

            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosRealizados_Tratamientos_ClinicaId_TratamientoId",
                table: "TratamientosRealizados");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TratamientosRealizados_ClinicaId_Id",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_TratamientosRealizados_ClinicaId",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_TratamientosRealizados_ClinicaId_ConsultaId",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_TratamientosRealizados_ClinicaId_OdontologoId",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_TratamientosRealizados_ClinicaId_PacienteId",
                table: "TratamientosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_TratamientosRealizados_ClinicaId_TratamientoId",
                table: "TratamientosRealizados");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Tratamientos_ClinicaId_Id",
                table: "Tratamientos");

            migrationBuilder.DropIndex(
                name: "IX_Tratamientos_ClinicaId",
                table: "Tratamientos");

            migrationBuilder.DropIndex(
                name: "IX_Tratamientos_ClinicaId_Nombre",
                table: "Tratamientos");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosOdontograma_ClinicaId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosOdontograma_ClinicaId_OdontologoId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosOdontograma_ClinicaId_PacienteId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosAuditoria_ClinicaId",
                table: "RegistrosAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosAuditoria_ClinicaId_FechaHora",
                table: "RegistrosAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_ClinicaId",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_ClinicaId_Fecha",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_ClinicaId_TratamientoRealizadoId",
                table: "Pagos");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Pacientes_ClinicaId_Id",
                table: "Pacientes");

            migrationBuilder.DropIndex(
                name: "IX_Pacientes_ClinicaId",
                table: "Pacientes");

            migrationBuilder.DropIndex(
                name: "IX_Pacientes_ClinicaId_Identificacion",
                table: "Pacientes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Odontologos_ClinicaId_Id",
                table: "Odontologos");

            migrationBuilder.DropIndex(
                name: "IX_Odontologos_ClinicaId",
                table: "Odontologos");

            migrationBuilder.DropIndex(
                name: "IX_Odontologos_ClinicaId_NumeroLicencia",
                table: "Odontologos");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_HistoriasClinicas_ClinicaId_Id",
                table: "HistoriasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_HistoriasClinicas_ClinicaId",
                table: "HistoriasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_HistoriasClinicas_ClinicaId_PacienteId",
                table: "HistoriasClinicas");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ConsultasClinicas_ClinicaId_Id",
                table: "ConsultasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_ConsultasClinicas_ClinicaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_ConsultasClinicas_ClinicaId_CitaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_ConsultasClinicas_ClinicaId_HistoriaClinicaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_ConsultasClinicas_ClinicaId_OdontologoId",
                table: "ConsultasClinicas");

            migrationBuilder.DropIndex(
                name: "IX_ConsultasClinicas_ClinicaId_PacienteId",
                table: "ConsultasClinicas");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Citas_ClinicaId_Id",
                table: "Citas");

            migrationBuilder.DropIndex(
                name: "IX_Citas_ClinicaId_OdontologoId_Fecha_HoraInicio",
                table: "Citas");

            migrationBuilder.DropIndex(
                name: "IX_Citas_ClinicaId_PacienteId_Fecha_HoraInicio",
                table: "Citas");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "TratamientosRealizados");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "Tratamientos");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "RegistrosOdontograma");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "RegistrosAuditoria");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "Pacientes");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "Odontologos");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "HistoriasClinicas");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "ConsultasClinicas");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "Citas");

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosRealizados_ConsultaId",
                table: "TratamientosRealizados",
                column: "ConsultaId");

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosRealizados_OdontologoId",
                table: "TratamientosRealizados",
                column: "OdontologoId");

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosRealizados_PacienteId",
                table: "TratamientosRealizados",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosRealizados_TratamientoId",
                table: "TratamientosRealizados",
                column: "TratamientoId");

            migrationBuilder.CreateIndex(
                name: "IX_Tratamientos_Nombre",
                table: "Tratamientos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosOdontograma_OdontologoId",
                table: "RegistrosOdontograma",
                column: "OdontologoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pacientes_Identificacion",
                table: "Pacientes",
                column: "Identificacion",
                unique: true,
                filter: "[Identificacion] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Odontologos_NumeroLicencia",
                table: "Odontologos",
                column: "NumeroLicencia",
                unique: true,
                filter: "[NumeroLicencia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasClinicas_HistoriaClinicaId",
                table: "ConsultasClinicas",
                column: "HistoriaClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasClinicas_OdontologoId",
                table: "ConsultasClinicas",
                column: "OdontologoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasClinicas_PacienteId",
                table: "ConsultasClinicas",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Citas_OdontologoId_Fecha_HoraInicio",
                table: "Citas",
                columns: new[] { "OdontologoId", "Fecha", "HoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Citas_PacienteId_Fecha_HoraInicio",
                table: "Citas",
                columns: new[] { "PacienteId", "Fecha", "HoraInicio" });

            migrationBuilder.AddForeignKey(
                name: "FK_Citas_Odontologos_OdontologoId",
                table: "Citas",
                column: "OdontologoId",
                principalTable: "Odontologos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Citas_Pacientes_PacienteId",
                table: "Citas",
                column: "PacienteId",
                principalTable: "Pacientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultasClinicas_Citas_CitaId",
                table: "ConsultasClinicas",
                column: "CitaId",
                principalTable: "Citas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultasClinicas_HistoriasClinicas_HistoriaClinicaId",
                table: "ConsultasClinicas",
                column: "HistoriaClinicaId",
                principalTable: "HistoriasClinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultasClinicas_Odontologos_OdontologoId",
                table: "ConsultasClinicas",
                column: "OdontologoId",
                principalTable: "Odontologos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultasClinicas_Pacientes_PacienteId",
                table: "ConsultasClinicas",
                column: "PacienteId",
                principalTable: "Pacientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HistoriasClinicas_Pacientes_PacienteId",
                table: "HistoriasClinicas",
                column: "PacienteId",
                principalTable: "Pacientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_TratamientosRealizados_TratamientoRealizadoId",
                table: "Pagos",
                column: "TratamientoRealizadoId",
                principalTable: "TratamientosRealizados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosOdontograma_Odontologos_OdontologoId",
                table: "RegistrosOdontograma",
                column: "OdontologoId",
                principalTable: "Odontologos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosOdontograma_Pacientes_PacienteId",
                table: "RegistrosOdontograma",
                column: "PacienteId",
                principalTable: "Pacientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosRealizados_ConsultasClinicas_ConsultaId",
                table: "TratamientosRealizados",
                column: "ConsultaId",
                principalTable: "ConsultasClinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosRealizados_Odontologos_OdontologoId",
                table: "TratamientosRealizados",
                column: "OdontologoId",
                principalTable: "Odontologos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosRealizados_Pacientes_PacienteId",
                table: "TratamientosRealizados",
                column: "PacienteId",
                principalTable: "Pacientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosRealizados_Tratamientos_TratamientoId",
                table: "TratamientosRealizados",
                column: "TratamientoId",
                principalTable: "Tratamientos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
