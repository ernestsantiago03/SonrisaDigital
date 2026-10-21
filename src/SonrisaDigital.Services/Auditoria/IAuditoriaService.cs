using SonrisaDigital.Services.Auditoria.DTOs;

namespace SonrisaDigital.Services.Auditoria;

public interface IAuditoriaService
{
    /// <summary>
    /// Registra un movimiento. Si no hay una sesión autenticada, falla de forma
    /// explícita en lugar de omitir la auditoría en silencio.
    /// </summary>
    Task RegistrarAsync(
        string accion,
        string modulo,
        string entidad,
        int? entidadId,
        string descripcion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los movimientos más recientes para el resumen del Dashboard.
    /// Exige el permiso Auditoria.Ver.
    /// </summary>
    Task<IReadOnlyList<ActividadRecienteDto>> GetRecientesAsync(
        int cantidad = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca la auditoría con filtros y paginación.
    /// Exige el permiso Auditoria.Ver.
    /// </summary>
    Task<AuditoriaPaginaDto> SearchAsync(
        AuditoriaFiltroDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los módulos y acciones disponibles para los filtros.
    /// Exige el permiso Auditoria.Ver.
    /// </summary>
    Task<AuditoriaOpcionesDto> GetOpcionesAsync(CancellationToken cancellationToken = default);
}
