using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SonrisaDigital.Data;

/// <summary>
/// Fábrica usada únicamente por las herramientas de diseño de EF Core
/// (dotnet ef migrations / database update).
///
/// Permite generar scripts y aplicar migraciones sin ejecutar Program.cs y, por
/// tanto, sin exigir que la aplicación web tenga ya toda la configuración de
/// producción. La cadena real se toma de la variable de entorno que también usa
/// el despliegue; si no existe, se usa la base local de desarrollo.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // La doble barra baja es la convención de .NET para variables de entorno
        // que representan secciones de configuración.
        var cadenaConexion = Environment.GetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            cadenaConexion =
                "Server=.\\SQLEXPRESS;Database=SonrisaDigital;Trusted_Connection=True;" +
                "TrustServerCertificate=True;MultipleActiveResultSets=True";
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(cadenaConexion)
            .Options;

        return new ApplicationDbContext(options, SonrisaDigital.Data.Tenancy.SinClinica.Instancia);
    }
}
