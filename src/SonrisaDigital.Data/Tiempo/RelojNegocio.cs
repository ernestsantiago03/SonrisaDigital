namespace SonrisaDigital.Data.Tiempo;

/// <summary>
/// Fecha y hora oficiales del negocio. La clínica opera en República Dominicana,
/// así que las fechas de calendario deben calcularse en su zona horaria y no en
/// la del servidor. Esto evita que un servidor en UTC cambie el día de un pago,
/// una consulta o un reporte registrado por la noche.
/// </summary>
public static class RelojNegocio
{
    private const string ZonaNegocioIana = "America/Santo_Domingo";
    private const string ZonaNegocioWindows = "SA Western Standard Time";

    private static readonly Lazy<TimeZoneInfo> Zona = new(ResolverZonaNegocio);

    /// <summary>Zona horaria configurada para el negocio.</summary>
    public static TimeZoneInfo ZonaHoraria => Zona.Value;

    /// <summary>Momento actual en la zona horaria del negocio, sin marca de zona.</summary>
    public static DateTime Ahora() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona.Value);

    /// <summary>Fecha actual (sin hora) en la zona horaria del negocio.</summary>
    public static DateTime Hoy() => Ahora().Date;

    /// <summary>Fecha actual del negocio como <see cref="DateOnly"/>.</summary>
    public static DateOnly HoyDateOnly() => DateOnly.FromDateTime(Hoy());

    /// <summary>
    /// Convierte una fecha local del negocio a UTC. Si la fecha llega sin marca
    /// de zona (como ocurre con los formularios), se interpreta como hora local
    /// de la clínica.
    /// </summary>
    public static DateTime AUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            Zona.Value);

    /// <summary>
    /// Convierte una fecha guardada en UTC a la hora local del negocio para mostrarla.
    /// Si el valor llega sin marca de zona (como lo devuelve SQL Server), se asume UTC.
    /// </summary>
    public static DateTime DeUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            Zona.Value);

    private static TimeZoneInfo ResolverZonaNegocio()
    {
        foreach (var id in new[] { ZonaNegocioIana, ZonaNegocioWindows })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Se prueba el siguiente identificador (IANA en Linux/macOS, Windows en IIS).
            }
            catch (InvalidTimeZoneException)
            {
                // Una base de zonas horarias dañada no debe impedir el arranque.
            }
        }

        // Último recurso: si no hay base de zonas, se usa UTC. Es preferible a
        // fallar el login de todas las clínicas.
        return TimeZoneInfo.Utc;
    }
}
