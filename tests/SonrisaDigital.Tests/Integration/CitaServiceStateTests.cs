using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Citas;
using SonrisaDigital.Services.Citas.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Pruebas reales contra SQL Server de la máquina de estados de citas. Se valida
/// que el servicio no dependa de los botones de la pantalla y que las transiciones
/// inválidas no dejen cambios parciales.
/// </summary>
public sealed class CitaServiceStateTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task RechazaAtenderUnaCitaProgramadaYNoLaModifica()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db);
        var servicio = CrearServicio(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SetEstadoAsync(cita.Id, EstadoCita.Atendida));

        await AssertEstadoAsync(cita.Id, EstadoCita.Programada);
    }

    [Fact]
    public async Task NoAsistioExigePermisoDeCancelar()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db);
        var servicio = CrearServicio(
            db,
            autorizacion: new FakeAuthorizationService(true, "Citas.Cancelar"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.SetEstadoAsync(cita.Id, EstadoCita.NoAsistio));

        await AssertEstadoAsync(cita.Id, EstadoCita.Programada);
    }

    [Fact]
    public async Task NoPermiteReabrirUnaCitaCancelada()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db, estado: EstadoCita.Cancelada);
        var servicio = CrearServicio(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SetEstadoAsync(cita.Id, EstadoCita.Programada));

        await AssertEstadoAsync(cita.Id, EstadoCita.Cancelada);
    }

    [Fact]
    public async Task AtenderUnaCitaConfirmadaRegistraSuConsulta()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db, estado: EstadoCita.Confirmada);
        var historia = new FakeHistoriaClinicaService();
        var servicio = CrearServicio(db, historia);

        await servicio.SetEstadoAsync(cita.Id, EstadoCita.Atendida);

        Assert.Equal(1, historia.ConsultasRegistradas);
        await AssertEstadoAsync(cita.Id, EstadoCita.Atendida);
    }

    [Fact]
    public async Task AtenderRevierteElEstadoSiNoSePuedeRegistrarLaConsulta()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db, estado: EstadoCita.Confirmada);
        var historia = new FakeHistoriaClinicaService
        {
            FallarAlRegistrarConsulta = true
        };
        var servicio = CrearServicio(db, historia);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.SetEstadoAsync(cita.Id, EstadoCita.Atendida));

        await AssertEstadoAsync(cita.Id, EstadoCita.Confirmada);
    }

    [Fact]
    public async Task CambiarAlMismoEstadoNoHaceNada()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db);
        var historia = new FakeHistoriaClinicaService();
        var servicio = CrearServicio(db, historia);

        await servicio.SetEstadoAsync(cita.Id, EstadoCita.Programada);

        Assert.Equal(0, historia.ConsultasRegistradas);
        await AssertEstadoAsync(cita.Id, EstadoCita.Programada);
    }

    [Fact]
    public async Task UpdateAsyncNoCambiaElEstado()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db);
        var servicio = CrearServicio(db);

        var modelo = ModeloDesde(cita);
        modelo.HoraInicio = new TimeOnly(10, 0);
        modelo.HoraFin = new TimeOnly(11, 0);

        await servicio.UpdateAsync(modelo);

        await using var verificacion = fixture.CreateContext();
        var actual = await verificacion.Citas.FindAsync(cita.Id);

        Assert.NotNull(actual);
        Assert.Equal(EstadoCita.Programada, actual.Estado);
        Assert.Equal(new TimeOnly(10, 0), actual.HoraInicio);
        Assert.Equal(new TimeOnly(11, 0), actual.HoraFin);
    }

    [Fact]
    public async Task UpdateAsyncRechazaCambiarElPaciente()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db);
        var otroPaciente = await TestData.CrearPacienteAsync(db, "Otro");
        var servicio = CrearServicio(db);

        var modelo = ModeloDesde(cita);
        modelo.PacienteId = otroPaciente.Id;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.UpdateAsync(modelo));

        Assert.Equal("El paciente de una cita no se puede cambiar.", excepcion.Message);
        await using var verificacion = fixture.CreateContext();
        var actual = await verificacion.Citas.FindAsync(cita.Id);
        Assert.Equal(cita.PacienteId, actual!.PacienteId);
    }

    [Fact]
    public async Task UpdateAsyncRechazaCambiarLaFecha()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db);
        var servicio = CrearServicio(db);

        var modelo = ModeloDesde(cita);
        modelo.Fecha = SiguienteDiaHabil(cita.Fecha);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.UpdateAsync(modelo));

        Assert.Equal(
            "La fecha de una cita no se puede cambiar. Crea una cita nueva para reprogramarla.",
            excepcion.Message);
        await AssertEstadoAsync(cita.Id, EstadoCita.Programada);
    }

    [Fact]
    public async Task UpdateAsyncRechazaCambiarElMotivo()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db);
        var servicio = CrearServicio(db);

        var modelo = ModeloDesde(cita);
        modelo.Motivo = "Otro motivo";

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.UpdateAsync(modelo));

        Assert.Equal("El motivo de una cita no se puede cambiar.", excepcion.Message);
        await AssertEstadoAsync(cita.Id, EstadoCita.Programada);
    }

    [Fact]
    public async Task RecepcionistaNoPuedeEditarUnaCitaConfirmada()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db, estado: EstadoCita.Confirmada);
        var usuario = TestSession.Crear("recepcion", RolesSonrisaDigital.Recepcionista);
        var servicio = CrearServicio(db, usuario: usuario);

        var modelo = ModeloDesde(cita);
        modelo.HoraInicio = new TimeOnly(10, 0);
        modelo.HoraFin = new TimeOnly(11, 0);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.UpdateAsync(modelo));

        Assert.Equal(
            "Esta cita ya está confirmada y no puede editarse. El odontólogo es quien la marca como atendida.",
            excepcion.Message);
        await AssertEstadoAsync(cita.Id, EstadoCita.Confirmada);
    }

    [Fact]
    public async Task CancelarUnaCitaProgramadaConPermisoCambiaElEstado()
    {
        await using var db = fixture.CreateContext();
        var cita = await CrearCitaAsync(db);
        var servicio = CrearServicio(db);

        await servicio.SetEstadoAsync(cita.Id, EstadoCita.Cancelada);

        await AssertEstadoAsync(cita.Id, EstadoCita.Cancelada);
    }

    [Fact]
    public async Task PermiteAgendarUnaCitaElSabado()
    {
        await using var db = fixture.CreateContext();
        var paciente = await TestData.CrearPacienteAsync(db);
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var servicio = CrearServicio(db);

        var citaId = await servicio.CreateAsync(new CitaFormDto
        {
            PacienteId = paciente.Id,
            OdontologoId = odontologo.Id,
            Fecha = ProximoSabado(),
            HoraInicio = new TimeOnly(9, 0),
            HoraFin = new TimeOnly(9, 30),
            Motivo = "Consulta de sábado"
        });

        Assert.True(citaId > 0);
    }

    private static CitaService CrearServicio(
        ApplicationDbContext db,
        FakeHistoriaClinicaService? historia = null,
        IAuthorizationService? autorizacion = null,
        ICurrentUserService? usuario = null) =>
        new(
            new CitaRepository(db),
            new PacienteRepository(db),
            new OdontologoRepository(db),
            historia ?? new FakeHistoriaClinicaService(),
            new FakeAuditoriaService(),
            usuario ?? TestSession.Crear(),
            autorizacion ?? new FakeAuthorizationService(),
            db,
            NullLogger<CitaService>.Instance);

    private static async Task<Cita> CrearCitaAsync(
        ApplicationDbContext db,
        EstadoCita estado = EstadoCita.Programada)
    {
        var paciente = await TestData.CrearPacienteAsync(db);
        var odontologo = await TestData.CrearOdontologoAsync(db);

        var cita = new Cita
        {
            PacienteId = paciente.Id,
            OdontologoId = odontologo.Id,
            Fecha = ProximoDiaHabil(),
            HoraInicio = new TimeOnly(9, 0),
            HoraFin = new TimeOnly(9, 30),
            Motivo = "Consulta de prueba",
            Estado = estado
        };

        db.Citas.Add(cita);
        await db.SaveChangesAsync();
        return cita;
    }

    private static CitaFormDto ModeloDesde(Cita cita) => new()
    {
        Id = cita.Id,
        PacienteId = cita.PacienteId,
        OdontologoId = cita.OdontologoId,
        Fecha = cita.Fecha,
        HoraInicio = cita.HoraInicio,
        HoraFin = cita.HoraFin,
        Motivo = cita.Motivo
    };

    private async Task AssertEstadoAsync(int citaId, EstadoCita esperado)
    {
        await using var db = fixture.CreateContext();
        var cita = await db.Citas.FindAsync(citaId);
        Assert.NotNull(cita);
        Assert.Equal(esperado, cita.Estado);
    }

    private static DateOnly ProximoSabado()
    {
        var fecha = RelojNegocio.HoyDateOnly().AddDays(1);
        while (fecha.DayOfWeek != DayOfWeek.Saturday)
        {
            fecha = fecha.AddDays(1);
        }

        return fecha;
    }

    private static DateOnly ProximoDiaHabil()
    {
        var fecha = RelojNegocio.HoyDateOnly().AddDays(1);
        while (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            fecha = fecha.AddDays(1);
        }

        return fecha;
    }

    private static DateOnly SiguienteDiaHabil(DateOnly fecha)
    {
        var siguiente = fecha.AddDays(1);
        while (siguiente.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            siguiente = siguiente.AddDays(1);
        }

        return siguiente;
    }
}
