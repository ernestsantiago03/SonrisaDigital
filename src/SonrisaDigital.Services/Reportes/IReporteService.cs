using SonrisaDigital.Services.Reportes.DTOs;

namespace SonrisaDigital.Services.Reportes;

/// <summary>
/// Casos de uso del módulo de Reportes. Todas las operaciones son de consulta y
/// validan los permisos de la sesión antes de acceder a los datos.
/// </summary>
public interface IReporteService
{
    /// <summary>Carga odontólogos y capacidades visibles para la página.</summary>
    Task<ReporteOpcionesDto> GetOpcionesAsync(CancellationToken cancellationToken = default);

    /// <summary>Resumen de citas, pacientes, tratamientos y pagos del período.</summary>
    Task<ReporteResumenDto> GetResumenAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Detalle de citas con los filtros de la pestaña.</summary>
    Task<IReadOnlyList<ReporteCitaItemDto>> GetCitasAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Pacientes únicos con al menos una cita atendida en el período.</summary>
    Task<IReadOnlyList<ReportePacienteItemDto>> GetPacientesAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Tratamientos realizados en el período con su estado de cobro.</summary>
    Task<IReadOnlyList<ReporteTratamientoItemDto>> GetTratamientosAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Pagos registrados en el período. Requiere Pagos.Ver.</summary>
    Task<IReadOnlyList<ReportePagoItemDto>> GetPagosAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default);
}
