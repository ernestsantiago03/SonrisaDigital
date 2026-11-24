using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SonrisaDigital.Services.Salud;

/// <summary>
/// Limites compartidos por los health checks.
/// </summary>
public static class ConfiguracionSalud
{
    /// <summary>
    /// Tope por comprobacion. Debe quedar por debajo del proxy_read_timeout de
    /// nginx (15 s) para que el balanceador reciba el 503 de la aplicacion y no
    /// un 504 del proxy.
    /// </summary>
    public static readonly TimeSpan TiempoMaximoPorCheck = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Aplica el tope por defecto a los registros ya creados por AddHealthChecks.
    /// </summary>
    public static void AplicarTiempoMaximo(HealthCheckServiceOptions opciones) =>
        AplicarTiempoMaximo(opciones, TiempoMaximoPorCheck);

    /// <summary>
    /// Aplica un tope concreto. AddDbContextCheck no acepta timeout en su firma,
    /// asi que se ajusta sobre el registro. El framework crea por cada check un
    /// CancellationToken con caducidad, de modo que EF y SqlClient abortan la
    /// conexion al vencer en lugar de esperar todos los reintentos.
    /// </summary>
    public static void AplicarTiempoMaximo(HealthCheckServiceOptions opciones, TimeSpan tiempoMaximo)
    {
        foreach (var registro in opciones.Registrations)
        {
            registro.Timeout = tiempoMaximo;
        }
    }
}
