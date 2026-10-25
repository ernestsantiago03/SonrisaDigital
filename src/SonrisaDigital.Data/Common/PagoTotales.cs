namespace SonrisaDigital.Data.Common;

/// <summary>
/// Totales del módulo de pagos calculados directamente en la base de datos.
/// El servicio usa estos números para armar las tarjetas de resumen.
/// </summary>
/// <param name="TotalFacturado">Suma de los precios de los tratamientos cobrables.</param>
/// <param name="TotalCobrado">Suma de los pagos registrados sobre esos tratamientos.</param>
/// <param name="TratamientosConSaldo">Cantidad de tratamientos cuyo precio supera lo pagado.</param>
public sealed record PagoTotales(
    decimal TotalFacturado,
    decimal TotalCobrado,
    int TratamientosConSaldo);
