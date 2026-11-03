using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Pagos;
using SonrisaDigital.Services.Pagos.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Pruebas de las validaciones de dinero y del cálculo del saldo pendiente.
/// Las validaciones se ejecutan antes de abrir la transacción, por lo que no
/// necesitan una base de datos.
/// </summary>
public sealed class PagoServiceTests
{
    [Fact]
    public async Task RechazaTratamientoVacio()
    {
        var modelo = ModeloValido();
        modelo.TratamientoRealizadoId = 0;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().RegistrarAsync(modelo));

        Assert.Equal("El tratamiento seleccionado no es válido.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaMontoNulo()
    {
        var modelo = ModeloValido();
        modelo.Monto = null;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().RegistrarAsync(modelo));

        Assert.Equal("El monto del pago debe ser mayor que cero.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaMontoCero()
    {
        var modelo = ModeloValido();
        modelo.Monto = 0m;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().RegistrarAsync(modelo));

        Assert.Equal("El monto del pago debe ser mayor que cero.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaFechaVacia()
    {
        var modelo = ModeloValido();
        modelo.Fecha = default;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().RegistrarAsync(modelo));

        Assert.Equal("La fecha del pago no es válida.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaMetodoDePagoDesconocido()
    {
        var modelo = ModeloValido();
        modelo.MetodoPago = (MetodoPago)999;

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio().RegistrarAsync(modelo));

        Assert.Equal("El método de pago no es válido.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaOperacionSinPermiso()
    {
        var servicio = CrearServicio(new FakeAuthorizationService(permitir: false));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.RegistrarAsync(ModeloValido()));
    }

    [Fact]
    public async Task ResumenCalculaSaldoPendienteConTotalesDeLaBase()
    {
        var repositorio = new StubPagoRepository(new PagoTotales(
            TotalFacturado: 1_500m,
            TotalCobrado: 900m,
            TratamientosConSaldo: 3));

        var servicio = new PagoService(
            repositorio,
            TestSession.Crear(),
            new FakeAuthorizationService(),
            new FakeAuditoriaService(),
            null!,
            NullLogger<PagoService>.Instance);

        var resumen = await servicio.GetResumenAsync();

        Assert.Equal(1_500m, resumen.TotalFacturado);
        Assert.Equal(900m, resumen.TotalCobrado);
        Assert.Equal(600m, resumen.SaldoPendiente);
        Assert.Equal(3, resumen.TratamientosConSaldo);
    }

    private static PagoService CrearServicio(IAuthorizationService? autorizacion = null)
    {
        ICurrentUserService usuario = TestSession.Crear();

        return new PagoService(
            null!,
            usuario,
            autorizacion ?? new FakeAuthorizationService(),
            new FakeAuditoriaService(),
            null!,
            NullLogger<PagoService>.Instance);
    }

    private static RegistrarPagoDto ModeloValido() => new()
    {
        TratamientoRealizadoId = 1,
        Fecha = RelojNegocio.Hoy(),
        Monto = 100m,
        MetodoPago = MetodoPago.Efectivo
    };
}
