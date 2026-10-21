using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autorizacion;

namespace SonrisaDigital.Services.Autenticacion;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<Rol>>();
        var userManager = services.GetRequiredService<UserManager<Usuario>>();

        var createdRoles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleName in new[] { RolesSonrisaDigital.Administrador, RolesSonrisaDigital.Recepcionista, RolesSonrisaDigital.Odontologo })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new Rol { Name = roleName, Estado = EstadoRegistro.Activo });
                EnsureSucceeded(result, $"crear el rol {roleName}");
                createdRoles.Add(roleName);
            }
        }

        var permissions = new Dictionary<string, Permiso>();
        foreach (var definition in PermissionCatalog.All)
        {
            var permission = await services.GetRequiredService<ApplicationDbContext>().Permisos
                .SingleOrDefaultAsync(item => item.Nombre == definition.Nombre);
            if (permission is null)
            {
                permission = new Permiso
                {
                    Nombre = definition.Nombre,
                    Modulo = definition.Modulo,
                    Descripcion = definition.Descripcion
                };
                services.GetRequiredService<ApplicationDbContext>().Permisos.Add(permission);
            }

            permissions[permission.Nombre] = permission;
        }

        await services.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();

        // Los permisos por defecto solo se asignan cuando el rol se acaba de crear.
        // Si el rol ya existia se respeta lo configurado en "Roles y permisos"; de lo
        // contrario cada arranque reviviria los permisos que el administrador quito.
        foreach (var rolePermissions in PermissionCatalog.DefaultRolePermissions)
        {
            if (!createdRoles.Contains(rolePermissions.Key)) continue;

            var role = await roleManager.FindByNameAsync(rolePermissions.Key);
            if (role is null) continue;

            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            dbContext.RolesPermisos.AddRange(rolePermissions.Value
                .Select(permission => new RolPermiso
                {
                    RolId = role.Id,
                    PermisoId = permissions[permission].Id
                }));

            await dbContext.SaveChangesAsync();
        }

        var email = configuration["Authentication:AdminEmail"] ?? "admin@sonrisadigital.com";
        var password = configuration["Authentication:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Authentication:AdminPassword no esta configurada; no se creo el administrador inicial.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new Usuario { UserName = email, Email = email, Nombre = "Administrador", Estado = EstadoRegistro.Activo, EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, password);
            EnsureSucceeded(result, "crear el administrador inicial");
        }

        if (!await userManager.IsInRoleAsync(admin, RolesSonrisaDigital.Administrador))
        {
            var result = await userManager.AddToRoleAsync(admin, RolesSonrisaDigital.Administrador);
            EnsureSucceeded(result, "asignar el rol Administrador");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"No se pudo {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}