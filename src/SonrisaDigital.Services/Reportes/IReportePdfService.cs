using SonrisaDigital.Services.Reportes.DTOs;

namespace SonrisaDigital.Services.Reportes;

/// <summary>
/// Genera la versión PDF de un reporte. El Service reutiliza IReporteService,
/// por lo que mantiene los mismos filtros, permisos y alcance por odontólogo.
/// </summary>
public interface IReportePdfService
{
    /// <summary>
    /// Construye el PDF del tipo de reporte indicado. Devuelve el archivo en
    /// memoria para que la interfaz pueda descargarlo sin crear archivos temporales.
    /// </summary>
    Task<byte[]> GenerarAsync(
        TipoReportePdf tipo,
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default);
}
