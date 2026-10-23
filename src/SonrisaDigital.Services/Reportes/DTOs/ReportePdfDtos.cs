namespace SonrisaDigital.Services.Reportes.DTOs;

/// <summary>
/// Reporte que se va a convertir en PDF. La página envía el tipo de la pestaña
/// activa y el Service reutiliza exactamente las mismas consultas y permisos.
/// </summary>
public enum TipoReportePdf
{
    Citas,
    Pacientes,
    Tratamientos,
    Pagos
}
