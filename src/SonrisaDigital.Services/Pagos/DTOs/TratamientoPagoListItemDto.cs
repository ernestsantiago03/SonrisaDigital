using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Pagos.DTOs;

/// <summary>
/// Una fila de la tabla de Pagos: el tratamiento, el paciente al que pertenece, los montos
/// (total, pagado y saldo), el último pago que se hizo y el estado de pago.
/// </summary>
public sealed record TratamientoPagoListItemDto(
    int TratamientoRealizadoId,
    int PacienteId,
    string PacienteNombre,
    string TratamientoNombre,
    decimal Total,
    decimal Pagado,
    decimal Saldo,
    EstadoTratamiento EstadoClinico,
    DateTime? UltimoPago,
    MetodoPago? UltimoMetodo,
    EstadoFinanciero Estado);
