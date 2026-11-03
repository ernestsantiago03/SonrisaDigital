using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Configuracion;

namespace SonrisaDigital.UI.Components;

/// <summary>
/// Un Dueño sin segundo factor activo solo puede llegar a la pantalla de configuración. Cualquier
/// otra petición se redirige (si es navegación) o se rechaza.
/// </summary>
public sealed class MfaDuenoMiddleware(RequestDelegate next, IOptions<SeguridadOptions> seguridad)
{
    private static readonly string[] RutasLibres =
    [
        "/seguridad/mfa",
        "/login",
        "/account/",
        "/acceso-denegado",
        "/404",
        "/error",
        "/health",
        "/_"
    ];

    public async Task InvokeAsync(HttpContext context, UserManager<Usuario> userManager)
    {
        if (!seguridad.Value.MfaDuenoObligatorio
            || context.User.Identity?.IsAuthenticated != true
            || !context.User.IsInRole(RolesSonrisaDigital.Dueno)
            || EsRutaLibre(context.Request.Path)
            || Path.HasExtension(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        var usuario = await userManager.GetUserAsync(context.User);
        if (usuario?.TwoFactorEnabled == true)
        {
            await next(context);
            return;
        }

        if (HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.Redirect("/seguridad/mfa");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Debes activar el segundo factor antes de continuar.");
    }

    private static bool EsRutaLibre(PathString ruta) =>
        RutasLibres.Any(prefijo => ruta.Value?.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) == true);
}
