using System.Runtime.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Archivos;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Tests.Support;
using SonrisaDigital.Tests.Unit;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Archivos del paciente en el odontograma: varias subidas conservadas, validación de tipo,
/// ocultar y restaurar solo para el Dueño, y que nada se borre del disco.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ArchivoPacienteServiceTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>, IDisposable
{
    private readonly string _directorio = Path.Combine(
        Path.GetTempPath(),
        "sonrisa-archivos-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task VariasRadiografiasDelMismoPacienteQuedanTodas()
    {
        if (!ImagenArchivoTests.RequerirWindows()) return;
        await using var db = fixture.CreateContext();
        var usuarioId = (await TestData.AsegurarUsuarioAsync(db, $"archivos-{Guid.NewGuid():N}")).Id;
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(db, TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo), new FakeAuthorizationService());

        await servicio.SubirAsync(paciente.Id, "panoramica.jpg", ImagenArchivoTests.CrearJpg(), DateOnly.FromDateTime(DateTime.Today.AddMonths(-7)), "Radiografía anterior");
        await servicio.SubirAsync(paciente.Id, "periapical.png", ImagenArchivoTests.CrearPng(), DateOnly.FromDateTime(DateTime.Today), "Radiografía de hoy");

        var lista = await servicio.ListarAsync(paciente.Id);

        Assert.Equal(2, lista.Archivos.Count);
        Assert.Equal("Radiografía de hoy", lista.Archivos[0].Descripcion);
        Assert.Equal("Radiografía anterior", lista.Archivos[1].Descripcion);

        // Dos originales y dos miniaturas, cada una en su propio archivo.
        var carpeta = Path.Combine(_directorio, fixture.ClinicaId.ToString(), paciente.Id.ToString());
        Assert.Equal(4, Directory.GetFiles(carpeta).Length);
    }

    [Fact]
    public async Task RechazaUnArchivoQueNoEsJpgNiPng()
    {
        if (!ImagenArchivoTests.RequerirWindows()) return;
        await using var db = fixture.CreateContext();
        var usuarioId = (await TestData.AsegurarUsuarioAsync(db, $"archivos-{Guid.NewGuid():N}")).Id;
        var paciente = await TestData.CrearPacienteAsync(db);
        var servicio = CrearServicio(db, TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo), new FakeAuthorizationService());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.SubirAsync(paciente.Id, "radiografia.jpg", System.Text.Encoding.ASCII.GetBytes("no soy una imagen"), DateOnly.FromDateTime(DateTime.Today), null));

        Assert.Contains("JPG o PNG", error.Message);
    }

    [Fact]
    public async Task OcultarConservaElArchivoYSoloElDuenoLoRestaura()
    {
        if (!ImagenArchivoTests.RequerirWindows()) return;
        await using var db = fixture.CreateContext();
        var usuarioId = (await TestData.AsegurarUsuarioAsync(db, $"archivos-{Guid.NewGuid():N}")).Id;
        var paciente = await TestData.CrearPacienteAsync(db);
        var dueno = CrearServicio(db, TestSession.Crear(usuarioId, RolesSonrisaDigital.Dueno), new FakeAuthorizationService());
        var odontologo = CrearServicio(db, TestSession.Crear(usuarioId, RolesSonrisaDigital.Odontologo), new FakeAuthorizationService(permitir: true, "Archivos.Ocultar"));

        var subido = await dueno.SubirAsync(paciente.Id, "panoramica.jpg", ImagenArchivoTests.CrearJpg(), DateOnly.FromDateTime(DateTime.Today), null);

        var noPuedeOcultar = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => odontologo.OcultarAsync(subido.Id));
        Assert.Contains("Solo el Dueño", noPuedeOcultar.Message);

        await dueno.OcultarAsync(subido.Id);

        Assert.Empty((await dueno.ListarAsync(paciente.Id)).Archivos);
        Assert.Single((await dueno.ListarAsync(paciente.Id, incluirOcultos: true)).Archivos);

        // El odontólogo no ve el archivo oculto, ni siquiera pidiéndolo expresamente.
        Assert.Empty((await odontologo.ListarAsync(paciente.Id, incluirOcultos: true)).Archivos);
        Assert.Null(await odontologo.ObtenerContenidoAsync(subido.Id));

        // Ocultar no borra nada: el archivo sigue en disco.
        var clave = await db.ArchivosPaciente.AsNoTracking().Where(item => item.Id == subido.Id).Select(item => item.ClaveArchivo).SingleAsync();
        Assert.True(File.Exists(Path.Combine(_directorio, clave)));

        await dueno.RestaurarAsync(subido.Id);
        Assert.Single((await dueno.ListarAsync(paciente.Id)).Archivos);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }

    private ArchivoPacienteService CrearServicio(
        ApplicationDbContext db,
        ICurrentUserService sesion,
        IAuthorizationService autorizacion)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Archivos:Directorio"] = _directorio })
            .Build();

        return new ArchivoPacienteService(
            db,
            new TenantFijo(fixture.ClinicaId),
            new ArchivoStorage(configuracion),
            new FakeAuditoriaService(),
            sesion,
            autorizacion);
    }
}
