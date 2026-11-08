using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autorizacion;

namespace SonrisaDigital.Services.Catalogos;

/// <summary>
/// Caché compartida para los catálogos globales que cambian poco y se consultan
/// desde varias pantallas. No guarda datos clínicos ni permisos efectivos de una
/// sesión: solo listas generales que pueden invalidarse cuando se editan.
/// </summary>
public interface ICatalogoCache
{
    /// <summary>
    /// Devuelve las piezas dentales desde la caché. Si no están guardadas, usa
    /// <paramref name="cargar"/> para consultarlas una sola vez.
    /// </summary>
    Task<IReadOnlyList<PiezaDental>> ObtenerPiezasAsync(
        Func<CancellationToken, Task<IReadOnlyList<PiezaDental>>> cargar,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los tratamientos activos o todos los tratamientos de una clínica, según
    /// <paramref name="soloActivos"/>. Cada clínica y cada variante tienen su propia entrada:
    /// el catálogo de tratamientos es por clínica y nunca debe compartirse entre ellas.
    /// </summary>
    Task<IReadOnlyList<Tratamiento>> ObtenerTratamientosAsync(
        int clinicaId,
        bool soloActivos,
        Func<CancellationToken, Task<IReadOnlyList<Tratamiento>>> cargar,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los roles con sus permisos asignados. Se invalida cuando cambia
    /// la configuración de permisos de un rol.
    /// </summary>
    Task<IReadOnlyList<RolePermissionDto>> ObtenerRolesAsync(
        Func<CancellationToken, Task<IReadOnlyList<RolePermissionDto>>> cargar,
        CancellationToken cancellationToken = default);

    /// <summary>Descarta la lista de piezas dentales guardada.</summary>
    void InvalidarPiezas();

    /// <summary>Descarta las listas de tratamientos activos y completos.</summary>
    void InvalidarTratamientos();

    /// <summary>Descarta la lista de roles y permisos.</summary>
    void InvalidarRoles();
}
