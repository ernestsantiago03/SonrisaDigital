using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanesTratamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanesTratamiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicaId = table.Column<int>(type: "int", nullable: false),
                    ConsultaId = table.Column<int>(type: "int", nullable: false),
                    TratamientoId = table.Column<int>(type: "int", nullable: false),
                    PiezaDentalId = table.Column<int>(type: "int", nullable: true),
                    Precio = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TratamientoRealizadoId = table.Column<int>(type: "int", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesTratamiento", x => x.Id);
                    table.UniqueConstraint("AK_PlanesTratamiento_ClinicaId_Id", x => new { x.ClinicaId, x.Id });
                    table.ForeignKey(
                        name: "FK_PlanesTratamiento_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanesTratamiento_ConsultasClinicas_ClinicaId_ConsultaId",
                        columns: x => new { x.ClinicaId, x.ConsultaId },
                        principalTable: "ConsultasClinicas",
                        principalColumns: new[] { "ClinicaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanesTratamiento_PiezasDentales_PiezaDentalId",
                        column: x => x.PiezaDentalId,
                        principalTable: "PiezasDentales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanesTratamiento_TratamientosRealizados_ClinicaId_TratamientoRealizadoId",
                        columns: x => new { x.ClinicaId, x.TratamientoRealizadoId },
                        principalTable: "TratamientosRealizados",
                        principalColumns: new[] { "ClinicaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanesTratamiento_Tratamientos_ClinicaId_TratamientoId",
                        columns: x => new { x.ClinicaId, x.TratamientoId },
                        principalTable: "Tratamientos",
                        principalColumns: new[] { "ClinicaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesTratamiento_ClinicaId",
                table: "PlanesTratamiento",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanesTratamiento_ClinicaId_ConsultaId",
                table: "PlanesTratamiento",
                columns: new[] { "ClinicaId", "ConsultaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesTratamiento_ClinicaId_TratamientoId",
                table: "PlanesTratamiento",
                columns: new[] { "ClinicaId", "TratamientoId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesTratamiento_ClinicaId_TratamientoRealizadoId",
                table: "PlanesTratamiento",
                columns: new[] { "ClinicaId", "TratamientoRealizadoId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesTratamiento_PiezaDentalId",
                table: "PlanesTratamiento",
                column: "PiezaDentalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanesTratamiento");
        }
    }
}
