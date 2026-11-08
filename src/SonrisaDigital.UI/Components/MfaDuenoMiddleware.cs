using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Configuracion;

namespace SonrisaDigital.UI.Components;

/// <summary>
/// Un Dueño sin segundo factor activo solo puede llegar a la pantalla de configuración. Cualquier
/// otra petición se redirige (si es navegación) o se rechaza. Con el factor activo, la sesión
/// además debe haberlo superado: una cookie emitida solo con contraseña se cierra y se pide
/// volver a entrar.
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

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<Usuario> userManager,
        SignInManager<Usuario> signInManager)
    {
        if (!seguridad.Value.MfaDuenoObligatorio
            || context.User.Identity?.IsAuthenticated != true
            || !context.User.IsInRole(RolesSonrisaDigital.Dueno)
            || Path.HasExtension(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        var usuario = await userManager.GetUserAsync(context.User);
        if (usuario is null)
        {
            await next(context);
            return;
        }

        // El factor está activo pero la sesión no lo superó (cookie anterior a la
        // activación o emitida solo con contraseña). Se invalida y se pide el ingreso completo.
        if (usuario.TwoFactorEnabled && !ReclamacionesSegundoFactor.Verificada(context.User))
        {
            await signInManager.SignOutAsync();

            if (HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.Redirect("/login?error=mfa");
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Vuelve a iniciar sesión y completa el segundo factor.");
            }

            return;
        }

        if (usuario.TwoFactorEnabled || EsRutaLibre(context.Request.Path))
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
