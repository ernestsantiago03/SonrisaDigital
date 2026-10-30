using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Validacion;
using SonrisaDigital.Services.Odontologos.DTOs;
using SonrisaDigital.Services.Usuarios.DTOs;

using SonrisaDigital.Data.Tenancy;

namespace SonrisaDigital.Services.Usuarios;

public sealed class UsuarioService(
    UserManager<Usuario> userManager,
    RoleManager<Rol> roleManager,
    ApplicationDbContext dbContext,
    IAuditoriaService auditoriaService,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService,
    ITenantProvider tenant) : IUsuarioService
{
    // Límite de filas por página que acepta el servicio, aunque la pantalla pida más.
    private const int MaxPageSize = 50;

    /// <summary>
    /// Página de usuarios para la tabla principal. Primero cuenta en SQL y después
    /// trae solo los usuarios de la página; los roles se consultan en un solo viaje.
    /// </summary>
    public async Task<PagedResult<UsuarioListItemDto>> SearchPagedAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        // Autorización en servicio.
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Usuarios.Ver", "No tienes permiso para consultar usuarios.");

        // Normalizar antes de contar para que el total de páginas sea correcto.
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

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

        // CountAsync se traduce a COUNT en SQL; no se traen todos los usuarios.
        var total = await query.CountAsync(cancellationToken);

        // Si la página quedó fuera de rango después de un filtro, se baja a la última válida.
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        var users = await query
            .OrderBy(user => user.Nombre)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return new PagedResult<UsuarioListItemDto>([], total, page, pageSize);
        }

        // Los roles se traen solo de los usuarios de la página, en una sola consulta.
        var userIds = users.Select(user => user.Id).ToList();
        var rolesPorUsuario = (await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                orderby role.Name
                select new { userRole.UserId, RoleName = role.Name })
            .ToListAsync(cancellationToken))
            .ToLookup(item => item.UserId, item => item.RoleName ?? string.Empty);

        var items = users
            .Select(user => new UsuarioListItemDto(
                user.Id,
                user.Nombre,
                user.Email ?? string.Empty,
                user.Estado,
                user.FechaCreacion,
                rolesPorUsuario[user.Id].ToList()))
            .ToList();

        return new PagedResult<UsuarioListItemDto>(items, total, page, pageSize);
    }

    public async Task<UsuarioFormDto?> GetFormAsync(string id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Usuarios.Ver", "No tienes permiso para consultar usuarios.");

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

    public async Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Usuarios.Ver", "No tienes permiso para consultar usuarios.");

        return await roleManager.Roles
            .Where(role => role.Estado == EstadoRegistro.Activo)
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(UsuarioFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Usuarios.Crear", "No tienes permiso para crear usuarios.");

        ValidadorDto.PrepararYValidar(model);
        ValidateOdontologoProfile(model);
        var email = model.Email.Trim();

        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            // El usuario se crea dentro de la transacción para que, si la
            // estrategia de reintentos vuelve a ejecutar el bloque, parta de
            // una entidad nueva y no reutilice una que ya quedó marcada.
            var user = new Usuario
            {
                UserName = email,
                Email = email,
                Nombre = model.Nombre.Trim(),
                Estado = EstadoRegistro.Activo,
                FechaCreacion = DateTime.UtcNow,
                EmailConfirmed = true,
                // Un usuario nuevo queda en la clínica de quien lo crea.
                ClinicaId = tenant.ClinicaId ?? throw new InvalidOperationException("No hay clínica en la sesión.")
            };

            var result = await userManager.CreateAsync(user, model.Password ?? string.Empty);
            EnsureSucceeded(result);
            await ReplaceRolesAsync(user, model.Roles, cancellationToken);
            await SaveOdontologoProfileAsync(user.Id, model, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            // La auditoría se guarda dentro de la misma transacción: si falla,
            // el alta completa se deshace y no queda un usuario sin trazabilidad.
            await auditoriaService.RegistrarAsync(
                "Creó", "Usuarios", nameof(Usuario), null,
                $"Creó el usuario: {await ObtenerNombreCompletoAsync(user, cancellationToken)}", cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdateAsync(UsuarioFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Usuarios.Editar", "No tienes permiso para editar usuarios.");

        ValidadorDto.PrepararYValidar(model);

        if (string.IsNullOrWhiteSpace(model.Id)) throw new InvalidOperationException("El usuario no existe.");
        ValidateOdontologoProfile(model);

        // Serializable evita que dos ediciones simultáneas puedan dejar al
        // sistema sin administrador activo al comprobar y guardar a la vez.
        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            var user = await userManager.FindByIdAsync(model.Id);
            if (user is null) throw new InvalidOperationException("El usuario no existe.");

            // Cambiar el estado es una operación sensible. Ocultar el control en
            // la UI no basta: una llamada directa también debe exigir el permiso.
            if (model.Estado != user.Estado)
            {
                await authorizationService.AsegurarPermisoAsync(
                    currentUserService,
                    "Usuarios.Desactivar",
                    "No tienes permiso para activar o desactivar usuarios.");
            }

            await EnsureNotLastActiveAdminAsync(user, model.Estado, model.Roles, cancellationToken);

            var estadoAnterior = user.Estado;
            var emailAnterior = user.Email;
            var email = model.Email.Trim();
            user.Nombre = model.Nombre.Trim();
            if (model.Estado != user.Estado)
            {
                user.Estado = model.Estado;
            }
            user.Email = email;
            user.UserName = email;

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

            await auditoriaService.RegistrarAsync(
                "Modificó", "Usuarios", nameof(Usuario), null,
                $"Modificó el usuario: {await ObtenerNombreCompletoAsync(user, cancellationToken)}", cancellationToken);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task SetStatusAsync(string id, EstadoRegistro estado, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Usuarios.Desactivar", "No tienes permiso para activar o desactivar usuarios.");

        // La comprobación del último administrador y el cambio de estado deben
        // formar una sola operación serializable para soportar concurrencia.
        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            var user = await userManager.FindByIdAsync(id) ?? throw new InvalidOperationException("El usuario no existe.");
            await EnsureNotLastActiveAdminAsync(user, estado, await userManager.GetRolesAsync(user), cancellationToken);

            user.Estado = estado;
            EnsureSucceeded(await userManager.UpdateAsync(user));

            // Al desactivar o reactivar se fuerza la invalidación de las sesiones
            // abiertas; el proveedor revalidante de Blazor lo detecta en su próximo ciclo.
            EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));

            var verbo = estado == EstadoRegistro.Activo ? "Activó" : "Desactivó";
            await auditoriaService.RegistrarAsync(
                verbo, "Usuarios", nameof(Usuario), null,
                $"{verbo} el usuario: {await ObtenerNombreCompletoAsync(user, cancellationToken)}", cancellationToken);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task ResetPasswordAsync(string id, string password, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Usuarios.Editar", "No tienes permiso para restablecer contraseñas.");

        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            var user = await userManager.FindByIdAsync(id) ?? throw new InvalidOperationException("El usuario no existe.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            EnsureSucceeded(await userManager.ResetPasswordAsync(user, token, password));

            // Una contraseña nueva debe invalidar cualquier sesión que siguiera abierta.
            EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));

            await auditoriaService.RegistrarAsync(
                "Restableció", "Usuarios", nameof(Usuario), null,
                $"Restableció la contraseña del usuario: {await ObtenerNombreCompletoAsync(user, cancellationToken)}", cancellationToken);
        }, cancellationToken);
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
            if (existing is not null)
            {
                // Al quitar el rol se conserva la licencia y el historial, pero el
                // perfil queda huérfano e inactivo para que no siga disponible.
                existing.UsuarioId = null;
                existing.Estado = EstadoRegistro.Inactivo;
            }
            return;
        }

        var profile = model.PerfilOdontologo!;
        var numeroLicencia = profile.NumeroLicencia.Trim();

        // Los perfiles huérfanos no bloquean su licencia: al recuperar el rol se
        // reutiliza el mismo perfil para conservar el historial clínico.
        var licenseInUse = await dbContext.Odontologos.AnyAsync(item =>
            item.NumeroLicencia == numeroLicencia
            && item.UsuarioId != null
            && item.UsuarioId != userId, cancellationToken);
        if (licenseInUse) throw new InvalidOperationException("El número de licencia ya está asociado a otro odontólogo.");

        if (existing is null)
        {
            // Si existe un perfil huérfano con la misma licencia, se reutiliza en
            // lugar de crear otro y se reactiva al recuperar el rol.
            existing = await dbContext.Odontologos.FirstOrDefaultAsync(
                item => item.UsuarioId == null && item.NumeroLicencia == numeroLicencia,
                cancellationToken);
            if (existing is not null)
            {
                existing.Estado = EstadoRegistro.Activo;
            }
        }

        if (existing is null)
        {
            existing = new Odontologo { UsuarioId = userId, Estado = EstadoRegistro.Activo };
            dbContext.Odontologos.Add(existing);
        }
        else
        {
            existing.UsuarioId = userId;
        }

        existing.Nombres = profile.Nombres.Trim();
        existing.Apellidos = profile.Apellidos.Trim();
        existing.NumeroLicencia = numeroLicencia;
        existing.Especialidad = Normalize(profile.Especialidad);
        existing.Telefono = Normalize(profile.Telefono);
        existing.Correo = Normalize(profile.Correo);
        // No se copia profile.Estado: el estado del perfil solo cambia con
        // Odontologos.Desactivar, no al editar el usuario.
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