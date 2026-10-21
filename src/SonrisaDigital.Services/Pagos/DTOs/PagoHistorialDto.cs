using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Pagos.DTOs;

/// <summary>
/// Una fila del historial de pagos: cuándo se pagó, a qué paciente y tratamiento corresponde,
/// el monto, el método de pago y quién lo registró.
/// </summary>
public sealed record PagoHistorialDto(
    int Id,
    int TratamientoRealizadoId,
    DateTime Fecha,
    string PacienteNombre,
    string TratamientoNombre,
    decimal Monto,
    MetodoPago MetodoPago,
    string RegistradoPor);
