using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IAuditoriaRepository
{
    /// <summary>
    /// Guarda un registro de auditoría de forma inmediata.
    /// Se usa para las operaciones que no comparten una transacción con el cambio auditado.
    /// </summary>
    Task AddAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los movimientos más recientes.
    /// Se conserva para el panel resumido del Dashboard.
    /// </summary>
    Task<IReadOnlyList<RegistroAuditoria>> GetRecientesAsync(int cantidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca auditoría con filtros y paginación del lado de la base de datos.
    /// Devuelve también la página efectiva: si la solicitada quedó fuera de rango,
    /// se ajusta a la última página disponible.
    /// </summary>
    Task<(IReadOnlyList<RegistroAuditoria> Registros, int Total, int Pagina)> SearchAsync(
        string? texto,
        string? modulo,
        string? accion,
        DateTime? desdeUtc,
        DateTime? hastaUtcExclusivo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los valores distintos de módulo y acción que ya existen en la
    /// auditoría, para alimentar los filtros de la página sin inventar catálogos.
    /// </summary>
    Task<(IReadOnlyList<string> Modulos, IReadOnlyList<string> Acciones)> GetOpcionesAsync(
        CancellationToken cancellationToken = default);
}
