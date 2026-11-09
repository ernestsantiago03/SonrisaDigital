using System.ComponentModel.DataAnnotations;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Suscripciones;

namespace SonrisaDigital.Services.Suscripciones.DTOs;

/// <summary>Estado completo de la suscripción de una clínica para el panel del Dueño.</summary>
public sealed record ResumenSuscripcionDto(
    int ClinicaId,
    string Nombre,
    string Codigo,
    string? Plan,
    EstadoClinica Estado,
    MotivoSuspension? MotivoSuspension,
    bool EnPrueba,
    DateTime? FechaPrimerIngreso,
    DateTime? FechaVencimiento,
    int LimiteUsuarios,
    AccesoClinica Acceso,
    int DiasRestantes,
    decimal PrecioMensualUsd,
    IReadOnlyList<PagoSuscripcionDto> Pagos);

/// <summary>Pago registrado en el historial de una clínica.</summary>
public sealed record PagoSuscripcionDto(
    int Id,
    DateOnly FechaPago,
    DateOnly PeriodoDesde,
    DateOnly PeriodoHasta,
    decimal Monto,
    string Moneda,
    MetodoPagoSuscripcion Metodo,
    string? Referencia,
    string? Notas,
    string RegistradoPor,
    DateTime FechaRegistro);

/// <summary>Datos que captura el Dueño al registrar una mensualidad.</summary>
public sealed class RegistrarPagoSuscripcionDto
{
    public int ClinicaId { get; set; }

    /// <summary>Meses que cubre el pago. Por ahora el formulario usa 1.</summary>
    [Range(1, 36, ErrorMessage = "El pago debe cubrir entre 1 y 36 meses.")]
    public int Meses { get; set; } = 1;

    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Monto { get; set; }

    public MetodoPagoSuscripcion Metodo { get; set; } = MetodoPagoSuscripcion.Transferencia;

    [StringLength(100, ErrorMessage = "La referencia no puede superar los 100 caracteres.")]
    public string? Referencia { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden superar los 500 caracteres.")]
    public string? Notas { get; set; }
}

/// <summary>Extensión manual de días, siempre con un motivo auditado.</summary>
public sealed class ExtenderSuscripcionDto
{
    public int ClinicaId { get; set; }

    [Range(1, 365, ErrorMessage = "La extensión debe estar entre 1 y 365 días.")]
    public int Dias { get; set; } = 1;

    [Required(ErrorMessage = "El motivo de la extensión es obligatorio.")]
    [StringLength(300, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 300 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Aviso que ve una clínica cuando su suscripción está por vencer o en gracia.</summary>
public sealed record AvisoSuscripcionDto(
    AccesoClinica Acceso,
    int DiasRestantes,
    bool EnPrueba,
    DateTime? FechaVencimiento);
