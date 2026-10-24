using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.Services.Autorizacion;

/// <summary>
/// Helper compartido para validar permisos en la capa de servicios (Fase 1 - Parte E).
/// Centraliza el patrón "fail closed": sin principal o sin permiso se lanza
/// UnauthorizedAccessException para que la UI muestre una denegación controlada.
/// </summary>
public static class AutorizacionHelper
{
    public static async Task AsegurarPermisoAsync(
        this IAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        string policy,
        string mensaje)
    {
        var principal = currentUserService.Principal;
        if (principal is null)
        {
            // Sin usuario autenticado se deniega por defecto (fail closed).
            throw new UnauthorizedAccessException(mensaje);
        }

        // Se usa el mismo motor de políticas que AuthorizeView y [Authorize].
        var result = await authorizationService.AuthorizeAsync(principal, policy);
        if (!result.Succeeded)
        {
            throw new UnauthorizedAccessException(mensaje);
        }
    }
}