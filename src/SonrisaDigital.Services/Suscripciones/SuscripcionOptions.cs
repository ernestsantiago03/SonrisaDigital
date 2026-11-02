namespace SonrisaDigital.Services.Suscripciones;

/// <summary>
/// Configuración comercial de la suscripción. Mientras haya un solo plan, el
/// precio y los días de aviso se cambian aquí sin tocar código.
/// </summary>
public sealed class SuscripcionOptions
{
    public const string SectionName = "Suscripcion";

    /// <summary>Precio mensual en dólares que se propone al registrar un pago.</summary>
    public decimal PrecioMensualUsd { get; set; } = 20m;

    /// <summary>Días antes del vencimiento en que la clínica empieza a ver el aviso.</summary>
    public int DiasAviso { get; set; } = 5;
}
