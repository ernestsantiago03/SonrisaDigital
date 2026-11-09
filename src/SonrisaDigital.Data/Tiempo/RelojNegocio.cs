namespace SonrisaDigital.Data.Tiempo;

/// <summary>
/// Fecha y hora oficiales del negocio. La clínica opera en República Dominicana,
/// así que las fechas de calendario deben calcularse en su zona horaria y no en
/// la del servidor. Esto evita que un servidor en UTC cambie el día de un pago,
/// una consulta o un reporte registrado por la noche.
///
/// La zona se configura una sola vez al arrancar con <see cref="Configurar"/>.
/// Si el identificador no existe en el servidor, el arranque falla con un
/// mensaje claro en lugar de caer silenciosamente a UTC y desplazar las fechas.
/// </summary>
public static class RelojNegocio
{
    /// <summary>Identificador IANA de la zona por defecto del negocio.</summary>
    public const string ZonaNegocioIana = "America/Santo_Domingo";

    private const string ZonaNegocioWindows = "SA Western Standard Time";

    private static readonly object Candado = new();
    private static TimeZoneInfo? zona;
    private static TimeProvider proveedor = TimeProvider.System;

    /// <summary>Zona horaria configurada para el negocio.</summary>
    public static TimeZoneInfo ZonaHoraria
    {
        get
        {
            var actual = Volatile.Read(ref zona);
            if (actual is not null)
            {
                return actual;
            }

            lock (Candado)
            {
                // Si nadie llamó a Configurar (por ejemplo, una prueba unitaria o
                // las herramientas de Entity Framework), se resuelve la zona por
                // defecto. Nunca se sustituye por UTC en silencio.
                zona ??= ResolverZona(null);
                return zona;
            }
        }
    }

    /// <summary>
    /// Fija la zona del negocio y, opcionalmente, el origen del tiempo. Se llama
    /// al arrancar la aplicación. Un identificador inválido lanza una excepción.
    /// </summary>
    public static void Configurar(string? zonaHorariaId = null, TimeProvider? timeProvider = null)
    {
        var resuelta = ResolverZona(zonaHorariaId);

        lock (Candado)
        {
            zona = resuelta;
            proveedor = timeProvider ?? TimeProvider.System;
        }
    }

    /// <summary>Vuelve a la zona por defecto y al reloj del sistema. Pensado para pruebas.</summary>
    public static void Restablecer()
    {
        lock (Candado)
        {
            zona = null;
            proveedor = TimeProvider.System;
        }
    }

    /// <summary>Momento actual en la zona horaria del negocio, sin marca de zona.</summary>
    public static DateTime Ahora() => Ahora(proveedor);

    /// <summary>Momento actual en la zona del negocio usando un reloj concreto.</summary>
    public static DateTime Ahora(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, ZonaHoraria);
    }

    /// <summary>Fecha actual (sin hora) en la zona horaria del negocio.</summary>
    public static DateTime Hoy() => Ahora().Date;

    /// <summary>Fecha actual (sin hora) usando un reloj concreto.</summary>
    public static DateTime Hoy(TimeProvider timeProvider) => Ahora(timeProvider).Date;

    /// <summary>Fecha actual del negocio como <see cref="DateOnly"/>.</summary>
    public static DateOnly HoyDateOnly() => DateOnly.FromDateTime(Hoy());

    /// <summary>Fecha actual del negocio como <see cref="DateOnly"/> usando un reloj concreto.</summary>
    public static DateOnly HoyDateOnly(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(Hoy(timeProvider));

    /// <summary>
    /// Convierte una fecha local del negocio a UTC. Si la fecha llega sin marca
    /// de zona (como ocurre con los formularios), se interpreta como hora local
    /// de la clínica. Una fecha que ya viene en UTC se devuelve sin cambios y
    /// una fecha marcada como local de la máquina se convierte con su propio huso.
    /// </summary>
    public static DateTime AUtc(DateTime local) => local.Kind switch
    {
        DateTimeKind.Utc => local,
        DateTimeKind.Local => local.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(local, ZonaHoraria)
    };

    /// <summary>
    /// Convierte una fecha guardada en UTC a la hora local del negocio para mostrarla.
    /// Si el valor llega sin marca de zona (como lo devuelve SQL Server), se asume UTC.
    /// </summary>
    public static DateTime DeUtc(DateTime utc) => utc.Kind switch
    {
        DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(utc, ZonaHoraria),
        DateTimeKind.Local => TimeZoneInfo.ConvertTimeFromUtc(utc.ToUniversalTime(), ZonaHoraria),
        _ => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            ZonaHoraria)
    };

    private static TimeZoneInfo ResolverZona(string? zonaHorariaId)
    {
        var solicitada = string.IsNullOrWhiteSpace(zonaHorariaId)
            ? ZonaNegocioIana
            : zonaHorariaId.Trim();

        if (IntentarResolver(solicitada, out var encontrada))
        {
            return encontrada;
        }

        // El mismo identificador puede estar registrado con el nombre IANA
        // (Linux/macOS) o con el nombre de Windows (IIS). Se prueba el equivalente
        // antes de declarar la configuración inválida.
        foreach (var alternativa in Alternativas(solicitada))
        {
            if (IntentarResolver(alternativa, out encontrada))
            {
                return encontrada;
            }
        }

        throw new InvalidOperationException(
            $"La zona horaria '{solicitada}' no está disponible en este servidor. " +
            "Configura RelojNegocio:ZonaHoraria con un identificador IANA " +
            "(por ejemplo, America/Santo_Domingo) o de Windows " +
            "(por ejemplo, SA Western Standard Time).");
    }

    private static IEnumerable<string> Alternativas(string id)
    {
        if (id.Contains('/'))
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windows)
                && !string.IsNullOrWhiteSpace(windows))
            {
                yield return windows;
            }
        }
        else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana)
                 && !string.IsNullOrWhiteSpace(iana))
        {
            yield return iana;
        }

        if (string.Equals(id, ZonaNegocioIana, StringComparison.Ordinal))
        {
            yield return ZonaNegocioWindows;
        }
        else if (string.Equals(id, ZonaNegocioWindows, StringComparison.Ordinal))
        {
            yield return ZonaNegocioIana;
        }
    }

    private static bool IntentarResolver(string id, out TimeZoneInfo encontrada)
    {
        try
        {
            encontrada = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            // Se prueba el siguiente identificador.
        }
        catch (InvalidTimeZoneException)
        {
            // Una base de zonas horarias dañada no debe convertirse en UTC silencioso.
        }

        encontrada = null!;
        return false;
    }
}
