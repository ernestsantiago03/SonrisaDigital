namespace SonrisaDigital.Services.Auditoria.DTOs;

/// <summary>
/// Parámetros de búsqueda de la página de auditoría.
/// Las fechas se reciben en UTC para que la consulta no dependa de la zona horaria
/// del servidor donde corre la aplicación.
/// </summary>
public sealed record AuditoriaFiltroDto(
    string? Texto = null,
    string? Modulo = null,
    string? Accion = null,
    DateTime? DesdeUtc = null,
    DateTime? HastaUtcExclusivo = null,
    int Pagina = 1,
    int TamanoPagina = 25);

/// <summary>
/// Resultado paginado de auditoría. Incluye el total para que la interfaz pueda
/// mostrar cuántos movimientos cumplen el filtro y navegar entre páginas.
/// </summary>
public sealed record AuditoriaPaginaDto(
    IReadOnlyList<ActividadRecienteDto> Registros,
    int Total,
    int Pagina,
    int TamanoPagina)
{
    /// <summary>Número de páginas disponibles; siempre al menos una para la UI.</summary>
    public int TotalPaginas => TamanoPagina <= 0
        ? 1
        : Math.Max(1, (int)Math.Ceiling(Total / (double)TamanoPagina));

    /// <summary>Indica si se puede retroceder a una página anterior.</summary>
    public bool TienePaginaAnterior => Pagina > 1;

    /// <summary>Indica si se puede avanzar a una página siguiente.</summary>
    public bool TienePaginaSiguiente => Pagina < TotalPaginas;
}

/// <summary>
/// Valores disponibles para los filtros de módulo y acción.
/// Se cargan desde los registros reales de auditoría.
/// </summary>
public sealed record AuditoriaOpcionesDto(
    IReadOnlyList<string> Modulos,
    IReadOnlyList<string> Acciones);
