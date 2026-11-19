using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

/// <summary>
/// Persistencia de la bitácora de accesos. Es una tabla global, sin filtro por
/// clínica, porque los intentos ocurren antes de que exista una sesión.
/// </summary>
public interface IEventoAccesoRepository
{
    /// <summary>Guarda un intento de acceso de inmediato.</summary>
    Task AddAsync(EventoAcceso evento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca intentos con filtros y paginación del lado de la base. Devuelve la
    /// página efectiva para que la interfaz pueda corregir una página fuera de rango.
    /// </summary>
    Task<(IReadOnlyList<EventoAcceso> Eventos, int Total, int Pagina)> SearchAsync(
        string? texto,
        ResultadoAcceso? resultado,
        DateTime? desdeUtc,
        DateTime? hastaUtcExclusivo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default);
}
