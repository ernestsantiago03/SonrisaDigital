using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.Services.Autorizacion;

public interface IRolService
{
    Task<IReadOnlyList<RolePermissionDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionDefinition>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    Task UpdatePermissionsAsync(string roleId, IEnumerable<string> permissionNames, CancellationToken cancellationToken = default);
}

public sealed record RolePermissionDto(string Id, string Nombre, string? Descripcion, IReadOnlyList<string> Permisos);

public sealed class RolService(
    ApplicationDbContext dbContext,
    RoleManager<Rol> roleManager,
    UserManager<Usuario> userManager,
    IAuditoriaService auditoriaService,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IRolService
{
    /// <summary>
    /// El rol Administrador nunca pierde estos permisos: si se quitaran, nadie podria
    /// volver a entrar a "Roles y permisos" para corregirlo.
    /// </summary>
    private static readonly string[] PermisosQueAdministradorConserva = ["Roles.Ver", "Roles.Editar"];

    public async Task<IReadOnlyList<RolePermissionDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Roles.Ver", "No tienes permiso para consultar roles y permisos.");

        var roles = await roleManager.Roles.OrderBy(role => role.Name).ToListAsync(cancellationToken);
        return roles.Select(role => new RolePermissionDto(
            role.Id,
            role.Name ?? string.Empty,
            role.Descripcion,
            dbContext.RolesPermisos
                .Where(rolePermission => rolePermission.RolId == role.Id)
                .Select(rolePermission => rolePermission.Permiso.Nombre)
                .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<PermissionDefinition>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Roles.Ver", "No tienes permiso para consultar roles y permisos.");

        return PermissionCatalog.All;
    }

    public async Task UpdatePermissionsAsync(string roleId, IEnumerable<string> permissionNames, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Roles.Editar", "No tienes permiso para asignar permisos a roles.");

        var role = await roleManager.FindByIdAsync(roleId)
            ?? throw new InvalidOperationException("El rol no existe.");
        var names = permissionNames.ToHashSet(StringComparer.Ordinal);

        if (string.Equals(role.Name, RolesSonrisaDigital.Administrador, StringComparison.Ordinal))
        {
            names.UnionWith(PermisosQueAdministradorConserva);
        }

        var permissions = await dbContext.Permisos
            .Where(permission => names.Contains(permission.Nombre))
            .ToListAsync(cancellationToken);

        if (permissions.Count != names.Count)
        {
            throw new InvalidOperationException("Uno o más permisos no existen.");
        }

        var current = await dbContext.RolesPermisos
            .Where(rolePermission => rolePermission.RolId == role.Id)
            .ToListAsync(cancellationToken);
        var currentNames = await dbContext.RolesPermisos
            .Where(rolePermission => rolePermission.RolId == role.Id)
            .Select(rolePermission => rolePermission.Permiso.Nombre)
            .ToListAsync(cancellationToken);
        var permissionsChanged = !currentNames.ToHashSet(StringComparer.Ordinal).SetEquals(names);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.RolesPermisos.RemoveRange(current);
            await dbContext.RolesPermisos.AddRangeAsync(
                permissions.Select(permission => new RolPermiso { RolId = role.Id, PermisoId = permission.Id }),
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Los permisos efectivos viven en los claims de la sesión. Al cambiar el rol,
            // se actualiza el SecurityStamp de sus usuarios para que las sesiones abiertas
            // se invaliden y vuelvan a calcular los permisos.
            if (permissionsChanged)
            {
                var usuariosDelRol = await userManager.GetUsersInRoleAsync(role.Name ?? string.Empty);
                foreach (var usuario in usuariosDelRol)
                {
                    EnsureSucceeded(await userManager.UpdateSecurityStampAsync(usuario));
                }
            }

            await transaction.CommitAsync(cancellationToken);

            await auditoriaService.RegistrarAsync(
                "Modificó", "Roles y Permisos", nameof(Rol), null,
                $"Actualizó los permisos del rol {role.Name} ({permissions.Count} permiso(s))", cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
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
