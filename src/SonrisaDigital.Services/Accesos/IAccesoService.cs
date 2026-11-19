using SonrisaDigital.Services.Accesos.DTOs;

namespace SonrisaDigital.Services.Accesos;

/// <summary>
/// Bitácora de intentos de acceso. El registro es tolerante a fallos: una caída de
/// la base no debe impedir que un usuario entre al sistema. La consulta exige el
/// permiso Auditoria.Ver.
/// </summary>
public interface IAccesoService
{
    /// <summary>Registra un intento. Nunca lanza por un fallo de escritura.</summary>
    Task RegistrarAsync(RegistroAccesoDto registro, CancellationToken cancellationToken = default);

    /// <summary>Busca intentos con filtros y paginación. Exige Auditoria.Ver.</summary>
    Task<AccesoPaginaDto> SearchAsync(
        AccesoFiltroDto filtro,
        CancellationToken cancellationToken = default);
}
