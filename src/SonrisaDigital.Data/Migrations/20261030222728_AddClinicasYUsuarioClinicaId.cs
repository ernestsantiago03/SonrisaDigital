using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicasYUsuarioClinicaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClinicaId",
                table: "Usuarios",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Clinicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Rnc = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Horario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Eslogan = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnPrueba = table.Column<bool>(type: "bit", nullable: false),
                    LimiteUsuarios = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clinicas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ClinicaId",
                table: "Usuarios",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_Clinicas_Codigo",
                table: "Clinicas",
                column: "Codigo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Clinicas_ClinicaId",
                table: "Usuarios",
                column: "ClinicaId",
                principalTable: "Clinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Clinica Demo representa los datos que ya existían antes de la multiclínica.
            // El RNC queda vacío hasta confirmar el real (el valor de ejemplo no se migra).
            migrationBuilder.Sql("""
                INSERT INTO Clinicas (Nombre, Codigo, Rnc, Telefono, Correo, Direccion, Eslogan, LogoUrl,
                                      Estado, FechaAlta, [Plan], FechaVencimiento, EnPrueba, LimiteUsuarios, Horario)
                VALUES (N'Clinica Demo', N'clinica-demo', NULL, N'809-555-5555', N'contacto@sonrisadigital.com',
                        N'Av. Pedro A. Rivera, Plaza Comercial, Sonrisa Digital, 2do Nivel, La Vega, República Dominicana.',
                        N'"Tu sonrisa en las mejores manos profesionales."', NULL,
                        N'Activa', SYSUTCDATETIME(), NULL, NULL, 0, 0, N'Lunes a Viernes: 8:00 AM - 6:00 PM');

                UPDATE Usuarios
                SET ClinicaId = (SELECT Id FROM Clinicas WHERE Codigo = N'clinica-demo')
                WHERE ClinicaId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Clinicas_ClinicaId",
                table: "Usuarios");

            migrationBuilder.DropTable(
                name: "Clinicas");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_ClinicaId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ClinicaId",
                table: "Usuarios");
        }
    }
}
