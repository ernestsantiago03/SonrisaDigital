using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Accesos;
using SonrisaDigital.Services.Accesos.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// La bitácora de accesos debe registrar intentos con y sin usuario, filtrar por
/// resultado, exigir Auditoria.Ver y no romper el login si la escritura falla.
/// </summary>
public sealed class AuditoriaAccesosTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task RegistraIntentosConUsuarioYConCorreoInexistente()
    {
        await using var db = fixture.CreateContext();
        var usuario = await TestData.AsegurarUsuarioAsync(db);
        var sufijo = TestData.NuevoSufijo();
        var correoDesconocido = $"desconocido-{sufijo}@sonrisadigital.local";
        var servicio = CrearServicio(db);

        await servicio.RegistrarAsync(new RegistroAccesoDto(
            usuario.Id,
            usuario.Email,
            ResultadoAcceso.Exitoso,
            fixture.ClinicaId,
            "10.0.0.10",
            "Navegador de prueba",
            "Entró correctamente."));

        await servicio.RegistrarAsync(new RegistroAccesoDto(
            null,
            correoDesconocido,
            ResultadoAcceso.CredencialesInvalidas,
            null,
            "10.0.0.11",
            "Navegador de prueba",
            "El correo no existe."));

        var eventos = await db.EventosAcceso
            .AsNoTracking()
            .Where(evento => evento.Correo == correoDesconocido
                || (evento.UsuarioId == usuario.Id && evento.DireccionIp == "10.0.0.10"))
            .ToListAsync();

        Assert.Equal(2, eventos.Count);
        var desconocido = Assert.Single(eventos, evento => evento.Correo == correoDesconocido);
        Assert.Null(desconocido.UsuarioId);
        Assert.Null(desconocido.ClinicaId);
        Assert.Equal(ResultadoAcceso.CredencialesInvalidas, desconocido.Resultado);

        var exitoso = Assert.Single(eventos, evento => evento.DireccionIp == "10.0.0.10");
        Assert.Equal(usuario.Id, exitoso.UsuarioId);
        Assert.Equal(fixture.ClinicaId, exitoso.ClinicaId);
        Assert.Equal(ResultadoAcceso.Exitoso, exitoso.Resultado);

        // La consulta también debe funcionar cuando el evento no tiene usuario.
        var pagina = await servicio.SearchAsync(new AccesoFiltroDto(Texto: correoDesconocido));
        var desconocidoDto = Assert.Single(pagina.Eventos);
        Assert.Equal("Sin usuario", desconocidoDto.Usuario);
        Assert.Equal(correoDesconocido, desconocidoDto.Correo);
    }

    [Fact]
    public async Task LaBusquedaFiltraPorTextoYResultado()
    {
        await using var db = fixture.CreateContext();
        var usuario = await TestData.AsegurarUsuarioAsync(db);
        var sufijo = TestData.NuevoSufijo();
        var correo = $"filtro-{sufijo}@sonrisadigital.local";
        var servicio = CrearServicio(db);

        await servicio.RegistrarAsync(new RegistroAccesoDto(
            usuario.Id, correo, ResultadoAcceso.Exitoso, fixture.ClinicaId, "10.0.0.20", "Prueba", null));
        await servicio.RegistrarAsync(new RegistroAccesoDto(
            usuario.Id, correo, ResultadoAcceso.ClaveIncorrecta, fixture.ClinicaId, "10.0.0.21", "Prueba", null));
        await servicio.RegistrarAsync(new RegistroAccesoDto(
            usuario.Id, correo, ResultadoAcceso.CuentaBloqueada, fixture.ClinicaId, "10.0.0.22", "Prueba", null));

        var pagina = await servicio.SearchAsync(new AccesoFiltroDto(
            Texto: correo,
            Resultado: ResultadoAcceso.ClaveIncorrecta));

        var evento = Assert.Single(pagina.Eventos);
        Assert.Equal(ResultadoAcceso.ClaveIncorrecta, evento.Resultado);
        Assert.Equal("10.0.0.21", evento.DireccionIp);
        Assert.Equal(1, pagina.Total);
        Assert.Equal(usuario.Nombre, evento.Usuario);
    }

    [Fact]
    public async Task LaConsultaExigeElPermisoDeAuditoria()
    {
        await using var db = fixture.CreateContext();
        var servicio = new AccesoService(
            new EventoAccesoRepository(db),
            TestSession.Crear(),
            new FakeAuthorizationService(permitir: true, "Auditoria.Ver"),
            NullLogger<AccesoService>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servicio.SearchAsync(new AccesoFiltroDto()));
    }

    [Fact]
    public async Task UnFalloDeEscrituraNoInterrumpeElAcceso()
    {
        var repositorio = new RepositorioQueFalla();
        var servicio = new AccesoService(
            repositorio,
            TestSession.Crear(),
            new FakeAuthorizationService(),
            NullLogger<AccesoService>.Instance);

        await servicio.RegistrarAsync(new RegistroAccesoDto(
            null, "sin-base@sonrisadigital.local", ResultadoAcceso.ClaveIncorrecta));

        Assert.Equal(1, repositorio.Intentos);
    }

    [Fact]
    public async Task LaBitacoraDeAccesosNoSePuedeModificarNiBorrar()
    {
        await using var db = fixture.CreateContext();
        var correo = $"inmutable-{TestData.NuevoSufijo()}@sonrisadigital.local";

        db.EventosAcceso.Add(new EventoAcceso
        {
            Correo = correo,
            Resultado = ResultadoAcceso.Exitoso,
            DireccionIp = "10.0.0.30",
            Detalle = "Registro que no debe cambiar"
        });
        await db.SaveChangesAsync();

        var id = await db.EventosAcceso
            .Where(evento => evento.Correo == correo)
            .Select(evento => evento.Id)
            .SingleAsync();

        var modificacion = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE EventosAcceso SET Detalle = N'Alterado' WHERE Id = {id}"));
        Assert.Contains("no se puede modificar ni borrar", modificacion.Message, StringComparison.Ordinal);

        var borrado = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM EventosAcceso WHERE Id = {id}"));
        Assert.Contains("no se puede modificar ni borrar", borrado.Message, StringComparison.Ordinal);

        var registro = await db.EventosAcceso.AsNoTracking().SingleAsync(evento => evento.Id == id);
        Assert.Equal("Registro que no debe cambiar", registro.Detalle);
    }

    private static AccesoService CrearServicio(ApplicationDbContext db) =>
        new(
            new EventoAccesoRepository(db),
            TestSession.Crear(),
            new FakeAuthorizationService(),
            NullLogger<AccesoService>.Instance);

    private sealed class RepositorioQueFalla : IEventoAccesoRepository
    {
        public int Intentos { get; private set; }

        public Task AddAsync(EventoAcceso evento, CancellationToken cancellationToken = default)
        {
            Intentos++;
            throw new InvalidOperationException("La base no está disponible.");
        }

        public Task<(IReadOnlyList<EventoAcceso> Eventos, int Total, int Pagina)> SearchAsync(
            string? texto,
            ResultadoAcceso? resultado,
            DateTime? desdeUtc,
            DateTime? hastaUtcExclusivo,
            int pagina,
            int tamanoPagina,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
