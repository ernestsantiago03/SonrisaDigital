using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SonrisaDigital.Services.Salud;

/// <summary>
/// Unica traduccion del informe de salud al contrato JSON que consumen el
/// balanceador, nginx y los scripts de despliegue:
///
///   /health        -> {"status":"Healthy"}
///   /health/ready  -> {"status":"Healthy"|"Unhealthy","database":"reachable"|"unreachable"|"schema-outdated"|"error"}
///
/// El estado se calcula a partir de los nombres de los health checks registrados,
/// no de la ruta, para que live y ready no puedan describir la base de datos de
/// forma distinta.
/// </summary>
public static class RespuestaSalud
{
    /// <summary>Nombre del health check que valida la conexion con SQL Server.</summary>
    public const string CheckBaseDatos = "database";

    /// <summary>Nombre del health check que valida las migraciones pendientes.</summary>
    public const string CheckMigraciones = "migraciones";

    /// <summary>Etiqueta de los checks que solo deben ejecutarse en /health/ready.</summary>
    public const string EtiquetaListo = "ready";

    /// <summary>
    /// Escribe la respuesta de liveness. No incluye datos de la base porque ese
    /// endpoint solo confirma que el proceso esta en marcha.
    /// </summary>
    public static Task EscribirLiveAsync(HttpContext context, HealthReport informe)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new { status = informe.Status.ToString() }));
    }

    /// <summary>
    /// Escribe la respuesta de readiness, que ademas confirma la base de datos y
    /// el esquema.
    /// </summary>
    public static Task EscribirReadyAsync(HttpContext context, HealthReport informe)
    {
        var (estado, baseDatos) = Interpretar(informe);

        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new { status = estado, database = baseDatos }));
    }

    /// <summary>
    /// Traduce el informe a los dos valores que conoce el contrato. Cuando la base
    /// no responde no se consulta el esquema: primero se informa el motivo real
    /// para no diagnosticar "migraciones pendientes" en una base caida.
    /// </summary>
    public static (string Estado, string BaseDatos) Interpretar(HealthReport informe)
    {
        var estado = informe.Status.ToString();

        if (!informe.Entries.TryGetValue(CheckBaseDatos, out var baseDatos))
        {
            return (estado, "error");
        }

        if (baseDatos.Status != HealthStatus.Healthy)
        {
            // CanConnectAsync devuelve Unhealthy sin excepcion cuando la base no
            // responde. Una excepcion es un fallo de configuracion o de consulta,
            // salvo un timeout (OperationCanceledException del health check o un
            // TimeoutException de la capa de red), que tambien significa "no respondio".
            var noResponde = baseDatos.Exception is null
                || baseDatos.Exception is OperationCanceledException
                || baseDatos.Exception is TimeoutException;

            return (estado, noResponde ? "unreachable" : "error");
        }

        if (informe.Entries.TryGetValue(CheckMigraciones, out var migraciones)
            && migraciones.Status != HealthStatus.Healthy)
        {
            return (estado, "schema-outdated");
        }

        return (estado, "reachable");
    }
}
