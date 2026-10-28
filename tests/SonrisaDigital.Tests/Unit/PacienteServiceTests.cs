using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Pacientes;
using SonrisaDigital.Services.Pacientes.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Pruebas de la separación entre datos administrativos y clínicos del paciente.
/// El objetivo es que las alergias y el estado no puedan modificarse con los
/// permisos del formulario administrativo.
/// </summary>
public sealed class PacienteServiceTests
{
    [Fact]
    public async Task RecepcionistaNoPuedeLeerDatosClinicos()
    {
        var repositorio = new StubPacienteRepository();
        repositorio.Pacientes.Add(PacienteConAlergias("Penicilina"));

        var servicio = CrearServicio(
            repositorio,
            new FakeAuthorizationService(true, "HistoriaClinica.Ver"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.GetClinicoAsync(1));
    }

    [Fact]
    public async Task UsuarioConPermisoClinicoPuedeLeerAlergias()
    {
        var repositorio = new StubPacienteRepository();
        repositorio.Pacientes.Add(PacienteConAlergias("Penicilina"));

        var servicio = CrearServicio(repositorio);

        var clinico = await servicio.GetClinicoAsync(1);

        Assert.NotNull(clinico);
        Assert.Equal("Penicilina", clinico.Alergias);
        Assert.False(clinico.NoRefiereAlergias);
    }

    [Fact]
    public async Task GetClinicoTraduceNoRefiereAlergias()
    {
        var repositorio = new StubPacienteRepository();
        repositorio.Pacientes.Add(PacienteConAlergias("No refiere alergias."));

        var servicio = CrearServicio(repositorio);

        var clinico = await servicio.GetClinicoAsync(1);

        Assert.NotNull(clinico);
        Assert.Null(clinico.Alergias);
        Assert.True(clinico.NoRefiereAlergias);
    }

    [Fact]
    public async Task NoPuedeCrearPacienteConAlergiasSinPermisoClinico()
    {
        var repositorio = new StubPacienteRepository();
        var servicio = CrearServicio(
            repositorio,
            new FakeAuthorizationService(true, "HistoriaClinica.Crear"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.CreateAsync(ModeloValido(), Clinico("Látex")));

        Assert.Empty(repositorio.Pacientes);
    }

    [Fact]
    public async Task PuedeCrearPacienteConAlergiasConPermisoClinico()
    {
        var repositorio = new StubPacienteRepository();
        var servicio = CrearServicio(repositorio);

        var id = await servicio.CreateAsync(ModeloValido(), Clinico("  Látex  "));

        var paciente = Assert.Single(repositorio.Pacientes);
        Assert.Equal(id, paciente.Id);
        Assert.NotNull(paciente.HistoriaClinica);
        Assert.Equal("Látex", paciente.HistoriaClinica.Alergias);
    }

    [Fact]
    public async Task NoCreaHistoriaCuandoElBloqueClinicoEstaVacio()
    {
        var repositorio = new StubPacienteRepository();
        var servicio = CrearServicio(
            repositorio,
            new FakeAuthorizationService(true, "HistoriaClinica.Crear"));

        await servicio.CreateAsync(ModeloValido(), new PacienteClinicoDto());

        var paciente = Assert.Single(repositorio.Pacientes);
        Assert.Null(paciente.HistoriaClinica);
    }

    [Fact]
    public async Task NoPuedeModificarAlergiasSinPermisoClinico()
    {
        var repositorio = new StubPacienteRepository();
        var paciente = PacienteConAlergias("Penicilina");
        repositorio.Pacientes.Add(paciente);

        var servicio = CrearServicio(
            repositorio,
            new FakeAuthorizationService(true, "HistoriaClinica.Editar"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.UpdateAsync(ModeloValido(paciente.Id), Clinico("Látex")));

        Assert.Equal("Penicilina", paciente.HistoriaClinica!.Alergias);
    }

    [Fact]
    public async Task PuedeModificarAlergiasConPermisoClinico()
    {
        var repositorio = new StubPacienteRepository();
        var paciente = PacienteConAlergias("Penicilina");
        repositorio.Pacientes.Add(paciente);

        var servicio = CrearServicio(repositorio);

        await servicio.UpdateAsync(ModeloValido(paciente.Id), Clinico("Látex"));

        Assert.Equal("Látex", paciente.HistoriaClinica!.Alergias);
    }

    [Fact]
    public async Task UpdateSinBloqueClinicoNoCambiaAlergiasNiEstado()
    {
        var repositorio = new StubPacienteRepository();
        var paciente = PacienteConAlergias("Penicilina");
        paciente.Estado = EstadoRegistro.Inactivo;
        repositorio.Pacientes.Add(paciente);

        var servicio = CrearServicio(
            repositorio,
            new FakeAuthorizationService(true, "HistoriaClinica.Editar", "Pacientes.Desactivar"));

        await servicio.UpdateAsync(ModeloValido(paciente.Id));

        Assert.Equal("Penicilina", paciente.HistoriaClinica!.Alergias);
        Assert.Equal(EstadoRegistro.Inactivo, paciente.Estado);
    }

    [Fact]
    public async Task UpdateConClinicoSinCambiosNoExigePermisoClinico()
    {
        var repositorio = new StubPacienteRepository();
        var paciente = PacienteConAlergias("Penicilina");
        repositorio.Pacientes.Add(paciente);

        var servicio = CrearServicio(
            repositorio,
            new FakeAuthorizationService(true, "HistoriaClinica.Editar"));

        await servicio.UpdateAsync(ModeloValido(paciente.Id), Clinico("Penicilina"));

        Assert.Equal("Penicilina", paciente.HistoriaClinica!.Alergias);
    }

    [Fact]
    public async Task RechazaAlergiasDemasiadoLargas()
    {
        var repositorio = new StubPacienteRepository();
        var servicio = CrearServicio(repositorio);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CreateAsync(ModeloValido(), Clinico(new string('a', 1001))));

        Assert.Equal("El detalle de alergias no puede superar los 1000 caracteres.", excepcion.Message);
        Assert.Empty(repositorio.Pacientes);
    }

    private static PacienteService CrearServicio(
        StubPacienteRepository repositorio,
        IAuthorizationService? autorizacion = null) =>
        new(
            repositorio,
            new FakeAuditoriaService(),
            TestSession.Crear(),
            autorizacion ?? new FakeAuthorizationService());

    private static PacienteFormDto ModeloValido(int id = 0) => new()
    {
        Id = id,
        Nombres = "Ana",
        Apellidos = "Pérez",
        Identificacion = "001-0000000-1",
        Telefono = "809-555-0000"
    };

    private static PacienteClinicoDto Clinico(string? alergias) => new()
    {
        Alergias = alergias
    };

    private static Paciente PacienteConAlergias(string alergias) => new()
    {
        Id = 1,
        Nombres = "Ana",
        Apellidos = "Pérez",
        Estado = EstadoRegistro.Activo,
        HistoriaClinica = new HistoriaClinica { PacienteId = 1, Alergias = alergias }
    };
}
