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
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        bool esDesarrollo)
    {
        var roleManager = services.GetRequiredService<RoleManager<Rol>>();
        var userManager = services.GetRequiredService<UserManager<Usuario>>();

        var createdRoles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleName in new[] { RolesSonrisaDigital.Administrador, RolesSonrisaDigital.Recepcionista, RolesSonrisaDigital.Odontologo, RolesSonrisaDigital.Dueno })
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

        // La cuenta global del Dueno se crea siempre que este configurada. El
        // administrador automatico solo existe en Development/E2E, donde hay una
        // clinica demo donde asignarlo; produccion arranca sin administrador.
        await SeedDuenoAsync(services, configuration, logger);

        // Fuera de Development no se crea un administrador automatico. La primera
        // clinica tampoco se crea sola: el Dueno entra, registra la clinica y luego
        // crea su administrador desde la interfaz. Asi produccion arranca limpia y
        // no depende de la clinica demo.
        if (!esDesarrollo)
        {
            if (!string.IsNullOrWhiteSpace(configuration["Authentication:AdminEmail"]))
            {
                logger.LogInformation(
                    "Authentication:AdminEmail se ignora fuera de Development. " +
                    "El administrador inicial se crea desde la interfaz del Dueno.");
            }

            return;
        }

        var email = configuration["Authentication:AdminEmail"]?.Trim();
        var password = configuration["Authentication:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Authentication:AdminEmail y Authentication:AdminPassword deben estar configuradas; no se creo el administrador inicial.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            // En Development la demo existe o la crea su propio seeder antes de este paso.
            // Si no esta, se omite el administrador en lugar de tumbar el arranque.
            var clinicaDemo = await services.GetRequiredService<ApplicationDbContext>().Clinicas
                .AsNoTracking()
                .SingleOrDefaultAsync(clinic => clinic.Codigo == ClinicaDemoSeeder.Codigo);
            if (clinicaDemo is null)
            {
                logger.LogWarning(
                    "No se creo el administrador inicial porque no existe la clinica de demostracion. " +
                    "En Development, ClinicaDemoSeeder la crea antes de este paso.");
                return;
            }

            admin = new Usuario { UserName = email, Email = email, Nombre = "Administrador", Estado = EstadoRegistro.Activo, EmailConfirmed = true, ClinicaId = clinicaDemo.Id };
            var result = await userManager.CreateAsync(admin, password);
            EnsureSucceeded(result, "crear el administrador inicial");
        }

        if (!await userManager.IsInRoleAsync(admin, RolesSonrisaDigital.Administrador))
        {
            var result = await userManager.AddToRoleAsync(admin, RolesSonrisaDigital.Administrador);
            EnsureSucceeded(result, "asignar el rol Administrador");
        }
    }

    /// <summary>
    /// Crea la cuenta del Dueño del sistema si no existe. Es global: no pertenece a ninguna clínica.
    /// Las credenciales vienen de Dueno:Email y Dueno:Password (secretos de usuario o variables de entorno).
    /// </summary>
    private static async Task SeedDuenoAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["Dueno:Email"]?.Trim();
        var password = configuration["Dueno:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Dueno:Email y Dueno:Password no estan configuradas; no se creo la cuenta del Dueno.");
            return;
        }

        var errorClave = SeguridadDueno.ValidarClave(password);
        if (errorClave is not null)
        {
            throw new InvalidOperationException($"Dueno:Password no es valida. {errorClave}");
        }

        var userManager = services.GetRequiredService<UserManager<Usuario>>();
        var dueno = await userManager.FindByEmailAsync(email);
        if (dueno is null)
        {
            dueno = new Usuario { UserName = email, Email = email, Nombre = "Dueno del sistema", Estado = EstadoRegistro.Activo, EmailConfirmed = true, ClinicaId = null };
            EnsureSucceeded(await userManager.CreateAsync(dueno, password), "crear la cuenta del Dueno");
        }
        else if (dueno.ClinicaId is not null)
        {
            // Una cuenta de clinica nunca puede convertirse en Dueno por configuracion.
            throw new InvalidOperationException(
                "Dueno:Email pertenece a un usuario de una clinica. Configura un correo de cuenta global para el Dueno.");
        }

        if (!await userManager.IsInRoleAsync(dueno, RolesSonrisaDigital.Dueno))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(dueno, RolesSonrisaDigital.Dueno), "asignar el rol Dueno");
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