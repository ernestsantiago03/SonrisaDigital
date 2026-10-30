using SonrisaDigital.Data;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Pacientes;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Comprueba contra SQL Server que los buscadores de pacientes no devuelven más del tope
/// y que un paciente ya elegido sigue disponible aunque quede fuera de la búsqueda.
/// </summary>
public sealed class SeleccionPacientesTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const int TotalCoincidencias = 60;

    [Fact]
    public async Task LasBusquedasDeOdontogramaEHistoriaNoDevuelvenMasDelTope()
    {
        await using var db = fixture.CreateContext();
        var (prefijo, _) = await CrearPacientesAsync(db, TotalCoincidencias, EstadoRegistro.Activo);

        var odontograma = await new OdontogramaRepository(db).GetActivePatientsAsync(prefijo);
        var historia = await new HistoriaClinicaRepository(db).GetPatientsAsync(prefijo);

        Assert.Equal(LimitesBusqueda.PacientesResultados, odontograma.Count);
        Assert.Equal(LimitesBusqueda.PacientesResultados, historia.Count);
    }

    [Fact]
    public async Task UnPacienteActivoSeEncuentraPorIdAunqueQuedeFueraDelTope()
    {
        await using var db = fixture.CreateContext();
        var (prefijo, ids) = await CrearPacientesAsync(db, TotalCoincidencias, EstadoRegistro.Activo);
        var ultimoId = ids[^1];

        var repositorio = new OdontogramaRepository(db);
        var enBusqueda = await repositorio.GetActivePatientsAsync(prefijo);
        var porId = await repositorio.GetActivePatientByIdAsync(ultimoId);

        Assert.DoesNotContain(enBusqueda, paciente => paciente.Id == ultimoId);
        Assert.NotNull(porId);
        Assert.Equal(ultimoId, porId.Id);
    }

    [Fact]
    public async Task UnPacienteInactivoNoSeDevuelvePorId()
    {
        await using var db = fixture.CreateContext();
        var (_, ids) = await CrearPacientesAsync(db, 1, EstadoRegistro.Inactivo);

        var porId = await new OdontogramaRepository(db).GetActivePatientByIdAsync(ids[0]);

        Assert.Null(porId);
    }

    [Fact]
    public async Task ElSelectorDevuelveElTopeYElTotalReal()
    {
        await using var db = fixture.CreateContext();
        var (prefijo, _) = await CrearPacientesAsync(db, TotalCoincidencias, EstadoRegistro.Activo);

        var servicio = CrearServicioPacientes(db);
        var resultado = await servicio.BuscarActivosParaSeleccionAsync(prefijo);

        Assert.Equal(LimitesBusqueda.PacientesResultados, resultado.Items.Count);
        Assert.Equal(TotalCoincidencias, resultado.Total);
    }

    [Fact]
    public async Task ElSelectorIncluyeElPacienteYaElegidoAunqueEsteInactivoOFueraDelTope()
    {
        await using var db = fixture.CreateContext();
        var (prefijo, _) = await CrearPacientesAsync(db, TotalCoincidencias, EstadoRegistro.Activo);
        var (_, inactivo) = await CrearPacientesAsync(db, 1, EstadoRegistro.Inactivo);
        var idInactivo = inactivo[0];

        var servicio = CrearServicioPacientes(db);
        var resultado = await servicio.BuscarActivosParaSeleccionAsync(prefijo, idInactivo);

        Assert.Equal(LimitesBusqueda.PacientesResultados + 1, resultado.Items.Count);
        Assert.Equal(idInactivo, resultado.Items[0].Id);
        Assert.Equal(TotalCoincidencias, resultado.Total);
    }

    private static PacienteService CrearServicioPacientes(SonrisaDigital.Data.ApplicationDbContext db) =>
        new(
            new PacienteRepository(db),
            new FakeAuditoriaService(),
            TestSession.Crear(),
            new FakeAuthorizationService());

    private static async Task<(string Prefijo, List<int> Ids)> CrearPacientesAsync(
        SonrisaDigital.Data.ApplicationDbContext db,
        int cantidad,
        EstadoRegistro estado)
    {
        var prefijo = $"Seleccion-{Guid.NewGuid():N}";
        var pacientes = Enumerable.Range(0, cantidad)
            .Select(indice => new Paciente
            {
                Nombres = $"{prefijo} {indice:D2}",
                Apellidos = "Prueba",
                Estado = estado
            })
            .ToList();

        db.Pacientes.AddRange(pacientes);
        await db.SaveChangesAsync();

        return (prefijo, pacientes.Select(paciente => paciente.Id).ToList());
    }
}
