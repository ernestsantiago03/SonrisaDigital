using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class MotivoSuspensionYReactivacionPorPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoSuspension",
                table: "Clinicas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // Las suspensiones anteriores a este cambio no distinguían el motivo.
            // Se tratan como administrativas para que un pago no las levante solo.
            migrationBuilder.Sql("""
                UPDATE Clinicas
                SET MotivoSuspension = 'DecisionAdministrativa'
                WHERE Estado = 'Suspendida'
                  AND MotivoSuspension IS NULL;
                """);

            // La clínica demo existente se convierte en prueba para que su primer
            // ingreso inicie los 30 días; ya no se apoya en una fecha nula eterna.
            migrationBuilder.Sql("""
                UPDATE Clinicas
                SET EnPrueba = 1,
                    [Plan] = 'Prueba'
                WHERE Codigo = 'clinica-demo'
                  AND FechaPrimerIngreso IS NULL
                  AND FechaVencimiento IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se revierte la clasificación de suspensiones ni el modo de la demo:
            // no hay forma de distinguir los datos anteriores de los ya corregidos.
            migrationBuilder.DropColumn(
                name: "MotivoSuspension",
                table: "Clinicas");
        }
    }
}
