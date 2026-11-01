using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Suscripciones;

/// <summary>Resultado de evaluar si una clínica puede operar en una fecha dada.</summary>
public enum AccesoClinica
{
    /// <summary>La clínica está al día y puede usar el sistema.</summary>
    Permitido,

    /// <summary>La mensualidad venció pero está dentro de los días de gracia.</summary>
    EnGracia,

    /// <summary>La mensualidad venció y se agotaron los días de gracia.</summary>
    Vencida,

    /// <summary>Un administrador suspendió la clínica de forma manual.</summary>
    Suspendida
}

/// <summary>
/// Única fuente de verdad para decidir si una clínica puede operar. La usan el
/// login, el middleware de navegación y la revalidación de sesiones abiertas.
///
/// El vencimiento no se guarda como estado: se calcula en cada acceso con la
/// fecha de la zona horaria del negocio. Así un proceso programado que falle no
/// deja clínicas trabajando gratis ni corta el servicio por error.
/// </summary>
public static class ReglaSuscripcion
{
    /// <summary>Días de prueba que recibe una clínica nueva desde su primer ingreso.</summary>
    public const int DiasPrueba = 30;

    /// <summary>Días de gracia después del vencimiento antes de bloquear el acceso.</summary>
    public const int DiasGracia = 3;

    // La clínica opera en República Dominicana. La fecha debe calcularse en su
    // zona horaria: con UTC, el último día se cortaría a las 8:00 p. m. locales.
    private const string ZonaNegocioIana = "America/Santo_Domingo";
    private const string ZonaNegocioWindows = "SA Western Standard Time";

    private static readonly Lazy<TimeZoneInfo> ZonaNegocio = new(ResolverZonaNegocio);

    /// <summary>Fecha actual (sin hora) en la zona horaria del negocio.</summary>
    public static DateTime Hoy() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaNegocio.Value).Date;

    /// <summary>
    /// Evalúa el acceso de una clínica. La suspensión manual siempre gana; si la
    /// prueba aún no empezó, se permite entrar para que ese primer ingreso la inicie.
    /// </summary>
    public static AccesoClinica Evaluar(
        EstadoClinica estado,
        bool enPrueba,
        DateTime? fechaPrimerIngreso,
        DateTime? fechaVencimiento,
        DateTime hoy,
        int diasGracia = DiasGracia)
    {
        if (estado == EstadoClinica.Suspendida)
        {
            return AccesoClinica.Suspendida;
        }

        if (enPrueba && fechaPrimerIngreso is null)
        {
            // La prueba empieza en el primer ingreso; todavía no hay fecha que comparar.
            return AccesoClinica.Permitido;
        }

        if (fechaVencimiento is null)
        {
            // Clínicas anteriores a la suscripción (por ejemplo, la clínica demo)
            // siguen operando hasta que se les asigne una fecha.
            return AccesoClinica.Permitido;
        }

        var vence = fechaVencimiento.Value.Date;
        if (hoy.Date <= vence)
        {
            return AccesoClinica.Permitido;
        }

        // La gracia se cuenta desde el día siguiente al vencimiento.
        return hoy.Date <= vence.AddDays(Math.Max(0, diasGracia))
            ? AccesoClinica.EnGracia
            : AccesoClinica.Vencida;
    }

    /// <summary>Fecha de vencimiento que corresponde al primer ingreso de una prueba.</summary>
    public static DateTime VencimientoDePrueba(DateTime hoy) =>
        hoy.Date.AddDays(DiasPrueba);

    /// <summary>
    /// Primer día que cubre una renovación. Si la clínica todavía tiene tiempo
    /// vigente, el nuevo período empieza al vencer; si ya venció, empieza hoy.
    /// </summary>
    public static DateTime InicioPeriodo(DateTime? fechaVencimiento, DateTime hoy) =>
        fechaVencimiento is DateTime vencimiento && vencimiento.Date > hoy.Date
            ? vencimiento.Date
            : hoy.Date;

    /// <summary>
    /// Calcula el nuevo vencimiento al registrar un pago. Si la clínica todavía
    /// tiene tiempo vigente, los meses se suman a esa fecha; si ya venció, se
    /// suman desde hoy para no cobrar días que no se van a usar.
    /// </summary>
    public static DateTime Renovar(DateTime? fechaVencimiento, DateTime hoy, int meses = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(meses, 1);

        return InicioPeriodo(fechaVencimiento, hoy).AddMonths(meses);
    }

    /// <summary>
    /// Días que faltan para el vencimiento. Un valor negativo significa que ya
    /// venció. Se usa para los avisos de la interfaz.
    /// </summary>
    public static int DiasRestantes(DateTime? fechaVencimiento, DateTime hoy) =>
        fechaVencimiento is DateTime vencimiento
            ? (int)(vencimiento.Date - hoy.Date).TotalDays
            : 0;

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
