using SonrisaDigital.Data;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Pacientes;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Comprueba contra SQL Server la paginación de los buscadores de pacientes y que un paciente
/// ya elegido sigue disponible aunque quede fuera de la búsqueda.
/// </summary>
public sealed class SeleccionPacientesTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const int TotalCoincidencias = 25;
    private const int TamanoPagina = 10;

    [Fact]
    public async Task LasBusquedasDeOdontogramaEHistoriaSePaginan()
    {
        await using var db = fixture.CreateContext();
        var (prefijo, _) = await CrearPacientesAsync(db, TotalCoincidencias, EstadoRegistro.Activo);

        var odontograma = await new OdontogramaRepository(db).SearchActivePatientsPagedAsync(prefijo, 1, TamanoPagina);
        var historia = await new HistoriaClinicaRepository(db).SearchPatientsPagedAsync(prefijo, 1, TamanoPagina);

        Assert.Equal(TamanoPagina, odontograma.Items.Count);
        Assert.Equal(TotalCoincidencias, odontograma.Total);
        Assert.Equal(3, odontograma.TotalPages);
        Assert.Equal(TamanoPagina, historia.Items.Count);
        Assert.Equal(TotalCoincidencias, historia.Total);
    }

    [Fact]
    public async Task LaPaginaSiguienteTraeOtrosPacientesYLaFueraDeRangoSeAjusta()
    {
        await using var db = fixture.CreateContext();
        var (prefijo, _) = await CrearPacientesAsync(db, TotalCoincidencias, EstadoRegistro.Activo);
        var repositorio = new OdontogramaRepository(db);

        var pagina1 = await repositorio.SearchActivePatientsPagedAsync(prefijo, 1, TamanoPagina);
        var pagina2 = await repositorio.SearchActivePatientsPagedAsync(prefijo, 2, TamanoPagina);
        var fueraDeRango = await repositorio.SearchActivePatientsPagedAsync(prefijo, 99, TamanoPagina);

        Assert.Equal(2, pagina2.Page);
        Assert.Empty(pagina1.Items.Select(paciente => paciente.Id).Intersect(pagina2.Items.Select(paciente => paciente.Id)));
        Assert.Equal(3, fueraDeRango.Page);
        Assert.Equal(5, fueraDeRango.Items.Count);
    }

    [Fact]
    public async Task UnPacienteActivoSeEncuentraPorIdAunqueQuedeEnOtraPagina()
    {
        await using var db = fixture.CreateContext();
        var (prefijo, ids) = await CrearPacientesAsync(db, TotalCoincidencias, EstadoRegistro.Activo);
        var ultimoId = ids[^1];

        var repositorio = new OdontogramaRepository(db);
        var primeraPagina = await repositorio.SearchActivePatientsPagedAsync(prefijo, 1, TamanoPagina);
        var porId = await repositorio.GetActivePatientByIdAsync(ultimoId);

        Assert.DoesNotContain(primeraPagina.Items, paciente => paciente.Id == ultimoId);
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
        var (prefijo, _) = await CrearPacientesAsync(db, 60, EstadoRegistro.Activo);

        var servicio = CrearServicioPacientes(db);
        var resultado = await servicio.BuscarActivosParaSeleccionAsync(prefijo);

        Assert.Equal(LimitesBusqueda.PacientesResultados, resultado.Items.Count);
        Assert.Equal(60, resultado.Total);
    }

    [Fact]
    public async Task ElSelectorIncluyeElPacienteYaElegidoAunqueEsteInactivo()
    {
        await using var db = fixture.CreateContext();
        var (prefijo, _) = await CrearPacientesAsync(db, 60, EstadoRegistro.Activo);
        var (_, inactivo) = await CrearPacientesAsync(db, 1, EstadoRegistro.Inactivo);
        var idInactivo = inactivo[0];

        var servicio = CrearServicioPacientes(db);
        var resultado = await servicio.BuscarActivosParaSeleccionAsync(prefijo, idInactivo);

        Assert.Equal(LimitesBusqueda.PacientesResultados + 1, resultado.Items.Count);
        Assert.Equal(idInactivo, resultado.Items[0].Id);
        Assert.Equal(60, resultado.Total);
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
