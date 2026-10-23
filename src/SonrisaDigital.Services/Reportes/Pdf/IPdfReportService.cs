using SonrisaDigital.Services.Reportes.DTOs;

namespace SonrisaDigital.Services.Reportes.Pdf;

/// <summary>
/// Exportación de los reportes a PDF con QuestPDF. Cada método consulta sus datos
/// a través de <see cref="IReporteService"/>, de modo que la autorización se valida
/// con la misma lógica de la página antes de generar el archivo.
/// </summary>
public interface IPdfReportService
{
    Task<byte[]> GenerarCitasPdfAsync(ReporteFiltroDto filtro, CancellationToken cancellationToken = default);
    Task<byte[]> GenerarPacientesPdfAsync(ReporteFiltroDto filtro, CancellationToken cancellationToken = default);
    Task<byte[]> GenerarTratamientosPdfAsync(ReporteFiltroDto filtro, CancellationToken cancellationToken = default);
    Task<byte[]> GenerarPagosPdfAsync(ReporteFiltroDto filtro, CancellationToken cancellationToken = default);
}