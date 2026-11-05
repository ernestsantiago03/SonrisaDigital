using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Catalogos;
using SonrisaDigital.Services.Tratamientos;
using SonrisaDigital.Services.Tratamientos.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Tratamientos realizados con pagos: solo el Dueño puede modificarlos.
/// </summary>
public sealed class TratamientoPagosTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task UnAdministradorNoPuedeEditarUnTratamientoConPagos()
    {
        var realizado = await PrepararRealizadoConPagoAsync(precio: 100m, monto: 60m);
        await using var provider = await CrearProveedorAsync(RolesSonrisaDigital.Administrador);
        var servicio = provider.GetRequiredService<ITratamientoService>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.UpdateRealizadoAsync(Formulario(realizado, precio: 120m)));

        Assert.Contains("Solo el Dueño", error.Message);
    }

    [Fact]
    public async Task ElDuenoPuedeModificarUnTratamientoConPagos()
    {
        var realizado = await PrepararRealizadoConPagoAsync(precio: 100m, monto: 60m);
        await using var provider = await CrearProveedorAsync(RolesSonrisaDigital.Dueno);
        var servicio = provider.GetRequiredService<ITratamientoService>();

        await servicio.UpdateRealizadoAsync(Formulario(realizado, precio: 150m));

        await using var comprobacion = fixture.CreateContext();
        var actualizado = await comprobacion.TratamientosRealizados.SingleAsync(item => item.Id == realizado.Id);
        Assert.Equal(150m, actualizado.Precio);
        Assert.Equal(EstadoTratamiento.EnProceso, actualizado.Estado);
    }

    [Fact]
    public async Task IgualarElPrecioAlPagadoNoCambiaElEstadoClinico()
    {
        var realizado = await PrepararRealizadoConPagoAsync(precio: 100m, monto: 60m);
        await using var provider = await CrearProveedorAsync(RolesSonrisaDigital.Dueno);
        var servicio = provider.GetRequiredService<ITratamientoService>();

        await servicio.UpdateRealizadoAsync(Formulario(realizado, precio: 60m));

        await using var comprobacion = fixture.CreateContext();
        var actualizado = await comprobacion.TratamientosRealizados.SingleAsync(item => item.Id == realizado.Id);
        Assert.Equal(EstadoTratamiento.EnProceso, actualizado.Estado);
    }

    [Fact]
    public async Task MarcarCompletadoYReabrirSoloCambianElEstadoClinico()
    {
        var realizado = await PrepararRealizadoConPagoAsync(precio: 100m, monto: 60m);
        await using var provider = await CrearProveedorAsync(RolesSonrisaDigital.Dueno);
        var servicio = provider.GetRequiredService<ITratamientoService>();

        await servicio.MarcarCompletadoAsync(realizado.Id);

        await using (var comprobacion = fixture.CreateContext())
        {
            var completado = await comprobacion.TratamientosRealizados.SingleAsync(item => item.Id == realizado.Id);
            Assert.Equal(EstadoTratamiento.Completado, completado.Estado);
            Assert.Equal(100m, completado.Precio);
        }

        await servicio.ReabrirRealizadoAsync(realizado.Id);

        await using var reabierto = fixture.CreateContext();
        var actualizado = await reabierto.TratamientosRealizados.SingleAsync(item => item.Id == realizado.Id);
        Assert.Equal(EstadoTratamiento.EnProceso, actualizado.Estado);
    }

    [Fact]
    public async Task UnAdministradorPuedeEditarUnTratamientoSinPagos()
    {
        var realizado = await PrepararRealizadoConPagoAsync(precio: 100m, monto: null);
        await using var provider = await CrearProveedorAsync(RolesSonrisaDigital.Administrador);
        var servicio = provider.GetRequiredService<ITratamientoService>();

        await servicio.UpdateRealizadoAsync(Formulario(realizado, precio: 120m));

        await using var comprobacion = fixture.CreateContext();
        var actualizado = await comprobacion.TratamientosRealizados.SingleAsync(item => item.Id == realizado.Id);
        Assert.Equal(120m, actualizado.Precio);
    }

    private async Task<TratamientoRealizado> PrepararRealizadoConPagoAsync(decimal precio, decimal? monto)
    {
        await using var db = fixture.CreateContext();
        var usuario = await TestData.AsegurarUsuarioAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var catalogo = await TestData.CrearTratamientoCatalogoAsync(db);
        var realizado = await TestData.CrearTratamientoRealizadoAsync(
            db, paciente.Id, odontologo.Id, catalogo.Id, precio);

        if (monto is decimal montoPagado)
        {
            db.Pagos.Add(new Pago
            {
                TratamientoRealizadoId = realizado.Id,
                Fecha = RelojNegocio.Hoy(),
                Monto = montoPagado,
                MetodoPago = MetodoPago.Efectivo,
                UsuarioId = usuario.Id
            });
            await db.SaveChangesAsync();
        }

        return realizado;
    }

    private static TratamientoRealizadoFormDto Formulario(TratamientoRealizado realizado, decimal precio) => new()
    {
        Id = realizado.Id,
        PacienteId = realizado.PacienteId,
        TratamientoId = realizado.TratamientoId,
        OdontologoId = realizado.OdontologoId,
        Fecha = RelojNegocio.Hoy(),
        Precio = precio,
        Estado = EstadoTratamiento.EnProceso,
        Observaciones = "Editado en prueba"
    };

    private async Task<ServiceProvider> CrearProveedorAsync(params string[] roles)
    {
        await using var db = fixture.CreateContext();
        var cadenaConexion = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("La base de pruebas no tiene cadena de conexión.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(cadenaConexion));

        services.AddSingleton<ITenantProvider>(new TenantMutable { ClinicaId = fixture.ClinicaId });
        services.AddSingleton<ICurrentUserService>(TestSession.Crear(TestSession.UsuarioId, roles));
        services.AddSingleton<IAuthorizationService>(new FakeAuthorizationService());
        services.AddSingleton<IAuditoriaService>(new FakeAuditoriaService());
        services.AddSingleton<ICatalogoCache, CatalogoCache>();
        services.AddScoped<ITratamientoRepository, TratamientoRepository>();
        services.AddScoped<ITratamientoService, TratamientoService>();

        return services.BuildServiceProvider();
    }
}
