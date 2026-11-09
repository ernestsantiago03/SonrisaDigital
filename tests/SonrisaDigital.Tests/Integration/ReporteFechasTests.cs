using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Reportes;
using SonrisaDigital.Services.Reportes.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Pagos y tratamientos guardan una fecha de calendario (DateOnly). El reporte de ese
/// día debe incluirlos sin importar la zona horaria del servidor ni la hora del registro.
/// </summary>
public sealed class ReporteFechasTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task UnPagoApareceEnSuDiaDelReporte()
    {
        var dia = new DateOnly(2035, 7, 14);
        var pagoId = await CrearPagoAsync(dia);

        await using var db = fixture.CreateContext();
        var repositorio = new ReporteRepository(db);

        var delDia = await repositorio.GetPagosAsync(dia, dia.AddDays(1));
        var deOtroDia = await repositorio.GetPagosAsync(dia.AddDays(1), dia.AddDays(2));

        Assert.Contains(delDia, pago => pago.Id == pagoId);
        Assert.DoesNotContain(deOtroDia, pago => pago.Id == pagoId);
    }

    [Fact]
    public async Task ElServicioDeReportesIncluyeUnPagoDelMismoDiaSinDesplazarloPorZonaHoraria()
    {
        var dia = new DateOnly(2035, 7, 15);
        var pagoId = await CrearPagoAsync(dia);

        await using var db = fixture.CreateContext();
        var servicio = new ReporteService(
            new ReporteRepository(db),
            TestSession.Crear(),
            new FakeAuthorizationService());

        var pagos = await servicio.GetPagosAsync(new ReporteFiltroDto(dia, dia));

        Assert.Contains(pagos, pago => pago.Id == pagoId);
    }

    [Fact]
    public async Task ElResumenIncluyeLosPagosDeLosDiasLimiteYExcluyeLosDeAfuera()
    {
        var desde = new DateOnly(2036, 6, 10);
        var hasta = desde.AddDays(2);
        await CrearPagoAsync(desde);
        await CrearPagoAsync(hasta);
        await CrearPagoAsync(desde.AddDays(-1));
        await CrearPagoAsync(hasta.AddDays(1));

        await using var db = fixture.CreateContext();
        var servicio = new ReporteService(
            new ReporteRepository(db),
            TestSession.Crear(),
            new FakeAuthorizationService());

        var resumen = await servicio.GetResumenAsync(new ReporteFiltroDto(desde, hasta));

        Assert.NotNull(resumen.Pagos);
        Assert.Equal(2, resumen.Pagos.CantidadPagos);
        Assert.Equal(50m, resumen.Pagos.TotalCobrado);
    }

    [Fact]
    public async Task LosPacientesNuevosSeCuentanConMedianocheLocalYNoConMedianocheUtc()
    {
        var dia = new DateOnly(2036, 4, 5);

        // 23:30 del día anterior en Santo Domingo (UTC-4).
        await CrearPacienteRegistradoAsync(new DateTime(2036, 4, 5, 3, 30, 0, DateTimeKind.Utc));
        // 00:30 del día consultado en Santo Domingo.
        await CrearPacienteRegistradoAsync(new DateTime(2036, 4, 5, 4, 30, 0, DateTimeKind.Utc));
        // 23:30 del día consultado en Santo Domingo.
        await CrearPacienteRegistradoAsync(new DateTime(2036, 4, 6, 3, 30, 0, DateTimeKind.Utc));

        await using var db = fixture.CreateContext();
        var servicio = new ReporteService(
            new ReporteRepository(db),
            TestSession.Crear(),
            new FakeAuthorizationService());

        var resumen = await servicio.GetResumenAsync(new ReporteFiltroDto(dia, dia));

        Assert.Equal(2, resumen.PacientesNuevos);
    }

    [Fact]
    public async Task ElServicioRechazaUnRangoDeReporteMayorAUnAnio()
    {
        var desde = new DateOnly(2036, 1, 1);
        var servicio = new ReporteService(
            null!,
            TestSession.Crear(),
            new FakeAuthorizationService());

        var excepcion = await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.GetResumenAsync(new ReporteFiltroDto(desde, desde.AddDays(367))));

        Assert.Contains("366", excepcion.Message);
    }

    private async Task CrearPacienteRegistradoAsync(DateTime fechaRegistroUtc)
    {
        await using var db = fixture.CreateContext();
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        db.Pacientes.Add(new Paciente
        {
            Nombres = $"Paciente {sufijo}",
            Apellidos = "Medianoche",
            Estado = EstadoRegistro.Activo,
            FechaRegistro = fechaRegistroUtc
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> CrearPagoAsync(DateOnly fecha)
    {
        await using var db = fixture.CreateContext();
        var usuario = await TestData.AsegurarUsuarioAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var catalogo = await TestData.CrearTratamientoCatalogoAsync(db);
        var realizado = await TestData.CrearTratamientoRealizadoAsync(
            db, paciente.Id, odontologo.Id, catalogo.Id, precio: 100m);

        var pago = new Pago
        {
            TratamientoRealizadoId = realizado.Id,
            Fecha = fecha,
            Monto = 25m,
            MetodoPago = MetodoPago.Efectivo,
            UsuarioId = usuario.Id
        };
        db.Pagos.Add(pago);
        await db.SaveChangesAsync();
        return pago.Id;
    }
}
