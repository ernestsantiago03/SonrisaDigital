using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Catalogos;

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
    IAuthorizationService authorizationService,
    ICatalogoCache catalogoCache) : IRolService
{
    /// <summary>
    /// El rol Administrador nunca pierde estos permisos: si se quitaran, nadie podria
    /// volver a entrar a "Roles y permisos" para corregirlo.
    /// </summary>
    private static readonly string[] PermisosQueAdministradorConserva = ["Roles.Ver", "Roles.Editar"];

    public async Task<IReadOnlyList<RolePermissionDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Roles.Ver", "No tienes permiso para consultar roles y permisos.");

        // El catálogo de definiciones ya vive en memoria (PermissionCatalog).
        // Lo que se consulta en SQL es la asignación de permisos de cada rol;
        // esa lista se cachea y se invalida al guardar cambios.
        return await catalogoCache.ObtenerRolesAsync(CargarRolesAsync, cancellationToken);
    }

    private async Task<IReadOnlyList<RolePermissionDto>> CargarRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

        if (roles.Count == 0)
        {
            return [];
        }

        var roleIds = roles.Select(role => role.Id).ToList();
        var permisosPorRol = (await dbContext.RolesPermisos
                .AsNoTracking()
                .Where(rolePermission => roleIds.Contains(rolePermission.RolId))
                .Select(rolePermission => new
                {
                    rolePermission.RolId,
                    Permiso = rolePermission.Permiso.Nombre
                })
                .OrderBy(item => item.Permiso)
                .ToListAsync(cancellationToken))
            .ToLookup(item => item.RolId, item => item.Permiso);

        return roles
            .Select(role => new RolePermissionDto(
                role.Id,
                role.Name ?? string.Empty,
                role.Descripcion,
                permisosPorRol[role.Id].ToList()))
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

        // Se materializa una sola vez para que un reintento no dependa de una
        // secuencia que ya se consumió en el intento anterior.
        var requestedNames = permissionNames.ToList();

        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            var role = await roleManager.FindByIdAsync(roleId)
                ?? throw new InvalidOperationException("El rol no existe.");
            var names = requestedNames.ToHashSet(StringComparer.Ordinal);

            // El rol Dueño no se modifica desde la aplicación: su alcance lo define el sistema.
            if (string.Equals(role.Name, RolesSonrisaDigital.Dueno, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("El rol Dueño no se puede modificar.");
            }

            // Los permisos del panel de clínicas solo puede tenerlos el rol Dueño.
            if (names.Any(PermissionCatalog.EsPermisoDeClinicas))
            {
                throw new InvalidOperationException("Solo el rol Dueño puede tener permisos del panel de clínicas.");
            }

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

            await auditoriaService.RegistrarAsync(
                "Modificó", "Roles y Permisos", nameof(Rol), null,
                $"Actualizó los permisos del rol {role.Name} ({permissions.Count} permiso(s))", cancellationToken);
        }, cancellationToken);

        // La transacción confirmó: la próxima consulta de roles debe reflejar
        // las casillas recién guardadas.
        catalogoCache.InvalidarRoles();
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}
