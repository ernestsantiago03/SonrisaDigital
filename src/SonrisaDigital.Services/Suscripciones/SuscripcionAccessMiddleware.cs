using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Suscripciones;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Autorizacion;

namespace SonrisaDigital.Services.Suscripciones;

/// <summary>
/// Bloquea toda petición autenticada de una clínica vencida o suspendida.
///
/// El middleware anterior solo actuaba sobre navegaciones HTML, así que las
/// descargas de archivos, los PDF y el handshake de Blazor seguían funcionando
/// después del vencimiento. Este se ejecuta para cualquier verbo y decide la
/// respuesta según el tipo de cliente: redirección al login para navegación HTML
/// y 403 JSON para API, archivos y SignalR.
/// </summary>
public sealed class SuscripcionAccessMiddleware(
    RequestDelegate next,
    ILogger<SuscripcionAccessMiddleware> logger)
{
    private static readonly string[] PrefijosPublicos =
    [
        "/login",
        "/account/login",
        "/account/logout",
        "/acceso-denegado",
        "/404",
        "/Error",
        "/health",
        "/_framework",
        "/_content",
        "/css",
        "/js",
        "/lib",
        "/images",
        "/app.css",
        "/favicon.ico"
    ];

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true || EsRutaPublica(context.Request.Path))
        {
            await next(context);
            return;
        }

        var clinicaIdClaim = context.User.FindFirst(ReclamacionesClinica.ClinicaId);
        if (clinicaIdClaim is null || !int.TryParse(clinicaIdClaim.Value, out var clinicaId))
        {
            // El Dueño del sistema no pertenece a ninguna clínica y debe poder
            // facturar o reactivar aunque la clínica no esté seleccionada.
            if (context.User.IsInRole(RolesSonrisaDigital.Dueno))
            {
                await next(context);
                return;
            }

            await DenegarAsync(context, AccesoClinica.Suspendida);
            return;
        }

        var clinica = await dbContext.Clinicas
            .AsNoTracking()
            .Where(item => item.Id == clinicaId)
            .Select(item => new
            {
                item.Estado,
                item.EnPrueba,
                item.FechaPrimerIngreso,
                item.FechaVencimiento
            })
            .FirstOrDefaultAsync(context.RequestAborted);

        // Una clínica inexistente se trata como suspendida: cierre por defecto.
        var acceso = clinica is null
            ? AccesoClinica.Suspendida
            : ReglaSuscripcion.Evaluar(
                clinica.Estado,
                clinica.EnPrueba,
                clinica.FechaPrimerIngreso,
                clinica.FechaVencimiento,
                ReglaSuscripcion.Hoy());

        if (acceso is not (AccesoClinica.Suspendida or AccesoClinica.Vencida))
        {
            await next(context);
            return;
        }

        await DenegarAsync(context, acceso);
    }

    /// <summary>Rutas que no dependen de la suscripción de una clínica.</summary>
    public static bool EsRutaPublica(PathString path) =>
        PrefijosPublicos.Any(prefijo =>
            path.StartsWithSegments(prefijo, StringComparison.OrdinalIgnoreCase));

    /// <summary>Una navegación de navegador puede mostrar la página de login; una API debe recibir 403.</summary>
    public static bool EsNavegacion(HttpRequest request) =>
        request.Headers.Accept.Any(valor =>
            valor?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>
    /// Rutas que deben responder con 403 aunque el navegador las abra como
    /// navegación: descargas, reportes y el canal de Blazor.
    /// </summary>
    public static bool EsRutaDeDatos(PathString path) =>
        path.StartsWithSegments("/archivos", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/reportes", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase);

    private async Task DenegarAsync(HttpContext context, AccesoClinica acceso)
    {
        var vencida = acceso == AccesoClinica.Vencida;

        logger.LogWarning(
            "Acceso bloqueado por suscripción para la clínica del usuario {Usuario} en {Path}.",
            context.User.Identity?.Name ?? "desconocido",
            context.Request.Path);

        await context.SignOutAsync(IdentityConstants.ApplicationScheme);

        if (!EsRutaDeDatos(context.Request.Path) && EsNavegacion(context.Request))
        {
            context.Response.Redirect(vencida
                ? "/login?error=clinic-expired"
                : "/login?error=clinic");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(vencida
            ? "{\"error\":\"clinic-expired\"}"
            : "{\"error\":\"clinic-suspended\"}");
    }
}
