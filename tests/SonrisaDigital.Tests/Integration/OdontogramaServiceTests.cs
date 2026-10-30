using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Catalogos;
using SonrisaDigital.Services.Odontograma;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Pruebas de integración de la firma del odontograma. Se comprueba que el servicio
/// resuelva el odontólogo responsable sin depender de la pantalla: un administrador
/// sin perfil puede elegir un firmante activo, un odontólogo vinculado no puede
/// falsificarlo y un usuario sin perfil ni rol administrador queda bloqueado.
/// </summary>
public sealed class OdontogramaServiceTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task AdministradorSinPerfilPuedeGuardarConOdontologoSeleccionado()
    {
        await using var db = fixture.CreateContext();
        await OdontogramaCatalogSeeder.SeedAsync(db);
        var pieza = await db.PiezasDentales.OrderBy(pieza => pieza.Id).FirstAsync();
        var usuarioId = await CrearUsuarioAsync(db, "admin");
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Administrador));

        // La pantalla recibe el contexto: puede editar y debe elegir un odontólogo activo.
        var contexto = await servicio.GetEditContextAsync();
        Assert.True(contexto.PuedeEditar);
        Assert.True(contexto.RequiereSeleccionarOdontologo);
        Assert.Null(contexto.OdontologoVinculadoId);
        Assert.Contains(
            contexto.OdontologosDisponibles,
            opcion => opcion.Id == odontologo.Id);

        await servicio.SaveAsync(
            paciente.Id,
            pieza.Id,
            EstadoOdontograma.Caries,
            "Caries en la cara oclusal",
            odontologo.Id);

        var registro = await db.RegistrosOdontograma
            .SingleAsync(registro => registro.PacienteId == paciente.Id && registro.PiezaDentalId == pieza.Id);
        Assert.Equal(odontologo.Id, registro.OdontologoId);
        Assert.Equal(EstadoOdontograma.Caries, registro.Estado);
        Assert.Equal("Caries en la cara oclusal", registro.Observaciones);
    }

    [Fact]
    public async Task OdontologoVinculadoGuardaConSuPropioPerfil()
    {
        await using var db = fixture.CreateContext();
        await OdontogramaCatalogSeeder.SeedAsync(db);
        var pieza = await db.PiezasDentales.OrderBy(pieza => pieza.Id).FirstAsync();
        var (usuarioId, odontologo) = await CrearOdontologoVinculadoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo));

        var contexto = await servicio.GetEditContextAsync();
        Assert.True(contexto.PuedeEditar);
        Assert.False(contexto.RequiereSeleccionarOdontologo);
        Assert.Equal(odontologo.Id, contexto.OdontologoVinculadoId);

        // Aunque la pantalla no envíe un firmante, el servicio usa el perfil vinculado.
        await servicio.SaveAsync(
            paciente.Id,
            pieza.Id,
            EstadoOdontograma.Restauracion,
            null);

        var registro = await db.RegistrosOdontograma
            .SingleAsync(registro => registro.PacienteId == paciente.Id && registro.PiezaDentalId == pieza.Id);
        Assert.Equal(odontologo.Id, registro.OdontologoId);
    }

    [Fact]
    public async Task OdontologoNoPuedeFalsificarElFirmante()
    {
        await using var db = fixture.CreateContext();
        await OdontogramaCatalogSeeder.SeedAsync(db);
        var pieza = await db.PiezasDentales.OrderBy(pieza => pieza.Id).FirstAsync();
        var (usuarioId, _) = await CrearOdontologoVinculadoAsync(db);
        var otroOdontologo = await TestData.CrearOdontologoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SaveAsync(
                paciente.Id,
                pieza.Id,
                EstadoOdontograma.Caries,
                null,
                otroOdontologo.Id));

        Assert.Contains("odontólogo vinculado", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await db.RegistrosOdontograma
            .AnyAsync(registro => registro.PacienteId == paciente.Id && registro.PiezaDentalId == pieza.Id));
    }

    [Fact]
    public async Task UsuarioSinPerfilNiAdministradorNoPuedeEditar()
    {
        await using var db = fixture.CreateContext();
        await OdontogramaCatalogSeeder.SeedAsync(db);
        var pieza = await db.PiezasDentales.OrderBy(pieza => pieza.Id).FirstAsync();
        var usuarioId = await CrearUsuarioAsync(db, "sinperfil");
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(db, TestSession.Crear(usuarioId));

        var contexto = await servicio.GetEditContextAsync();
        Assert.False(contexto.PuedeEditar);
        Assert.False(contexto.RequiereSeleccionarOdontologo);
        Assert.Contains("perfil de odontólogo", contexto.MotivoSinEdicion, StringComparison.OrdinalIgnoreCase);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SaveAsync(
                paciente.Id,
                pieza.Id,
                EstadoOdontograma.Caries,
                null,
                odontologo.Id));

        Assert.Contains("perfil de odontólogo", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await db.RegistrosOdontograma
            .AnyAsync(registro => registro.PacienteId == paciente.Id && registro.PiezaDentalId == pieza.Id));
    }

    [Fact]
    public async Task AdministradorSinPerfilDebeSeleccionarUnOdontologo()
    {
        await using var db = fixture.CreateContext();
        await OdontogramaCatalogSeeder.SeedAsync(db);
        var pieza = await db.PiezasDentales.OrderBy(pieza => pieza.Id).FirstAsync();
        var usuarioId = await CrearUsuarioAsync(db, "admin");
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Administrador));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SaveAsync(
                paciente.Id,
                pieza.Id,
                EstadoOdontograma.Caries,
                null));

        Assert.Contains("Selecciona un odontólogo", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdministradorNoPuedeFirmarConUnOdontologoInactivo()
    {
        await using var db = fixture.CreateContext();
        await OdontogramaCatalogSeeder.SeedAsync(db);
        var pieza = await db.PiezasDentales.OrderBy(pieza => pieza.Id).FirstAsync();
        var usuarioId = await CrearUsuarioAsync(db, "admin");
        var odontologoInactivo = await TestData.CrearOdontologoAsync(
            db,
            estado: EstadoRegistro.Inactivo);
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(
            db,
            TestSession.Crear(usuarioId, RolesSonrisaDigital.Administrador));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SaveAsync(
                paciente.Id,
                pieza.Id,
                EstadoOdontograma.Caries,
                null,
                odontologoInactivo.Id));

        Assert.Contains("inactivo", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static OdontogramaService CrearServicio(
        ApplicationDbContext db,
        ICurrentUserService usuario) =>
        new(
            new OdontogramaRepository(db),
            new PacienteRepository(db),
            usuario,
            new FakeAuthorizationService(),
            new CatalogoCache(new MemoryCache(new MemoryCacheOptions())));

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
}
