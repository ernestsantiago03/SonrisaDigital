using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.HistoriasClinicas;
using SonrisaDigital.Services.HistoriasClinicas.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Pruebas de integración de la firma y el cierre de consultas. Se comprueba que
/// el servicio no dependa de la pantalla para decidir quién firma, quién puede
/// modificar una consulta y cuándo está completa para cerrarse.
/// </summary>
public sealed class HistoriaClinicaServiceTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task OdontologoNoPuedeFirmarComoOtro()
    {
        await using var db = fixture.CreateContext();
        var (usuarioId, _) = await CrearOdontologoVinculadoAsync(db);
        var otroOdontologo = await TestData.CrearOdontologoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CreateConsultaAsync(Modelo(paciente.Id, otroOdontologo.Id)));

        Assert.Contains("odontólogo vinculado", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.ConsultasClinicas
            .Where(consulta => consulta.PacienteId == paciente.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task AdministradorSinPerfilPuedeElegirAlFirmante()
    {
        await using var db = fixture.CreateContext();
        var usuarioId = await CrearUsuarioAsync(db, "admin");
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Administrador));

        await servicio.CreateConsultaAsync(Modelo(paciente.Id, odontologo.Id));

        var consulta = await db.ConsultasClinicas
            .SingleAsync(consulta => consulta.PacienteId == paciente.Id);
        Assert.Equal(odontologo.Id, consulta.OdontologoId);
        Assert.Equal(EstadoConsulta.Abierta, consulta.Estado);
    }

    [Fact]
    public async Task OdontologoVinculadoPuedeFirmarSuPropiaConsulta()
    {
        await using var db = fixture.CreateContext();
        var (usuarioId, odontologo) = await CrearOdontologoVinculadoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo));

        await servicio.CreateConsultaAsync(Modelo(paciente.Id, odontologo.Id));

        var consulta = await db.ConsultasClinicas
            .SingleAsync(consulta => consulta.PacienteId == paciente.Id);
        Assert.Equal(odontologo.Id, consulta.OdontologoId);
        Assert.Equal(EstadoConsulta.Abierta, consulta.Estado);
    }

    [Fact]
    public async Task UsuarioSinPerfilOdontologicoNoPuedeFirmar()
    {
        await using var db = fixture.CreateContext();
        var usuarioId = await CrearUsuarioAsync(db, "sinperfil");
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(db, TestSession.Crear(usuarioId));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CreateConsultaAsync(Modelo(paciente.Id, odontologo.Id)));

        Assert.Contains("perfil de odontólogo", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.ConsultasClinicas
            .Where(consulta => consulta.PacienteId == paciente.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task OdontologoNoPuedeEditarUnaConsultaDeOtro()
    {
        await using var db = fixture.CreateContext();
        var (usuarioId, odontologo) = await CrearOdontologoVinculadoAsync(db);
        var otroOdontologo = await TestData.CrearOdontologoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var consulta = await CrearConsultaAsync(db, paciente.Id, otroOdontologo.Id);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo));

        var modelo = Modelo(paciente.Id, odontologo.Id, consulta.Id);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.UpdateConsultaAsync(modelo));

        Assert.Contains("firmada por otro odontólogo", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OdontologoNoPuedeCerrarUnaConsultaDeOtro()
    {
        await using var db = fixture.CreateContext();
        var (usuarioId, _) = await CrearOdontologoVinculadoAsync(db);
        var otroOdontologo = await TestData.CrearOdontologoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var consulta = await CrearConsultaAsync(db, paciente.Id, otroOdontologo.Id);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CerrarConsultaAsync(consulta.Id));

        Assert.Contains("firmada por otro odontólogo", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        await AssertEstadoAsync(consulta.Id, EstadoConsulta.Abierta);
    }

    [Fact]
    public async Task NoSePuedeCerrarSinDiagnostico()
    {
        await using var db = fixture.CreateContext();
        var (usuarioId, odontologo) = await CrearOdontologoVinculadoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var consulta = await CrearConsultaAsync(
            db,
            paciente.Id,
            odontologo.Id,
            diagnostico: null,
            plan: "Obturación");
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CerrarConsultaAsync(consulta.Id));

        Assert.Contains("diagnóstico", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        await AssertEstadoAsync(consulta.Id, EstadoConsulta.Abierta);
    }

    [Fact]
    public async Task NoSePuedeCerrarSinPlanDeTratamiento()
    {
        await using var db = fixture.CreateContext();
        var (usuarioId, odontologo) = await CrearOdontologoVinculadoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var consulta = await CrearConsultaAsync(
            db,
            paciente.Id,
            odontologo.Id,
            diagnostico: "Caries",
            plan: null);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CerrarConsultaAsync(consulta.Id));

        Assert.Contains("al menos un tratamiento al plan", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        await AssertEstadoAsync(consulta.Id, EstadoConsulta.Abierta);
    }

    [Fact]
    public async Task CerrarConsultaCompletaLaDejaEnSoloLectura()
    {
        await using var db = fixture.CreateContext();
        var (usuarioId, odontologo) = await CrearOdontologoVinculadoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var consulta = await CrearConsultaAsync(db, paciente.Id, odontologo.Id);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo));

        await servicio.CerrarConsultaAsync(consulta.Id);

        await AssertEstadoAsync(consulta.Id, EstadoConsulta.Cerrada);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.UpdateConsultaAsync(Modelo(paciente.Id, odontologo.Id, consulta.Id)));
        Assert.Contains("consulta cerrada", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CerrarConsultaExigeEditarYNoCrear()
    {
        await using var db = fixture.CreateContext();
        var (usuarioId, odontologo) = await CrearOdontologoVinculadoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var consulta = await CrearConsultaAsync(db, paciente.Id, odontologo.Id);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo),
            new FakeAuthorizationService(true, "HistoriaClinica.Crear"));

        await servicio.CerrarConsultaAsync(consulta.Id);

        await AssertEstadoAsync(consulta.Id, EstadoConsulta.Cerrada);
    }

    private static HistoriaClinicaService CrearServicio(
        ApplicationDbContext db,
        ICurrentUserService usuario,
        IAuthorizationService? autorizacion = null) =>
        new(
            new HistoriaClinicaRepository(db),
            new FakeAuditoriaService(),
            usuario,
            autorizacion ?? new FakeAuthorizationService());

    private static async Task<string> CrearUsuarioAsync(ApplicationDbContext db, string prefijo)
    {
        var usuarioId = $"{prefijo}-{Guid.NewGuid():N}";
        await TestData.AsegurarUsuarioAsync(db, usuarioId);
        return usuarioId;
    }

    private static async Task<(string UsuarioId, Odontologo Odontologo)> CrearOdontologoVinculadoAsync(
        ApplicationDbContext db)
    {
        var usuarioId = await CrearUsuarioAsync(db, "odontologo");
        var odontologo = await TestData.CrearOdontologoAsync(db);
        odontologo.UsuarioId = usuarioId;
        await db.SaveChangesAsync();
        return (usuarioId, odontologo);
    }

    private static async Task<ConsultaClinica> CrearConsultaAsync(
        ApplicationDbContext db,
        int pacienteId,
        int odontologoId,
        string? diagnostico = "Caries",
        string? plan = "Obturación")
    {
        var historia = await db.HistoriasClinicas
            .FirstOrDefaultAsync(historia => historia.PacienteId == pacienteId);

        if (historia is null)
        {
            historia = new HistoriaClinica { PacienteId = pacienteId };
            db.HistoriasClinicas.Add(historia);
            await db.SaveChangesAsync();
        }

        var consulta = new ConsultaClinica
        {
            PacienteId = pacienteId,
            HistoriaClinicaId = historia.Id,
            OdontologoId = odontologoId,
            Fecha = DateTime.Today,
            Motivo = "Dolor",
            Diagnostico = diagnostico,
            PlanTratamiento = plan,
            Estado = EstadoConsulta.Abierta
        };

        db.ConsultasClinicas.Add(consulta);
        await db.SaveChangesAsync();
        return consulta;
    }

    private static ConsultaClinicaFormDto Modelo(
        int pacienteId,
        int odontologoId,
        int consultaId = 0) => new()
    {
        Id = consultaId,
        PacienteId = pacienteId,
        OdontologoId = odontologoId,
        Fecha = DateTime.Today,
        Motivo = "Dolor",
        Diagnostico = "Caries",
        PlanTratamiento = "Obturación"
    };

    private async Task AssertEstadoAsync(int consultaId, EstadoConsulta esperado)
    {
        await using var db = fixture.CreateContext();
        var consulta = await db.ConsultasClinicas.FindAsync(consultaId);
        Assert.NotNull(consulta);
        Assert.Equal(esperado, consulta.Estado);
    }
}
