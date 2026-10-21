using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Pagos.DTOs;

/// <summary>
/// Un pago (cuota) registrado sobre un tratamiento, con el detalle completo que
/// se muestra en la ficha: fecha, monto, método, quién lo registró y las
/// observaciones que se le agregaron.
/// </summary>
public sealed record PagoDetalleItemDto(
    int Id,
    DateTime Fecha,
    decimal Monto,
    MetodoPago MetodoPago,
    string RegistradoPor,
    string? Observaciones);

/// <summary>
/// Ficha completa de un tratamiento realizado: datos del paciente, del tratamiento
/// y el historial de cuotas (pagos) que se han ido registrando.
/// </summary>
public sealed record PagoDetalleDto(
    int TratamientoRealizadoId,
    int PacienteId,
    string PacienteNombre,
    string? PacienteIdentificacion,
    DateOnly? PacienteFechaNacimiento,
    string? PacienteTelefono,
    string? PacienteCorreo,
    string TratamientoNombre,
    string OdontologoNombre,
    string? PiezaDental,
    DateTime Fecha,
    EstadoTratamiento EstadoClinico,
    string? ObservacionesTratamiento,
    decimal Total,
    decimal Pagado,
    decimal Saldo,
    EstadoFinanciero Estado,
    IReadOnlyList<PagoDetalleItemDto> Pagos);
