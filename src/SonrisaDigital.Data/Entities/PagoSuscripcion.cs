namespace SonrisaDigital.Data.Entities;

/// <summary>
/// Pago de la mensualidad de una clínica. Solo lo consulta y registra el Dueño
/// del sistema desde el panel de facturación.
///
/// A propósito no implementa <c>IPerteneceAClinica</c>: el panel
/// de facturación necesita ver los pagos de todas las clínicas aunque el Dueño
/// no tenga una clínica seleccionada en el selector. La clínica se guarda de
/// forma explícita y el servicio de suscripciones valida el permiso.
/// </summary>
public class PagoSuscripcion
{
    public int Id { get; set; }

    public int ClinicaId { get; set; }
    public Clinica Clinica { get; set; } = null!;

    /// <summary>Fecha en que se recibió el pago (sin hora).</summary>
    public DateTime FechaPago { get; set; }

    /// <summary>Primer día que cubre este pago.</summary>
    public DateTime PeriodoDesde { get; set; }

    /// <summary>Fecha de vencimiento que deja este pago.</summary>
    public DateTime PeriodoHasta { get; set; }

    public decimal Monto { get; set; }

    /// <summary>Código ISO de la moneda. Mientras haya un solo plan se usa USD.</summary>
    public string Moneda { get; set; } = "USD";

    public MetodoPagoSuscripcion Metodo { get; set; }

    /// <summary>Número de transferencia, autorización de tarjeta o recibo.</summary>
    public string? Referencia { get; set; }

    public string? Notas { get; set; }

    public string RegistradoPorId { get; set; } = string.Empty;
    public Usuario RegistradoPor { get; set; } = null!;

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}

public enum MetodoPagoSuscripcion
{
    Transferencia,
    Efectivo,
    Tarjeta,
    Otro
}
