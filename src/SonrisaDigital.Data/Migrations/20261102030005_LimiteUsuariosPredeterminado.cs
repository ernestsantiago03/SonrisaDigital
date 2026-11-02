using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <inheritdoc />
    public partial class LimiteUsuariosPredeterminado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Las clínicas anteriores al plan quedaron con el límite en cero.
            // Se les asigna el límite vigente de 10 usuarios activos.
            migrationBuilder.Sql("""
                UPDATE Clinicas
                SET LimiteUsuarios = 10
                WHERE LimiteUsuarios <= 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se revierte: no hay forma de distinguir las clínicas que tenían
            // cero de las que ya tenían el límite vigente antes de la migración.
        }
    }
}
