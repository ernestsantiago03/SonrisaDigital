using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autorizacion;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Alta y baja de cuentas Dueño. No hay pantalla para esto: se usa desde la consola del servidor,
/// así que quien opera la cuenta necesita acceso al equipo.
/// </summary>
public static class CuentasDueno
{
    public static async Task<string> CrearAsync(
        UserManager<Usuario> userManager,
        RoleManager<Rol> roleManager,
        string correo,
        string nombre,
        string clave)
    {
        correo = correo.Trim();
        if (!MailAddress.TryCreate(correo, out _))
        {
            throw new InvalidOperationException("El correo no es válido.");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new InvalidOperationException("El nombre es obligatorio.");
        }

        var errorClave = SeguridadDueno.ValidarClave(clave);
        if (errorClave is not null)
        {
            throw new InvalidOperationException(errorClave);
        }

        if (await userManager.FindByEmailAsync(correo) is not null)
        {
            throw new InvalidOperationException("Ya existe un usuario con ese correo.");
        }

        if (!await roleManager.RoleExistsAsync(RolesSonrisaDigital.Dueno))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new Rol { Name = RolesSonrisaDigital.Dueno, Estado = EstadoRegistro.Activo }));
        }

        var usuario = new Usuario
        {
            UserName = correo,
            Email = correo,
            Nombre = nombre.Trim(),
            Estado = EstadoRegistro.Activo,
            EmailConfirmed = true,
            ClinicaId = null
        };

        EnsureSucceeded(await userManager.CreateAsync(usuario, clave));

        var rol = await userManager.AddToRoleAsync(usuario, RolesSonrisaDigital.Dueno);
        if (!rol.Succeeded)
        {
            await userManager.DeleteAsync(usuario);
            EnsureSucceeded(rol);
        }

        return usuario.Id;
    }

    public static async Task DesactivarAsync(UserManager<Usuario> userManager, string correo)
    {
        var usuario = await userManager.FindByEmailAsync(correo.Trim())
            ?? throw new InvalidOperationException("No existe un usuario con ese correo.");

        if (!await userManager.IsInRoleAsync(usuario, RolesSonrisaDigital.Dueno))
        {
            throw new InvalidOperationException("Ese usuario no tiene el rol Dueño.");
        }

        if (usuario.Estado != EstadoRegistro.Activo)
        {
            return;
        }

        var duenosActivos = (await userManager.GetUsersInRoleAsync(RolesSonrisaDigital.Dueno))
            .Count(dueno => dueno.Estado == EstadoRegistro.Activo);

        if (!SeguridadDueno.PuedeDesactivarse(duenosActivos))
        {
            throw new InvalidOperationException(
                $"Debe quedar al menos {SeguridadDueno.DuenosActivosMinimos} Dueño activo. Crea otro antes de desactivar este.");
        }

        usuario.Estado = EstadoRegistro.Inactivo;
        EnsureSucceeded(await userManager.UpdateAsync(usuario));

        // Cierra las sesiones que siguieran abiertas con esta cuenta.
        EnsureSucceeded(await userManager.UpdateSecurityStampAsync(usuario));
    }

    /// <summary>
    /// Quita el segundo factor y los códigos de recuperación de un Dueño que perdió el acceso a su app.
    /// Al siguiente inicio de sesión tendrá que volver a activarlo. La contraseña no cambia.
    /// </summary>
    public static async Task RestablecerSegundoFactorAsync(UserManager<Usuario> userManager, string correo)
    {
        var usuario = await userManager.FindByEmailAsync(correo.Trim())
            ?? throw new InvalidOperationException("No existe un usuario con ese correo.");

        if (!await userManager.IsInRoleAsync(usuario, RolesSonrisaDigital.Dueno))
        {
            throw new InvalidOperationException("Ese usuario no tiene el rol Dueño.");
        }

        EnsureSucceeded(await userManager.SetTwoFactorEnabledAsync(usuario, false));
        await userManager.ResetAuthenticatorKeyAsync(usuario);

        // Cero códigos nuevos: los anteriores dejan de servir.
        await userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, 0);

        // Cierra las sesiones abiertas con la configuración anterior.
        EnsureSucceeded(await userManager.UpdateSecurityStampAsync(usuario));
    }

    private static void EnsureSucceeded(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", resultado.Errors.Select(error => error.Description)));
        }
    }
}
