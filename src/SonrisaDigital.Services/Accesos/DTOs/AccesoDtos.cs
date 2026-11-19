using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Accesos.DTOs;

/// <summary>Datos de un intento de acceso que se va a registrar.</summary>
public sealed record RegistroAccesoDto(
    string? UsuarioId,
    string? Correo,
    ResultadoAcceso Resultado,
    int? ClinicaId = null,
    string? DireccionIp = null,
    string? Agente = null,
    string? Detalle = null);

/// <summary>
/// Parámetros de búsqueda de la bitácora de accesos. Las fechas se reciben en UTC
/// para que la consulta no dependa de la zona horaria del servidor.
/// </summary>
public sealed record AccesoFiltroDto(
    string? Texto = null,
    ResultadoAcceso? Resultado = null,
    DateTime? DesdeUtc = null,
    DateTime? HastaUtcExclusivo = null,
    int Pagina = 1,
    int TamanoPagina = 25);

/// <summary>Fila de la bitácora de accesos que consume la interfaz.</summary>
public sealed record EventoAccesoDto(
    long Id,
    string? Correo,
    string Usuario,
    ResultadoAcceso Resultado,
    DateTime FechaHora,
    string? DireccionIp,
    string? Agente,
    string? Detalle);

/// <summary>Resultado paginado de la bitácora de accesos.</summary>
public sealed record AccesoPaginaDto(
    IReadOnlyList<EventoAccesoDto> Eventos,
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
