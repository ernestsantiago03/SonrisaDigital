using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Citas;
using SonrisaDigital.Services.Citas.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Pruebas de las reglas de agenda que se aplican en el servicio, antes de
/// abrir la transacción. Estas reglas deben cumplirse aunque la petición no
/// venga del formulario web.
/// </summary>
public sealed class CitaServiceValidationTests
{
    [Fact]
    public async Task RechazaPacienteVacio()
    {
        var modelo = ModeloValido();
        modelo.PacienteId = 0;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().CreateAsync(modelo));

        Assert.Equal("El paciente es obligatorio.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaOdontologoVacio()
    {
        var modelo = ModeloValido();
        modelo.OdontologoId = 0;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().CreateAsync(modelo));

        Assert.Equal("El odontólogo es obligatorio.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaFechaVacia()
    {
        var modelo = ModeloValido();
        modelo.Fecha = default;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().CreateAsync(modelo));

        Assert.Equal("La fecha no es válida.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaHorasVacias()
    {
        var modelo = ModeloValido();
        modelo.HoraInicio = default;
        modelo.HoraFin = default;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().CreateAsync(modelo));

        Assert.Equal("Las horas no son válidas.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaHoraFinAnteriorAInicio()
    {
        var modelo = ModeloValido();
        modelo.HoraInicio = new TimeOnly(10, 0);
        modelo.HoraFin = new TimeOnly(9, 0);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().CreateAsync(modelo));

        Assert.Equal("La hora de fin debe ser posterior a la hora de inicio.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaDomingo()
    {
        var modelo = ModeloValido();
        modelo.Fecha = ProximoDia(DayOfWeek.Sunday);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().CreateAsync(modelo));

        Assert.Equal("Las citas solo pueden programarse de lunes a sábado.", excepcion.Message);
    }


    [Fact]
    public async Task RechazaHorarioFueraDeAtencion()
    {
        var modelo = ModeloValido();
        modelo.HoraInicio = new TimeOnly(7, 0);
        modelo.HoraFin = new TimeOnly(8, 0);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().CreateAsync(modelo));

        Assert.Equal("El horario de atención es de 8:00 a.m. a 6:00 p.m.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaCitaQueCruzaElAlmuerzo()
    {
        var modelo = ModeloValido();
        modelo.HoraInicio = new TimeOnly(11, 30);
        modelo.HoraFin = new TimeOnly(12, 30);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().CreateAsync(modelo));

        Assert.Equal(
            "La cita no puede cruzar el horario de almuerzo (12:00 a 2:00 p.m.).",
            excepcion.Message);
    }

    [Fact]
    public async Task RechazaCitaQueEmpiezaDuranteElAlmuerzo()
    {
        var modelo = ModeloValido();
        modelo.HoraInicio = new TimeOnly(13, 0);
        modelo.HoraFin = new TimeOnly(14, 0);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().CreateAsync(modelo));

        Assert.Equal(
            "No se agendan citas durante el almuerzo (12:00 a 2:00 p.m.).",
            excepcion.Message);
    }

    [Fact]
    public async Task RechazaOperacionSinPermiso()
    {
        var servicio = CrearServicio(new FakeAuthorizationService(permitir: false));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.CreateAsync(ModeloValido()));
    }

    private static CitaService CrearServicio(IAuthorizationService? autorizacion = null)
    {
        ICurrentUserService usuario = TestSession.Crear();

        return new CitaService(
            null!,
            null!,
            null!,
            new FakeHistoriaClinicaService(),
            new FakeAuditoriaService(),
            usuario,
            autorizacion ?? new FakeAuthorizationService(),
            null!,
            NullLogger<CitaService>.Instance);
    }

    private static CitaFormDto ModeloValido() => new()
    {
        PacienteId = 1,
        OdontologoId = 1,
        Fecha = ProximoDiaHabil(),
        HoraInicio = new TimeOnly(9, 0),
        HoraFin = new TimeOnly(10, 0),
        Motivo = "Consulta de prueba"
    };

    private static DateOnly ProximoDiaHabil()
    {
        var fecha = RelojNegocio.HoyDateOnly().AddDays(1);
        while (fecha.DayOfWeek is DayOfWeek.Sunday)
        {
            fecha = fecha.AddDays(1);
        }

        return fecha;
    }

    private static DateOnly ProximoDia(DayOfWeek dia)
    {
        var fecha = RelojNegocio.HoyDateOnly();
        while (fecha.DayOfWeek != dia)
        {
            fecha = fecha.AddDays(1);
        }

        return fecha;
    }
}
