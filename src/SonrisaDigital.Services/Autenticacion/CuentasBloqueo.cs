using Microsoft.AspNetCore.Identity;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Desbloqueo manual de cuentas. Lo usa el Dueño desde la consola del servidor cuando alguien
/// queda bloqueado antes de que termine el plazo.
/// </summary>
public static class CuentasBloqueo
{
    public static async Task DesbloquearAsync(UserManager<Usuario> userManager, string correo)
    {
        var usuario = await userManager.FindByEmailAsync(correo.Trim())
            ?? throw new InvalidOperationException("No existe un usuario con ese correo.");

        EnsureSucceeded(await userManager.SetLockoutEnabledAsync(usuario, true));
        EnsureSucceeded(await userManager.SetLockoutEndDateAsync(usuario, null));
        EnsureSucceeded(await userManager.ResetAccessFailedCountAsync(usuario));
    }

    private static void EnsureSucceeded(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", resultado.Errors.Select(error => error.Description)));
        }
    }
}
