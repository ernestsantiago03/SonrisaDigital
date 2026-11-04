using System.Threading.RateLimiting;

namespace SonrisaDigital.UI.Services;

/// <summary>
/// Límite de intentos de acceso por cuenta, independiente de la IP. Protege una cuenta concreta de la
/// fuerza bruta aunque el ataque venga de muchas IPs. El límite por IP es más alto porque una clínica
/// puede salir toda por una misma IP pública.
/// </summary>
public sealed class LimitadorCuentaLogin : IDisposable
{
    public const int IntentosPorMinutoPredeterminado = 10;

    private readonly PartitionedRateLimiter<string> _limitador;

    public LimitadorCuentaLogin(int intentosPorMinuto)
    {
        _limitador = PartitionedRateLimiter.Create<string, string>(
            correo => RateLimitPartition.GetFixedWindowLimiter(
                correo,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = intentosPorMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    }

    /// <summary>True si la cuenta todavía puede intentar entrar en este minuto.</summary>
    public bool PermitirIntento(string correo)
    {
        using var lease = _limitador.AttemptAcquire(correo.Trim().ToUpperInvariant());
        return lease.IsAcquired;
    }

    public void Dispose() => _limitador.Dispose();
}
