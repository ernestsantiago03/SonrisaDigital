using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioIdToPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "Pagos",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_UsuarioId",
                table: "Pagos",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Usuarios_UsuarioId",
                table: "Pagos",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Usuarios_UsuarioId",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_UsuarioId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Pagos");
        }
    }
}
