using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaDigital.Data.Migrations
{
    /// <summary>
    /// La bitácora de auditoría solo admite inserciones. El disparador aplica a cualquier usuario de la
    /// base, incluido el de la aplicación, así que un error o un acceso indebido no puede borrar el rastro.
    /// </summary>
    public partial class ProtegerAuditoriaSoloLectura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER dbo.TR_RegistrosAuditoria_SoloLectura
                ON dbo.RegistrosAuditoria
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    THROW 50001, N'La bitácora de auditoría no se puede modificar ni borrar.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_RegistrosAuditoria_SoloLectura;");
        }
    }
}
