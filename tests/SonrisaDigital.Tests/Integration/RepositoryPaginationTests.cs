using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Comprueba contra SQL Server que el conteo, el recorte y los filtros se
/// ejecutan en la base y no en memoria.
/// </summary>
public sealed class PacienteRepositoryPaginationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task PaginaFiltraYCorrigePaginaFueraDeRango()
    {
        await using var db = fixture.CreateContext();
        var prefijo = $"Pagina-{Guid.NewGuid():N}";

        for (var indice = 0; indice < 25; indice++)
        {
            db.Pacientes.Add(new Paciente
            {
                Nombres = $"{prefijo} {indice:D2}",
                Apellidos = "Activo",
                Estado = EstadoRegistro.Activo
            });
        }

        for (var indice = 0; indice < 5; indice++)
        {
            db.Pacientes.Add(new Paciente
            {
                Nombres = $"{prefijo} Inactivo {indice:D2}",
                Apellidos = "Prueba",
                Estado = EstadoRegistro.Inactivo
            });
        }

        await db.SaveChangesAsync();

        var repositorio = new PacienteRepository(db);

        var pagina1 = await repositorio.SearchPagedAsync(
            prefijo,
            EstadoRegistro.Activo,
            page: 1,
            pageSize: 10);

        var pagina2 = await repositorio.SearchPagedAsync(
            prefijo,
            EstadoRegistro.Activo,
            page: 2,
            pageSize: 10);

        var paginaFueraDeRango = await repositorio.SearchPagedAsync(
            prefijo,
            EstadoRegistro.Activo,
            page: 99,
            pageSize: 10);

        var todos = await repositorio.SearchPagedAsync(
            prefijo,
            estado: null,
            page: 1,
            pageSize: 10);

        var busqueda = await repositorio.SearchPagedAsync(
            $"{prefijo} 03",
            estado: null,
            page: 1,
            pageSize: 10);

        Assert.Equal(25, pagina1.Total);
        Assert.Equal(10, pagina1.Items.Count);
        Assert.Equal(1, pagina1.Page);
        Assert.Equal(10, pagina2.Items.Count);
        Assert.Equal(25, paginaFueraDeRango.Total);
        Assert.Equal(3, paginaFueraDeRango.Page);
        Assert.Equal(5, paginaFueraDeRango.Items.Count);
        Assert.Equal(30, todos.Total);
        Assert.Single(busqueda.Items);
    }
}

/// <summary>
/// Pruebas del repositorio de citas: paginación por filtros y detección de
/// choques de horario por odontólogo o por paciente.
/// </summary>
public sealed class CitaRepositoryTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task PaginaYFiltraCitasPorFechaEstadoYPaciente()
    {
        await using var db = fixture.CreateContext();
        var paciente = await TestData.CrearPacienteAsync(db);
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var fecha = ProximoDiaHabil();

        for (var indice = 0; indice < 12; indice++)
        {
            var inicio = new TimeOnly(8, 0).AddMinutes(indice * 15);
            db.Citas.Add(new Cita
            {
                PacienteId = paciente.Id,
                OdontologoId = odontologo.Id,
                Fecha = fecha,
                HoraInicio = inicio,
                HoraFin = inicio.AddMinutes(15),
                Motivo = $"Cita de prueba {indice}",
                Estado = indice % 2 == 0 ? EstadoCita.Programada : EstadoCita.Confirmada
            });
        }

        await db.SaveChangesAsync();

        var repositorio = new CitaRepository(db);

        var programadas = await repositorio.SearchPagedAsync(
            fecha,
            paciente.Id,
            odontologo.Id,
            EstadoCita.Programada,
            page: 1,
            pageSize: 5);

        var pagina2 = await repositorio.SearchPagedAsync(
            fecha,
            paciente.Id,
            odontologo.Id,
            EstadoCita.Programada,
            page: 2,
            pageSize: 5);

        var fueraDeRango = await repositorio.SearchPagedAsync(
            fecha,
            paciente.Id,
            odontologo.Id,
            EstadoCita.Programada,
            page: 99,
            pageSize: 5);

        var todas = await repositorio.SearchPagedAsync(
            fecha,
            paciente.Id,
            odontologo.Id,
            estado: null,
            page: 1,
            pageSize: 10);

        Assert.Equal(6, programadas.Total);
        Assert.Equal(5, programadas.Items.Count);
        Assert.Single(pagina2.Items);
        Assert.Equal(2, fueraDeRango.Page);
        Assert.Single(fueraDeRango.Items);
        Assert.Equal(12, todas.Total);
        Assert.Equal(10, todas.Items.Count);
    }

    [Fact]
    public async Task DetectaChoquePorOdontologoOPacienteYRespetaCanceladas()
    {
        await using var db = fixture.CreateContext();
        var pacienteA = await TestData.CrearPacienteAsync(db);
        var pacienteB = await TestData.CrearPacienteAsync(db);
        var odontologoA = await TestData.CrearOdontologoAsync(db);
        var odontologoB = await TestData.CrearOdontologoAsync(db);
        var fecha = ProximoDiaHabil();

        var existente = new Cita
        {
            PacienteId = pacienteA.Id,
            OdontologoId = odontologoA.Id,
            Fecha = fecha,
            HoraInicio = new TimeOnly(9, 0),
            HoraFin = new TimeOnly(10, 0),
            Estado = EstadoCita.Programada
        };

        var cancelada = new Cita
        {
            PacienteId = pacienteB.Id,
            OdontologoId = odontologoB.Id,
            Fecha = fecha,
            HoraInicio = new TimeOnly(11, 0),
            HoraFin = new TimeOnly(12, 0),
            Estado = EstadoCita.Cancelada
        };

        db.Citas.AddRange(existente, cancelada);
        await db.SaveChangesAsync();

        var repositorio = new CitaRepository(db);

        var mismoOdontologo = await repositorio.BuscarConflictoAsync(
            odontologoA.Id,
            pacienteB.Id,
            fecha,
            new TimeOnly(9, 30),
            new TimeOnly(10, 30));

        var mismoPaciente = await repositorio.BuscarConflictoAsync(
            odontologoB.Id,
            pacienteA.Id,
            fecha,
            new TimeOnly(9, 30),
            new TimeOnly(10, 30));

        var libre = await repositorio.BuscarConflictoAsync(
            odontologoB.Id,
            pacienteB.Id,
            fecha,
            new TimeOnly(9, 30),
            new TimeOnly(10, 30));

        var excluyendoLaPropia = await repositorio.BuscarConflictoAsync(
            odontologoA.Id,
            pacienteA.Id,
            fecha,
            new TimeOnly(9, 30),
            new TimeOnly(10, 30),
            excluirCitaId: existente.Id);

        var canceladaNoBloquea = await repositorio.BuscarConflictoAsync(
            odontologoB.Id,
            pacienteB.Id,
            fecha,
            new TimeOnly(11, 30),
            new TimeOnly(12, 30));

        Assert.True(mismoOdontologo.OdontologoOcupado);
        Assert.False(mismoOdontologo.PacienteOcupado);
        Assert.False(mismoPaciente.OdontologoOcupado);
        Assert.True(mismoPaciente.PacienteOcupado);
        Assert.False(libre.OdontologoOcupado);
        Assert.False(libre.PacienteOcupado);
        Assert.False(excluyendoLaPropia.OdontologoOcupado);
        Assert.False(excluyendoLaPropia.PacienteOcupado);
        Assert.False(canceladaNoBloquea.OdontologoOcupado);
        Assert.False(canceladaNoBloquea.PacienteOcupado);
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
}

/// <summary>
/// Pruebas del repositorio de pagos: filtros financieros calculados en SQL y
/// totales agregados.
/// </summary>
public sealed class PagoRepositoryTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task FiltraEstadosFinancierosYCalculaTotales()
    {
        await using var db = fixture.CreateContext();
        var usuario = await TestData.AsegurarUsuarioAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var catalogo = await TestData.CrearTratamientoCatalogoAsync(db);

        var pendiente = await TestData.CrearTratamientoRealizadoAsync(
            db, paciente.Id, odontologo.Id, catalogo.Id, precio: 100m);

        var parcial = await TestData.CrearTratamientoRealizadoAsync(
            db, paciente.Id, odontologo.Id, catalogo.Id, precio: 100m);

        var pagado = await TestData.CrearTratamientoRealizadoAsync(
            db, paciente.Id, odontologo.Id, catalogo.Id, precio: 100m);

        db.Pagos.AddRange(
            new Pago
            {
                TratamientoRealizadoId = parcial.Id,
                Fecha = RelojNegocio.HoyDateOnly(),
                Monto = 40m,
                MetodoPago = MetodoPago.Efectivo,
                UsuarioId = usuario.Id
            },
            new Pago
            {
                TratamientoRealizadoId = pagado.Id,
                Fecha = RelojNegocio.HoyDateOnly(),
                Monto = 100m,
                MetodoPago = MetodoPago.Tarjeta,
                UsuarioId = usuario.Id
            });

        await db.SaveChangesAsync();

        var repositorio = new PagoRepository(db);

        var filtroPendiente = await repositorio.SearchTratamientosPagedAsync(
            searchTerm: null,
            EstadoFinanciero.Pendiente,
            paciente.Id,
            page: 1,
            pageSize: 10);

        var filtroParcial = await repositorio.SearchTratamientosPagedAsync(
            searchTerm: null,
            EstadoFinanciero.PagoParcial,
            paciente.Id,
            page: 1,
            pageSize: 10);

        var filtroPagado = await repositorio.SearchTratamientosPagedAsync(
            searchTerm: null,
            EstadoFinanciero.Pagado,
            paciente.Id,
            page: 1,
            pageSize: 10);

        var filtroTexto = await repositorio.SearchTratamientosPagedAsync(
            catalogo.Nombre,
            estado: null,
            paciente.Id,
            page: 1,
            pageSize: 10);

        var totales = await repositorio.GetTotalesAsync();

        Assert.Single(filtroPendiente.Items);
        Assert.Equal(pendiente.Id, filtroPendiente.Items[0].Id);
        Assert.Single(filtroParcial.Items);
        Assert.Equal(parcial.Id, filtroParcial.Items[0].Id);
        Assert.Single(filtroPagado.Items);
        Assert.Equal(pagado.Id, filtroPagado.Items[0].Id);
        Assert.Equal(3, filtroTexto.Total);
        Assert.Equal(300m, totales.TotalFacturado);
        Assert.Equal(140m, totales.TotalCobrado);
        Assert.Equal(2, totales.TratamientosConSaldo);
    }
}
