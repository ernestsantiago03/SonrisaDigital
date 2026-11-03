using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Tests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Crea una base de datos temporal y aislada para las pruebas de integración.
///
/// La cadena base se toma de SONRISADIGITAL_TEST_CONNECTION. Si no existe, se
/// usa SQL Server Express local con autenticación de Windows. La base se elimina
/// al terminar la clase de pruebas; nunca se toca la base de desarrollo ni la de
/// producción.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string ConexionLocalPorDefecto =
        @"Server=.\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    private string _cadenaMaestra = string.Empty;
    private string _nombreBase = string.Empty;

    public DbContextOptions<ApplicationDbContext> Options { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configurada = Environment.GetEnvironmentVariable("SONRISADIGITAL_TEST_CONNECTION");
        var cadenaBase = string.IsNullOrWhiteSpace(configurada)
            ? ConexionLocalPorDefecto
            : configurada;

        var constructor = new SqlConnectionStringBuilder(cadenaBase)
        {
            InitialCatalog = "master"
        };

        _cadenaMaestra = constructor.ConnectionString;
        _nombreBase = $"SonrisaDigital_Tests_{Guid.NewGuid():N}";

        try
        {
            await using var conexion = new SqlConnection(_cadenaMaestra);
            await conexion.OpenAsync();

            await using var comando = conexion.CreateCommand();
            comando.CommandText = $"CREATE DATABASE [{_nombreBase}];";
            await comando.ExecuteNonQueryAsync();
        }
        catch (Exception excepcion)
        {
            throw new InvalidOperationException(
                "No se pudo crear la base temporal de pruebas. " +
                "Verifica que SQL Server esté disponible y que SONRISADIGITAL_TEST_CONNECTION " +
                "apunte a una conexión con permiso para crear bases de datos.",
                excepcion);
        }

        constructor.InitialCatalog = _nombreBase;
        // Las conexiones se cierran al terminar cada contexto; así la base temporal
        // se puede eliminar sin quedar retenida por el pool de conexiones.
        constructor.Pooling = false;

        Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(constructor.ConnectionString)
            .Options;

        await using var db = new ApplicationDbContext(Options, SinClinica.Instancia);
        await db.Database.MigrateAsync();

        // La migración histórica inserta la demo y la migración DemoFueraDeProduccion
        // la elimina cuando la base está limpia. Se conserva el resultado para poder
        // comprobar que una instalación nueva no nace con datos de demostración.
        DemoIdTrasMigrar = await db.Clinicas
            .AsNoTracking()
            .Where(clinic => clinic.Codigo == ClinicaDemoSeeder.Codigo)
            .Select(clinic => (int?)clinic.Id)
            .SingleOrDefaultAsync();

        // Las pruebas de integración siguen trabajando dentro de la demo, igual que
        // el runner E2E en Development. El seeder es el mismo que usa la aplicación.
        await ClinicaDemoSeeder.SeedAsync(db);

        ClinicaId = await db.Clinicas
            .AsNoTracking()
            .Where(clinic => clinic.Codigo == ClinicaDemoSeeder.Codigo)
            .Select(clinic => clinic.Id)
            .SingleAsync();
    }

    /// <summary>Clínica en la que trabajan las pruebas de integración.</summary>
    public int ClinicaId { get; private set; }

    /// <summary>
    /// Id de la demo que dejó la migración, o <c>null</c> si la migración de limpieza
    /// la eliminó. Debe ser <c>null</c> en una base nueva.
    /// </summary>
    public int? DemoIdTrasMigrar { get; private set; }

    public ApplicationDbContext CreateContext() => new(Options, new TenantFijo(ClinicaId));

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(_nombreBase))
        {
            return;
        }

        var constructor = new SqlConnectionStringBuilder(_cadenaMaestra);
        await using var conexion = new SqlConnection(constructor.ConnectionString);
        await conexion.OpenAsync();

        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"""
            IF DB_ID(N'{_nombreBase}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_nombreBase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_nombreBase}];
            END
            """;

        await comando.ExecuteNonQueryAsync();
    }
}
