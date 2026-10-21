using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.UI.Components;

/// <summary>
/// Revalida la sesión de los circuitos Blazor abiertos.
/// La cookie se valida en cada petición HTTP, pero un circuito ya iniciado no
/// vuelve a pasar por ese pipeline; este proveedor comprueba el estado del usuario
/// y su SecurityStamp para cerrar la sesión cuando cambian sus permisos o estado.
/// </summary>
public sealed class RevalidatingIdentityAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ICurrentUserService currentUserService)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private readonly IdentityOptions identityOptions = optionsAccessor.Value;
    private readonly ICurrentUserService currentUserService = currentUserService;

    /// <summary>
    /// Intervalo máximo para detectar cambios en una sesión ya abierta.
    /// Un valor corto reduce la ventana en la que un usuario desactivado sigue viendo la UI.
    /// </summary>
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Comprueba que el usuario siga activo y que el SecurityStamp de la cookie
    /// coincida con el de la base de datos.
    /// </summary>
    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var isValid = await ValidateSecurityStampAsync(userManager, authenticationState.User);
        if (!isValid)
        {
            // El circuito puede seguir vivo unos instantes hasta que el layout se
            // vuelva a renderizar; se limpia el principal para que los Services no
            // sigan autorizando con los claims anteriores.
            currentUserService.Establecer(null, null, null);
        }

        return isValid;
    }

    private async Task<bool> ValidateSecurityStampAsync(UserManager<Usuario> userManager, ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || user.Estado != EstadoRegistro.Activo)
        {
            return false;
        }

        // Si el almacén no usa SecurityStamp no hay nada más que comparar.
        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var claimStamp = principal.FindFirst(identityOptions.ClaimsIdentity.SecurityStampClaimType)?.Value;
        var currentStamp = await userManager.GetSecurityStampAsync(user);

        // Sin ambos valores no se puede demostrar que la sesión siga siendo válida.
        return !string.IsNullOrEmpty(claimStamp)
            && !string.IsNullOrEmpty(currentStamp)
            && string.Equals(claimStamp, currentStamp, StringComparison.Ordinal);
    }
}
