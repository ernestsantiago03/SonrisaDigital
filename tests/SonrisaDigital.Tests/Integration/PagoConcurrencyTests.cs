using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Pagos;
using SonrisaDigital.Services.Pagos.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Prueba real contra SQL Server: dos cajas intentan cobrar RD$60 sobre un
/// tratamiento de RD$100. El bloqueo UPDLOCK/HOLDLOCK debe dejar un solo pago
/// y rechazar el segundo por falta de saldo.
/// </summary>
public sealed class PagoConcurrencyTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task DosCobrosSimultaneosNoSuperanElSaldo()
    {
        await using var preparacion = fixture.CreateContext();
        var usuario = await TestData.AsegurarUsuarioAsync(preparacion);
        var paciente = await TestData.CrearPacienteAsync(preparacion);
        var odontologo = await TestData.CrearOdontologoAsync(preparacion);
        var catalogo = await TestData.CrearTratamientoCatalogoAsync(preparacion);
        var tratamiento = await TestData.CrearTratamientoRealizadoAsync(
            preparacion,
            paciente.Id,
            odontologo.Id,
            catalogo.Id,
            precio: 100m);

        var modelo = new RegistrarPagoDto
        {
            TratamientoRealizadoId = tratamiento.Id,
            Fecha = DateTime.Today,
            Monto = 60m,
            MetodoPago = MetodoPago.Efectivo,
            Observaciones = "Cobro simultáneo"
        };

        var barrera = new AsyncBarrier(2);

        await using var db1 = fixture.CreateContext();
        await using var db2 = fixture.CreateContext();

        var servicio1 = CrearServicio(db1, barrera);
        var servicio2 = CrearServicio(db2, barrera);

        var resultados = await Task.WhenAll(
            EjecutarAsync(() => servicio1.RegistrarAsync(modelo)),
            EjecutarAsync(() => servicio2.RegistrarAsync(modelo)));

        Assert.Equal(1, resultados.Count(resultado => resultado.Exito));
        Assert.Equal(1, resultados.Count(resultado => !resultado.Exito));

        await using var comprobacion = fixture.CreateContext();
        var pagos = await comprobacion.Pagos
            .Where(pago => pago.TratamientoRealizadoId == tratamiento.Id)
            .ToListAsync();

        Assert.Single(pagos);
        Assert.Equal(60m, pagos[0].Monto);

        var estado = await comprobacion.TratamientosRealizados
            .Where(item => item.Id == tratamiento.Id)
            .Select(item => item.Estado)
            .SingleAsync();

        Assert.Equal(EstadoTratamiento.EnProceso, estado);
    }

    private static PagoService CrearServicio(
        SonrisaDigital.Data.ApplicationDbContext db,
        AsyncBarrier barrera)
    {
        var repositorio = new BarrierPagoRepository(new PagoRepository(db), barrera);

        return new PagoService(
            repositorio,
            TestSession.Crear(),
            new FakeAuthorizationService(),
            new FakeAuditoriaService(),
            db,
            NullLogger<PagoService>.Instance);
    }

    private static async Task<(bool Exito, Exception? Error)> EjecutarAsync(Func<Task<int>> operacion)
    {
        try
        {
            await operacion();
            return (true, null);
        }
        catch (Exception excepcion)
        {
            return (false, excepcion);
        }
    }
}
