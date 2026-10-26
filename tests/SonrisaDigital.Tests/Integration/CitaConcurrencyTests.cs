using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Citas;
using SonrisaDigital.Services.Citas.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Prueba real contra SQL Server: dos recepcionistas intentan reservar el mismo
/// horario al mismo tiempo. La transacción Serializable debe dejar una sola cita.
/// </summary>
public sealed class CitaConcurrencyTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task DosReservasSimultaneasSoloGuardanUnaCita()
    {
        await using var preparacion = fixture.CreateContext();
        var paciente = await TestData.CrearPacienteAsync(preparacion);
        var odontologo = await TestData.CrearOdontologoAsync(preparacion);
        var fecha = ProximoDiaHabil();

        var modelo = new CitaFormDto
        {
            PacienteId = paciente.Id,
            OdontologoId = odontologo.Id,
            Fecha = fecha,
            HoraInicio = new TimeOnly(9, 0),
            HoraFin = new TimeOnly(10, 0),
            Motivo = "Reserva simultánea"
        };

        var barrera = new AsyncBarrier(2);

        await using var db1 = fixture.CreateContext();
        await using var db2 = fixture.CreateContext();

        var servicio1 = CrearServicio(db1, barrera);
        var servicio2 = CrearServicio(db2, barrera);

        var resultados = await Task.WhenAll(
            EjecutarAsync(() => servicio1.CreateAsync(modelo)),
            EjecutarAsync(() => servicio2.CreateAsync(modelo)));

        Assert.Equal(1, resultados.Count(resultado => resultado.Exito));
        Assert.Equal(1, resultados.Count(resultado => !resultado.Exito));

        await using var comprobacion = fixture.CreateContext();
        var citas = await comprobacion.Citas
            .Where(cita =>
                cita.OdontologoId == odontologo.Id &&
                cita.Fecha == fecha &&
                cita.HoraInicio == new TimeOnly(9, 0))
            .ToListAsync();

        Assert.Single(citas);
    }

    private static CitaService CrearServicio(
        SonrisaDigital.Data.ApplicationDbContext db,
        AsyncBarrier barrera)
    {
        var repositorio = new BarrierCitaRepository(new CitaRepository(db), barrera);

        return new CitaService(
            repositorio,
            new PacienteRepository(db),
            new OdontologoRepository(db),
            new FakeHistoriaClinicaService(),
            new FakeAuditoriaService(),
            TestSession.Crear(),
            new FakeAuthorizationService(),
            db,
            NullLogger<CitaService>.Instance);
    }

    private static async Task<(bool Exito, Exception? Error)> EjecutarAsync(Func<Task<int>> operacion)
    {
        try
        {
            await operacion();
            return (true, null);
        }
        catch (Exception excepcion)
        {
            return (false, excepcion);
        }
    }

    private static DateOnly ProximoDiaHabil()
    {
        var fecha = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        while (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            fecha = fecha.AddDays(1);
        }

        return fecha;
    }
}
