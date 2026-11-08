using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Revalida el sello de seguridad y, al regenerar el principal, conserva la marca del
/// segundo factor. La revalidación se ejecuta en cada petición (ValidationInterval = 0) y
/// reconstruye los claims desde la base de datos; sin esto la sesión perdería la marca y
/// el usuario tendría que repetir el segundo factor en cada navegación.
/// </summary>
public sealed class SecurityStampValidatorConSegundoFactor(
    IOptions<SecurityStampValidatorOptions> options,
    SignInManager<Usuario> signInManager,
    ILoggerFactory loggerFactory)
    : SecurityStampValidator<Usuario>(options, signInManager, loggerFactory)
{
    protected override async Task SecurityStampVerified(Usuario user, CookieValidatePrincipalContext context)
    {
        var marca = context.Principal?.FindFirst(ReclamacionesSegundoFactor.Tipo)?.Value;

        await base.SecurityStampVerified(user, context);

        if (marca is null
            || context.Principal?.Identity is not ClaimsIdentity identidad
            || identidad.HasClaim(ReclamacionesSegundoFactor.Tipo, marca))
        {
            return;
        }

        identidad.AddClaim(new Claim(ReclamacionesSegundoFactor.Tipo, marca));
    }
}
