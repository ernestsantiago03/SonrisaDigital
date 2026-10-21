namespace SonrisaDigital.Services.Pagos.DTOs;

/// <summary>
/// Números para las tarjetas de resumen de la pantalla: total facturado, total cobrado,
/// saldo pendiente y cuántos tratamientos siguen con saldo.
/// </summary>
public sealed record PagosResumenDto(
    decimal TotalFacturado,
    decimal TotalCobrado,
    decimal SaldoPendiente,
    int TratamientosConSaldo);
