using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Salud;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// El check de migraciones es el que decide el estado "schema-outdated". Se
/// prueba contra la base temporal ya migrada, contra una base sin migraciones y
/// contra un servidor que no responde.
/// </summary>
public sealed class SaludHealthCheckTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task UnaBaseConTodasLasMigracionesEsSaludable()
    {
        await using var db = fixture.CreateContext();
        var check = CrearCheck(db);

        var resultado = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, resultado.Status);
    }

    [Fact]
    public async Task UnaBaseSinElHistorialDeMigracionesReportaPendientes()
    {
        await using var db = fixture.CreateContext();

        // master no tiene __EFMigrationsHistory: EF informa todas las migraciones
        // como pendientes. Solo se lee, nunca se escribe en master.
        var constructor = new SqlConnectionStringBuilder(db.Database.GetConnectionString())
        {
            InitialCatalog = "master"
        };

        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(constructor.ConnectionString)
            .Options;

        await using var sinMigrar = new ApplicationDbContext(opciones, SinClinica.Instancia);
        var check = CrearCheck(sinMigrar);

        var resultado = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, resultado.Status);
        Assert.Null(resultado.Exception);
    }

    [Fact]
    public async Task UnServidorInalcanzableReportaUnhealthyConLaExcepcion()
    {
        // El puerto 1 no tiene SQL Server. La comprobación debe fallar rápido y
        // capturar la excepción en lugar de propagarla al pipeline.
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=127.0.0.1,1;Database=SonrisaDigital;User Id=sa;Password=invalida;" +
                "Connect Timeout=1;TrustServerCertificate=True")
            .Options;

        await using var db = new ApplicationDbContext(opciones, SinClinica.Instancia);
        var check = CrearCheck(db);

        var resultado = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, resultado.Status);
        Assert.NotNull(resultado.Exception);
    }

    private static MigracionesPendientesHealthCheck CrearCheck(ApplicationDbContext db) =>
        new(db, NullLogger<MigracionesPendientesHealthCheck>.Instance);
}
