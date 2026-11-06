namespace SonrisaDigital.Services.Dashboard.DTOs;

/// <summary>
/// Cobros que se muestran en el panel del Inicio: lo cobrado hoy, lo que va del
/// mes y la cantidad de pagos registrados en ese mismo período.
/// </summary>
public sealed record DashboardCobrosDto(
    decimal CobradoHoy,
    decimal CobradoMes,
    int PagosMes);
