using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class DemoFueraDeProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La migración histórica inserta "clinica-demo" en cualquier base nueva.
            // Una instalación de producción debe arrancar sin datos de demostración,
            // pero la base de desarrollo ya tiene usuarios y datos reales de prueba.
            //
            // La demo solo se elimina cuando está completamente vacía. Si tiene
            // cualquier usuario o registro de negocio, se conserva para no destruir
            // trabajo existente. En una base nueva de producción no hay filas y la
            // eliminación ocurre antes de que el seeding cree al Dueño.
            migrationBuilder.Sql("""
                DECLARE @clinicaDemoId int = (
                    SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo'
                );

                IF @clinicaDemoId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Usuarios WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM Pacientes WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM Odontologos WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM HistoriasClinicas WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM Citas WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM ConsultasClinicas WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM RegistrosOdontograma WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM ArchivosPaciente WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM HistorialOdontograma WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM Tratamientos WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM TratamientosRealizados WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM Pagos WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM PagosSuscripcion WHERE ClinicaId = @clinicaDemoId)
                   AND NOT EXISTS (SELECT 1 FROM RegistrosAuditoria WHERE ClinicaId = @clinicaDemoId)
                BEGIN
                    DELETE FROM Clinicas WHERE Id = @clinicaDemoId;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se revierte: una demo eliminada de una base limpia no debe
            // reaparecer al bajar de versión. En desarrollo la vuelve a crear
            // ClinicaDemoSeeder al arrancar la aplicación.
        }
    }
}
