using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Odontologos.DTOs;
using SonrisaDigital.Services.Usuarios.DTOs;

namespace SonrisaDigital.Services.Usuarios;

public sealed class UsuarioService(
    UserManager<Usuario> userManager,
    RoleManager<Rol> roleManager,
    ApplicationDbContext dbContext,
    IAuditoriaService auditoriaService) : IUsuarioService
{
    public async Task<IReadOnlyList<UsuarioListItemDto>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default)
    {
        var query = userManager.Users.AsNoTracking();

        if (estado.HasValue)
        {
            query = query.Where(user => user.Estado == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(user => user.Nombre.Contains(term) || (user.Email != null && user.Email.Contains(term)));
        }

        var users = await query.OrderBy(user => user.Nombre).ToListAsync(cancellationToken);
        var result = new List<UsuarioListItemDto>(users.Count);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UsuarioListItemDto(user.Id, user.Nombre, user.Email ?? string.Empty, user.Estado, user.FechaCreacion, roles.ToList()));
        }

        return result;
    }

    public async Task<UsuarioFormDto?> GetFormAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return null;

        return new UsuarioFormDto
        {
            Id = user.Id,
            Nombre = user.Nombre,
            Email = user.Email ?? string.Empty,
            Estado = user.Estado,
            Roles = (await userManager.GetRolesAsync(user)).ToList(),
            PerfilOdontologo = await GetOdontologoFormAsync(user.Id, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken cancellationToken = default) =>
        await roleManager.Roles
            .Where(role => role.Estado == EstadoRegistro.Activo)
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);

    public async Task CreateAsync(UsuarioFormDto model, CancellationToken cancellationToken = default)
    {
        ValidateOdontologoProfile(model);
        var email = model.Email.Trim();
        var user = new Usuario
        {
            UserName = email,
            Email = email,
            Nombre = model.Nombre.Trim(),
            Estado = EstadoRegistro.Activo,
            FechaCreacion = DateTime.UtcNow,
            EmailConfirmed = true
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await userManager.CreateAsync(user, model.Password ?? string.Empty);
            EnsureSucceeded(result);
            await ReplaceRolesAsync(user, model.Roles, cancellationToken);
            await SaveOdontologoProfileAsync(user.Id, model, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await auditoriaService.RegistrarAsync(
                "Creó", "Usuarios", nameof(Usuario), null,
                $"Creó el usuario: {await ObtenerNombreCompletoAsync(user, cancellationToken)}", cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateAsync(UsuarioFormDto model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Id)) throw new InvalidOperationException("El usuario no existe.");

        var user = await userManager.FindByIdAsync(model.Id);
        if (user is null) throw new InvalidOperationException("El usuario no existe.");
        ValidateOdontologoProfile(model);
        await EnsureNotLastActiveAdminAsync(user, model.Estado, model.Roles, cancellationToken);

        var estadoAnterior = user.Estado;
        var emailAnterior = user.Email;
        var email = model.Email.Trim();
        user.Nombre = model.Nombre.Trim();
        user.Estado = model.Estado;
        user.Email = email;
        user.UserName = email;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await userManager.UpdateAsync(user);
            EnsureSucceeded(result);
            var rolesChanged = await ReplaceRolesAsync(user, model.Roles, cancellationToken);
            await SaveOdontologoProfileAsync(user.Id, model, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                EnsureSucceeded(await userManager.ResetPasswordAsync(user, token, model.Password));
            }

            // El SecurityStamp invalida las sesiones ya abiertas cuando cambió algo
            // relevante para la autorización: estado, correo, roles o contraseña.
            var debeActualizarStamp = estadoAnterior != user.Estado
                || !string.Equals(emailAnterior, email, StringComparison.OrdinalIgnoreCase)
                || rolesChanged
                || !string.IsNullOrWhiteSpace(model.Password);

            if (debeActualizarStamp)
            {
                EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));
            }

            await transaction.CommitAsync(cancellationToken);

            await auditoriaService.RegistrarAsync(
                "Modificó", "Usuarios", nameof(Usuario), null,
                $"Modificó el usuario: {await ObtenerNombreCompletoAsync(user, cancellationToken)}", cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SetStatusAsync(string id, EstadoRegistro estado, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(id) ?? throw new InvalidOperationException("El usuario no existe.");
        await EnsureNotLastActiveAdminAsync(user, estado, await userManager.GetRolesAsync(user), cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            user.Estado = estado;
            EnsureSucceeded(await userManager.UpdateAsync(user));

            // Al desactivar o reactivar se fuerza la invalidación de las sesiones
            // abiertas; el proveedor revalidante de Blazor lo detecta en su próximo ciclo.
            EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var verbo = estado == EstadoRegistro.Activo ? "Activó" : "Desactivó";
        await auditoriaService.RegistrarAsync(
            verbo, "Usuarios", nameof(Usuario), null,
            $"{verbo} el usuario: {await ObtenerNombreCompletoAsync(user, cancellationToken)}", cancellationToken);
    }

    public async Task ResetPasswordAsync(string id, string password, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(id) ?? throw new InvalidOperationException("El usuario no existe.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            EnsureSucceeded(await userManager.ResetPasswordAsync(user, token, password));

            // Una contraseña nueva debe invalidar cualquier sesión que siguiera abierta.
            EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await auditoriaService.RegistrarAsync(
            "Restableció", "Usuarios", nameof(Usuario), null,
            $"Restableció la contraseña del usuario: {await ObtenerNombreCompletoAsync(user, cancellationToken)}", cancellationToken);
    }

    private async Task<bool> ReplaceRolesAsync(Usuario user, IEnumerable<string> roleNames, CancellationToken cancellationToken)
    {
        var requestedRoles = roleNames.Where(role => !string.IsNullOrWhiteSpace(role)).Distinct().ToArray();
        foreach (var role in requestedRoles)
        {
            if (!await roleManager.RoleExistsAsync(role)) throw new InvalidOperationException($"El rol '{role}' no existe.");
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.ToHashSet(StringComparer.Ordinal).SetEquals(requestedRoles))
        {
            return false;
        }

        EnsureSucceeded(await userManager.RemoveFromRolesAsync(user, currentRoles));
        if (requestedRoles.Length > 0) EnsureSucceeded(await userManager.AddToRolesAsync(user, requestedRoles));
        return true;
    }

    private async Task<OdontologoFormDto?> GetOdontologoFormAsync(string userId, CancellationToken cancellationToken)
    {
        var odontologo = await dbContext.Odontologos.AsNoTracking().SingleOrDefaultAsync(item => item.UsuarioId == userId, cancellationToken);
        return odontologo is null ? null : new OdontologoFormDto
        {
            Id = odontologo.Id,
            Estado = odontologo.Estado,
            Nombres = odontologo.Nombres,
            Apellidos = odontologo.Apellidos,
            NumeroLicencia = odontologo.NumeroLicencia ?? string.Empty,
            Especialidad = odontologo.Especialidad,
            Telefono = odontologo.Telefono,
            Correo = odontologo.Correo
        };
    }

    private async Task SaveOdontologoProfileAsync(string userId, UsuarioFormDto model, CancellationToken cancellationToken)
    {
        var isDentist = model.Roles.Contains(RolesSonrisaDigital.Odontologo, StringComparer.Ordinal);
        var existing = await dbContext.Odontologos.SingleOrDefaultAsync(item => item.UsuarioId == userId, cancellationToken);

        if (!isDentist)
        {
            if (existing is not null) existing.UsuarioId = null;
            return;
        }

        var profile = model.PerfilOdontologo!;
        var licenseInUse = await dbContext.Odontologos.AnyAsync(item => item.NumeroLicencia == profile.NumeroLicencia && item.UsuarioId != userId, cancellationToken);
        if (licenseInUse) throw new InvalidOperationException("El número de licencia ya está asociado a otro odontólogo.");

        if (existing is null)
        {
            existing = new Odontologo { UsuarioId = userId };
            dbContext.Odontologos.Add(existing);
        }

        existing.Nombres = profile.Nombres.Trim();
        existing.Apellidos = profile.Apellidos.Trim();
        existing.NumeroLicencia = profile.NumeroLicencia.Trim();
        existing.Especialidad = Normalize(profile.Especialidad);
        existing.Telefono = Normalize(profile.Telefono);
        existing.Correo = Normalize(profile.Correo);
        existing.Estado = profile.Estado;
    }

    private async Task<string> ObtenerNombreCompletoAsync(Usuario user, CancellationToken cancellationToken)
    {
        var perfil = await dbContext.Odontologos.AsNoTracking()
            .Where(odontologo => odontologo.UsuarioId == user.Id)
            .Select(odontologo => new { odontologo.Nombres, odontologo.Apellidos })
            .SingleOrDefaultAsync(cancellationToken);

        var nombrePerfil = perfil is null ? string.Empty : $"{perfil.Nombres} {perfil.Apellidos}".Trim();
        return string.IsNullOrWhiteSpace(nombrePerfil) ? user.Nombre : nombrePerfil;
    }

    private static void ValidateOdontologoProfile(UsuarioFormDto model)
    {
        if (model.Roles.Contains(RolesSonrisaDigital.Odontologo, StringComparer.Ordinal) && model.PerfilOdontologo is null)
        {
            throw new InvalidOperationException("Completa el perfil profesional del odontólogo.");
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Impide que una operación deje al sistema sin ningún administrador activo:
    /// ya sea desactivando al último o quitándole el rol "Administrador".
    /// </summary>
    private async Task EnsureNotLastActiveAdminAsync(
        Usuario user,
        EstadoRegistro nuevoEstado,
        IEnumerable<string> nuevosRoles,
        CancellationToken cancellationToken)
    {
        var eraAdminActivo = user.Estado == EstadoRegistro.Activo
                             && await userManager.IsInRoleAsync(user, RolesSonrisaDigital.Administrador);
        if (!eraAdminActivo) return;

        var seguiraAdminActivo = nuevoEstado == EstadoRegistro.Activo
                                 && nuevosRoles.Contains(RolesSonrisaDigital.Administrador, StringComparer.Ordinal);
        if (seguiraAdminActivo) return;

        var admins = await userManager.GetUsersInRoleAsync(RolesSonrisaDigital.Administrador);
        var otrosAdminsActivos = admins.Count(admin => admin.Id != user.Id && admin.Estado == EstadoRegistro.Activo);
        if (otrosAdminsActivos == 0)
        {
            throw new InvalidOperationException(
                "El sistema debe conservar al menos un administrador activo. " +
                "Asigna el rol Administrador a otro usuario antes de desactivar o cambiar el rol de este.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}