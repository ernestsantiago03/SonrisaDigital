using System.ComponentModel.DataAnnotations;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Pagos.DTOs;

/// <summary>
/// Datos que el usuario llena en la ventana "Registrar pago": a qué tratamiento corresponde,
/// la fecha del pago, el monto, el método de pago y una observación opcional.
/// </summary>
public sealed class RegistrarPagoDto
{
    public int TratamientoRealizadoId { get; set; }

    public DateTime Fecha { get; set; } = DateTime.Now;

    [Range(0.01, 999999999.99, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal? Monto { get; set; }

    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;

    [StringLength(1000, ErrorMessage = "Las observaciones no pueden superar los 1000 caracteres.")]
    public string? Observaciones { get; set; }
}
