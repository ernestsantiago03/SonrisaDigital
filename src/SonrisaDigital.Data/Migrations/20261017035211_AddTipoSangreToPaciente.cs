using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <summary>
    /// Migración de corrección.
    ///
    /// Qué pasó: la columna "Pacientes.TipoSangre" existe en el modelo y en la base de datos
    /// actual, pero NINGUNA migración anterior la creaba (se agregó al modelo directamente, sin
    /// generar su migración). Por eso, si alguien creara la base desde cero aplicando las
    /// migraciones, la aplicación fallaría con "Invalid column name 'TipoSangre'".
    ///
    /// Qué hace: únicamente crea la columna (y la borra si la migración se revierte). Así una
    /// base nueva queda completa y una base existente no sufre cambios (la columna ya está ahí).
    /// </summary>
    /// <inheritdoc />
    public partial class AddTipoSangreToPaciente : Migration
    {
        /// <summary>
        /// Aplica el cambio: agrega la columna TipoSangre a la tabla Pacientes.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Texto opcional (nvarchar(max)): el tipo de sangre puede quedar sin registrar.
            migrationBuilder.AddColumn<string>(
                name: "TipoSangre",
                table: "Pacientes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <summary>
        /// Revierte el cambio: elimina la columna TipoSangre de la tabla Pacientes.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoSangre",
                table: "Pacientes");
        }
    }
}
