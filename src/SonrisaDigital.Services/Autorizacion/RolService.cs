using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Auditoria;

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
    IAuditoriaService auditoriaService) : IRolService
{
    /// <summary>
    /// El rol Administrador nunca pierde estos permisos: si se quitaran, nadie podria
    /// volver a entrar a "Roles y permisos" para corregirlo.
    /// </summary>
    private static readonly string[] PermisosQueAdministradorConserva = ["Roles.Ver", "Roles.Editar"];

    public async Task<IReadOnlyList<RolePermissionDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
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

    public Task<IReadOnlyList<PermissionDefinition>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(PermissionCatalog.All);

    public async Task UpdatePermissionsAsync(string roleId, IEnumerable<string> permissionNames, CancellationToken cancellationToken = default)
    {
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
        dbContext.RolesPermisos.RemoveRange(current);
        await dbContext.RolesPermisos.AddRangeAsync(
            permissions.Select(permission => new RolPermiso { RolId = role.Id, PermisoId = permission.Id }),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Modificó", "Roles y Permisos", nameof(Rol), null,
            $"Actualizó los permisos del rol {role.Name} ({permissions.Count} permiso(s))", cancellationToken);
    }
}