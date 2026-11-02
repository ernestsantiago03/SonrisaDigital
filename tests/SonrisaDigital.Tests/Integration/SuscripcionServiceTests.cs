using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Suscripciones;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Suscripciones;
using SonrisaDigital.Services.Suscripciones.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Suscripción de clínicas: prueba desde el primer ingreso, registro de pagos y
/// extensión manual. Usa la misma base temporal que el resto de integración.
/// </summary>
public sealed class SuscripcionServiceTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task LaPruebaEmpiezaEnElPrimerIngresoYVenceEnTreintaDias()
    {
        await using var db = fixture.CreateContext();
        var clinica = await CrearClinicaAsync(db);
        var (provider, _) = await CrearProveedorAsync();

        await using (provider)
        {
            var servicio = provider.GetRequiredService<ISuscripcionService>();

            var iniciada = await servicio.IniciarPruebaSiCorrespondeAsync(clinica.Id);
            var repetida = await servicio.IniciarPruebaSiCorrespondeAsync(clinica.Id);

            Assert.True(iniciada);
            Assert.False(repetida);

            var actualizada = await db.Clinicas.AsNoTracking().SingleAsync(item => item.Id == clinica.Id);
            var hoy = ReglaSuscripcion.Hoy();
            Assert.Equal(hoy, actualizada.FechaPrimerIngreso);
            Assert.Equal(ReglaSuscripcion.VencimientoDePrueba(hoy), actualizada.FechaVencimiento);
            Assert.True(actualizada.EnPrueba);
            Assert.Equal("Prueba", actualizada.Plan);
        }
    }

    [Fact]
    public async Task RegistrarUnPagoTerminaLaPruebaYExtiendeElVencimiento()
    {
        await using var db = fixture.CreateContext();
        var hoy = ReglaSuscripcion.Hoy();
        var clinica = await CrearClinicaAsync(db, enPrueba: false, fechaVencimiento: hoy.AddDays(10));
        var (provider, usuarioId) = await CrearProveedorAsync();

        await using (provider)
        {
            var servicio = provider.GetRequiredService<ISuscripcionService>();
            var vencimientoEsperado = ReglaSuscripcion.Renovar(clinica.FechaVencimiento, hoy);

            await servicio.RegistrarPagoAsync(new RegistrarPagoSuscripcionDto
            {
                ClinicaId = clinica.Id,
                Monto = 75m,
                Meses = 1,
                Metodo = MetodoPagoSuscripcion.Transferencia,
                Referencia = "TRF-001"
            });

            var actualizada = await db.Clinicas.AsNoTracking().SingleAsync(item => item.Id == clinica.Id);
            Assert.False(actualizada.EnPrueba);
            Assert.Equal(EstadoClinica.Activa, actualizada.Estado);
            Assert.Equal(vencimientoEsperado, actualizada.FechaVencimiento);
            Assert.Equal("Mensual", actualizada.Plan);

            var pago = await db.PagosSuscripcion.AsNoTracking().SingleAsync(item => item.ClinicaId == clinica.Id);
            Assert.Equal(75m, pago.Monto);
            Assert.Equal("USD", pago.Moneda);
            Assert.Equal("TRF-001", pago.Referencia);
            Assert.Equal(usuarioId, pago.RegistradoPorId);
            Assert.Equal(vencimientoEsperado, pago.PeriodoHasta);
        }
    }

    [Fact]
    public async Task ExtenderDiasSumaAlVencimientoYQuedaEnAuditoria()
    {
        await using var db = fixture.CreateContext();
        var hoy = ReglaSuscripcion.Hoy();
        var clinica = await CrearClinicaAsync(db, enPrueba: false, fechaVencimiento: hoy.AddDays(5));
        var (provider, _) = await CrearProveedorAsync();

        await using (provider)
        {
            var servicio = provider.GetRequiredService<ISuscripcionService>();

            await servicio.ExtenderAsync(new ExtenderSuscripcionDto
            {
                ClinicaId = clinica.Id,
                Dias = 15,
                Motivo = "Compensación por mantenimiento"
            });

            var actualizada = await db.Clinicas.AsNoTracking().SingleAsync(item => item.Id == clinica.Id);
            Assert.Equal(hoy.AddDays(20), actualizada.FechaVencimiento);

            var auditoria = (FakeAuditoriaService)provider.GetRequiredService<IAuditoriaService>();
            Assert.Contains(auditoria.Registros, registro =>
                registro.Accion == "Extendió" && registro.ClinicaId == clinica.Id);
        }
    }

    [Fact]
    public async Task SinPermisoNoSePuedeRegistrarUnPago()
    {
        await using var db = fixture.CreateContext();
        var clinica = await CrearClinicaAsync(db, enPrueba: false, fechaVencimiento: ReglaSuscripcion.Hoy().AddDays(5));
        var (provider, _) = await CrearProveedorAsync(permitir: false);

        await using (provider)
        {
            var servicio = provider.GetRequiredService<ISuscripcionService>();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.RegistrarPagoAsync(
                new RegistrarPagoSuscripcionDto
                {
                    ClinicaId = clinica.Id,
                    Monto = 75m
                }));
        }
    }

    [Fact]
    public async Task ElResumenMuestraElPrecioDelPlanYLosDiasRestantes()
    {
        await using var db = fixture.CreateContext();
        var hoy = ReglaSuscripcion.Hoy();
        var clinica = await CrearClinicaAsync(db, enPrueba: false, fechaVencimiento: hoy.AddDays(2));
        var (provider, _) = await CrearProveedorAsync();

        await using (provider)
        {
            var servicio = provider.GetRequiredService<ISuscripcionService>();

            var resumen = await servicio.ObtenerAsync(clinica.Id);

            Assert.NotNull(resumen);
            Assert.Equal(75m, resumen.PrecioMensualUsd);
            Assert.Equal(AccesoClinica.Permitido, resumen.Acceso);
            Assert.Equal(2, resumen.DiasRestantes);
            Assert.Empty(resumen.Pagos);
        }
    }

    [Fact]
    public async Task UnPagoNoReactivaUnaClinicaSuspendidaManualmente()
    {
        await using var db = fixture.CreateContext();
        var hoy = ReglaSuscripcion.Hoy();
        var clinica = await CrearClinicaAsync(
            db, enPrueba: false, fechaVencimiento: hoy.AddDays(-10), estado: EstadoClinica.Suspendida);
        var (provider, _) = await CrearProveedorAsync();

        await using (provider)
        {
            var servicio = provider.GetRequiredService<ISuscripcionService>();

            await servicio.RegistrarPagoAsync(new RegistrarPagoSuscripcionDto
            {
                ClinicaId = clinica.Id,
                Monto = 75m,
                Metodo = MetodoPagoSuscripcion.Efectivo
            });

            var actualizada = await db.Clinicas.AsNoTracking().SingleAsync(item => item.Id == clinica.Id);
            Assert.Equal(EstadoClinica.Suspendida, actualizada.Estado);
            Assert.True(actualizada.FechaVencimiento > hoy);
            Assert.False(actualizada.EnPrueba);
        }
    }

    [Fact]
    public async Task ElAvisoApareceCuandoLaMensualidadEstaPorVencer()
    {
        await using var db = fixture.CreateContext();
        var clinica = await CrearClinicaAsync(
            db, enPrueba: false, fechaVencimiento: ReglaSuscripcion.Hoy().AddDays(2));
        var (provider, _) = await CrearProveedorAsync(clinicaId: clinica.Id);

        await using (provider)
        {
            var aviso = await provider.GetRequiredService<ISuscripcionAvisoService>().ObtenerAsync();

            Assert.NotNull(aviso);
            Assert.Equal(AccesoClinica.Permitido, aviso.Acceso);
            Assert.Equal(2, aviso.DiasRestantes);
            Assert.False(aviso.EnPrueba);
        }
    }

    [Fact]
    public async Task ElAvisoNoUsaElContextoDelCircuito()
    {
        await using var db = fixture.CreateContext();
        var clinica = await CrearClinicaAsync(
            db, enPrueba: false, fechaVencimiento: ReglaSuscripcion.Hoy().AddDays(2));
        var (provider, _) = await CrearProveedorAsync(clinicaId: clinica.Id);

        await using (provider)
        {
            // El layout de Blazor consulta el aviso mientras la página está usando
            // el DbContext del circuito. Si compartieran contexto, una de las dos
            // consultas fallaría ("A second operation was started...").
            var contextoDelCircuito = provider.GetRequiredService<ApplicationDbContext>();
            await contextoDelCircuito.DisposeAsync();

            var aviso = await provider.GetRequiredService<ISuscripcionAvisoService>().ObtenerAsync();

            Assert.NotNull(aviso);
            Assert.Equal(2, aviso.DiasRestantes);
        }
    }

    private static async Task<Clinica> CrearClinicaAsync(
        ApplicationDbContext db,
        bool enPrueba = true,
        DateTime? fechaVencimiento = null,
        EstadoClinica estado = EstadoClinica.Activa)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var clinica = new Clinica
        {
            Nombre = $"Clínica {sufijo}",
            Codigo = $"test-{sufijo}",
            Estado = estado,
            FechaAlta = DateTime.UtcNow,
            Plan = enPrueba ? "Prueba" : "Mensual",
            EnPrueba = enPrueba,
            FechaVencimiento = fechaVencimiento,
            LimiteUsuarios = 10
        };

        db.Clinicas.Add(clinica);
        await db.SaveChangesAsync();
        return clinica;
    }

    private async Task<(ServiceProvider Provider, string UsuarioId)> CrearProveedorAsync(
        bool permitir = true,
        int? clinicaId = null,
        params string[] denegadas)
    {
        var usuarioId = $"user-{Guid.NewGuid():N}";

        await using var db = fixture.CreateContext();
        db.Users.Add(new Usuario
        {
            Id = usuarioId,
            UserName = $"{usuarioId}@pruebas.local",
            NormalizedUserName = $"{usuarioId}@PRUEBAS.LOCAL".ToUpperInvariant(),
            Email = $"{usuarioId}@pruebas.local",
            NormalizedEmail = $"{usuarioId}@PRUEBAS.LOCAL".ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            Nombre = "Dueño de pruebas",
            Estado = EstadoRegistro.Activo
        });
        await db.SaveChangesAsync();

        var cadenaConexion = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("La base de pruebas no tiene cadena de conexión.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(cadenaConexion));
        services.AddSingleton<ITenantProvider>(new TenantMutable { ClinicaId = clinicaId ?? fixture.ClinicaId });
        services.AddSingleton<ICurrentUserService>(TestSession.Crear(usuarioId));
        services.AddSingleton<IAuthorizationService>(new FakeAuthorizationService(permitir, denegadas));
        services.AddSingleton<IAuditoriaService>(new FakeAuditoriaService());
        services.Configure<SuscripcionOptions>(options => options.PrecioMensualUsd = 75m);
        services.AddScoped<ISuscripcionService, SuscripcionService>();
        services.AddScoped<ISuscripcionAvisoService, SuscripcionAvisoService>();

        return (services.BuildServiceProvider(), usuarioId);
    }
}
