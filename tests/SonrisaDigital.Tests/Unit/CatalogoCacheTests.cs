using Microsoft.Extensions.Caching.Memory;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Catalogos;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Comprueba que la caché de catálogos reutiliza datos, separa las variantes de
/// tratamientos y no deja que una lectura lenta vuelva a publicar datos viejos
/// después de una invalidación.
/// </summary>
public sealed class CatalogoCacheTests
{
    [Fact]
    public async Task Piezas_SeCarganUnaVezYSeReutilizan()
    {
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var cache = new CatalogoCache(memoria);
        var lista = new List<PiezaDental>
        {
            new() { Id = 1, NumeroFDI = "11", Nombre = "Incisivo central" }
        };
        var cargas = 0;

        Task<IReadOnlyList<PiezaDental>> Cargar(CancellationToken _)
        {
            cargas++;
            return Task.FromResult<IReadOnlyList<PiezaDental>>(lista);
        }

        var primera = await cache.ObtenerPiezasAsync(Cargar);
        var segunda = await cache.ObtenerPiezasAsync(Cargar);

        Assert.Same(primera, segunda);
        Assert.Equal(1, cargas);
    }

    [Fact]
    public async Task Tratamientos_ActivosYTodosUsanEntradasSeparadas()
    {
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var cache = new CatalogoCache(memoria);
        var activos = new List<Tratamiento>
        {
            new() { Id = 1, Nombre = "Limpieza", PrecioBase = 100m, Estado = EstadoRegistro.Activo }
        };
        var todos = new List<Tratamiento>
        {
            new() { Id = 1, Nombre = "Limpieza", PrecioBase = 100m, Estado = EstadoRegistro.Activo },
            new() { Id = 2, Nombre = "Retirado", PrecioBase = 200m, Estado = EstadoRegistro.Inactivo }
        };
        var cargas = 0;

        Task<IReadOnlyList<Tratamiento>> Cargar(bool soloActivos, CancellationToken _)
        {
            cargas++;
            return Task.FromResult<IReadOnlyList<Tratamiento>>(soloActivos ? activos : todos);
        }

        var primeraActiva = await cache.ObtenerTratamientosAsync(1, true, ct => Cargar(true, ct));
        var segundaActiva = await cache.ObtenerTratamientosAsync(1, true, ct => Cargar(true, ct));
        var completa = await cache.ObtenerTratamientosAsync(1, false, ct => Cargar(false, ct));

        Assert.Same(primeraActiva, segundaActiva);
        Assert.NotSame(primeraActiva, completa);
        Assert.Equal(2, cargas);
    }

    [Fact]
    public async Task Tratamientos_CadaClinicaTieneSuPropiaEntrada()
    {
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var cache = new CatalogoCache(memoria);
        var cargas = 0;

        Task<IReadOnlyList<Tratamiento>> Cargar(int clinicaId, CancellationToken _)
        {
            cargas++;
            return Task.FromResult<IReadOnlyList<Tratamiento>>(
                new[] { new Tratamiento { Id = clinicaId, ClinicaId = clinicaId, Nombre = $"Clínica {clinicaId}", PrecioBase = 100m } });
        }

        var deLaPrimera = await cache.ObtenerTratamientosAsync(1, true, ct => Cargar(1, ct));
        var deLaSegunda = await cache.ObtenerTratamientosAsync(2, true, ct => Cargar(2, ct));

        Assert.Equal(2, cargas);
        Assert.Equal(1, deLaPrimera[0].ClinicaId);
        Assert.Equal(2, deLaSegunda[0].ClinicaId);
    }

    [Fact]
    public async Task Tratamientos_InvalidarAfectaATodasLasClinicas()
    {
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var cache = new CatalogoCache(memoria);
        var cargas = 0;

        Task<IReadOnlyList<Tratamiento>> Cargar(CancellationToken _)
        {
            cargas++;
            return Task.FromResult<IReadOnlyList<Tratamiento>>(Array.Empty<Tratamiento>());
        }

        await cache.ObtenerTratamientosAsync(1, true, Cargar);
        await cache.ObtenerTratamientosAsync(2, true, Cargar);
        cache.InvalidarTratamientos();
        await cache.ObtenerTratamientosAsync(1, true, Cargar);
        await cache.ObtenerTratamientosAsync(2, true, Cargar);

        Assert.Equal(4, cargas);
    }

    [Fact]
    public async Task Invalidar_UnaListaFuerzaUnaNuevaCarga()
    {
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var cache = new CatalogoCache(memoria);
        var cargas = 0;

        Task<IReadOnlyList<RolePermissionDto>> Cargar(CancellationToken _)
        {
            cargas++;
            return Task.FromResult<IReadOnlyList<RolePermissionDto>>(
                new[] { new RolePermissionDto($"rol-{cargas}", "Administrador", null, ["Roles.Ver"]) });
        }

        await cache.ObtenerRolesAsync(Cargar);
        cache.InvalidarRoles();
        await cache.ObtenerRolesAsync(Cargar);

        Assert.Equal(2, cargas);
    }

    [Fact]
    public async Task LecturaLentaNoReemplazaElResultadoNuevoDespuesDeInvalidar()
    {
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var cache = new CatalogoCache(memoria);
        var espera = new TaskCompletionSource<IReadOnlyList<PiezaDental>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var vieja = new List<PiezaDental>
        {
            new() { Id = 1, NumeroFDI = "11", Nombre = "Dato viejo" }
        };
        var nueva = new List<PiezaDental>
        {
            new() { Id = 1, NumeroFDI = "11", Nombre = "Dato nuevo" }
        };

        // La primera lectura queda esperando, como una consulta lenta a SQL Server.
        var lecturaVieja = cache.ObtenerPiezasAsync(_ => espera.Task);

        // Mientras la consulta vieja sigue en curso, se guarda un cambio del catálogo.
        cache.InvalidarPiezas();

        // La siguiente lectura usa la versión nueva y publica el dato actualizado.
        var lecturaNueva = await cache.ObtenerPiezasAsync(
            _ => Task.FromResult<IReadOnlyList<PiezaDental>>(nueva));

        // La consulta vieja termina después: debe quedar en una clave que ya nadie usa.
        espera.SetResult(vieja);
        await lecturaVieja;

        var comprobacion = await cache.ObtenerPiezasAsync(
            _ => throw new InvalidOperationException("No debía volver a cargar."));

        Assert.Same(lecturaNueva, comprobacion);
        Assert.Equal("Dato nuevo", comprobacion[0].Nombre);
    }
}
