using Microsoft.Extensions.Caching.Memory;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autorizacion;

namespace SonrisaDigital.Services.Catalogos;

/// <summary>
/// Implementación de la caché de catálogos con <see cref="IMemoryCache"/>.
///
/// Cada catálogo tiene una versión interna. Al invalidarlo se incrementa la
/// versión y las siguientes lecturas usan una clave nueva. Así, si una consulta
/// antigua termina justo después de una edición, su resultado queda en una clave
/// que ya nadie consulta y no vuelve a mostrar datos viejos.
/// </summary>
public sealed class CatalogoCache(IMemoryCache cache) : ICatalogoCache
{
    // La expiración es una red de seguridad. La invalidación explícita ocurre
    // cuando se guarda un cambio en el catálogo correspondiente.
    private static readonly TimeSpan Duracion = TimeSpan.FromMinutes(30);

    private long versionPiezas;
    private long versionTratamientos;
    private long versionRoles;

    public Task<IReadOnlyList<PiezaDental>> ObtenerPiezasAsync(
        Func<CancellationToken, Task<IReadOnlyList<PiezaDental>>> cargar,
        CancellationToken cancellationToken = default) =>
        ObtenerOCrearAsync(ClavePiezas, cargar, cancellationToken);

    public Task<IReadOnlyList<Tratamiento>> ObtenerTratamientosAsync(
        int clinicaId,
        bool soloActivos,
        Func<CancellationToken, Task<IReadOnlyList<Tratamiento>>> cargar,
        CancellationToken cancellationToken = default) =>
        ObtenerOCrearAsync(ClaveTratamientos(clinicaId, soloActivos), cargar, cancellationToken);

    public Task<IReadOnlyList<RolePermissionDto>> ObtenerRolesAsync(
        Func<CancellationToken, Task<IReadOnlyList<RolePermissionDto>>> cargar,
        CancellationToken cancellationToken = default) =>
        ObtenerOCrearAsync(ClaveRoles, cargar, cancellationToken);

    public void InvalidarPiezas() => Interlocked.Increment(ref versionPiezas);

    public void InvalidarTratamientos() => Interlocked.Increment(ref versionTratamientos);

    public void InvalidarRoles() => Interlocked.Increment(ref versionRoles);

    private string ClavePiezas =>
        $"catalogo:piezas:{Interlocked.Read(ref versionPiezas)}";

    private string ClaveTratamientos(int clinicaId, bool soloActivos) =>
        $"catalogo:tratamientos:{clinicaId}:{(soloActivos ? "activos" : "todos")}:{Interlocked.Read(ref versionTratamientos)}";

    private string ClaveRoles =>
        $"catalogo:roles-permisos:{Interlocked.Read(ref versionRoles)}";

    /// <summary>
    /// Busca el valor en la caché y, si no existe, lo carga y lo guarda.
    /// El valor devuelto siempre es una lista completa; nunca se comparte una
    /// entidad rastreada por Entity Framework.
    /// </summary>
    private async Task<T> ObtenerOCrearAsync<T>(
        string clave,
        Func<CancellationToken, Task<T>> cargar,
        CancellationToken cancellationToken)
        where T : class
    {
        if (cache.TryGetValue(clave, out T? valor) && valor is not null)
        {
            return valor;
        }

        var creado = await cache.GetOrCreateAsync(clave, async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = Duracion;
            return await cargar(cancellationToken);
        });

        return creado
            ?? throw new InvalidOperationException("No fue posible cargar el catálogo solicitado.");
    }
}
