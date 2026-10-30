using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Usuarios;
using SonrisaDigital.Services.Usuarios.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// La gestión de usuarios respeta la clínica: una clínica no lista, lee, desactiva ni cuenta
/// los administradores de otra (plan multiclínica, criterios 3 y 9).
/// </summary>
public sealed class AislamientoUsuariosTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task UnaClinicaNoListaNiEditaUsuariosDeOtra()
    {
        var clinicaB = await CrearClinicaAsync();
        await using var provider = await CrearProveedorAsync();
        var tenant = provider.GetRequiredService<TenantMutable>();
        var servicio = provider.GetRequiredService<IUsuarioService>();

        tenant.ClinicaId = fixture.ClinicaId;
        var nombre = $"Usuario A {Guid.NewGuid():N}";
        await servicio.CreateAsync(ModeloUsuario(nombre));
        var idA = (await servicio.SearchPagedAsync(nombre, null)).Items.Single().Id;

        tenant.ClinicaId = clinicaB;

        Assert.Empty((await servicio.SearchPagedAsync(nombre, null)).Items);
        Assert.Null(await servicio.GetFormAsync(idA));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.SetStatusAsync(idA, EstadoRegistro.Inactivo));
    }

    [Fact]
    public async Task ElUltimoAdministradorSeCuentaPorClinica()
    {
        var clinicaB = await CrearClinicaAsync();
        await using var provider = await CrearProveedorAsync();
        var tenant = provider.GetRequiredService<TenantMutable>();
        var servicio = provider.GetRequiredService<IUsuarioService>();

        // Cada clínica tiene un único administrador; uno no debe contar como respaldo del otro.
        tenant.ClinicaId = fixture.ClinicaId;
        var nombreAdminA = $"Admin A {Guid.NewGuid():N}";
        await servicio.CreateAsync(ModeloUsuario(nombreAdminA, "Administrador"));
        var idAdminA = (await servicio.SearchPagedAsync(nombreAdminA, null)).Items.Single().Id;

        tenant.ClinicaId = clinicaB;
        await servicio.CreateAsync(ModeloUsuario($"Admin B {Guid.NewGuid():N}", "Administrador"));

        tenant.ClinicaId = fixture.ClinicaId;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SetStatusAsync(idAdminA, EstadoRegistro.Inactivo));
        Assert.Contains("al menos un administrador", error.Message);
    }

    private static UsuarioFormDto ModeloUsuario(string nombre, params string[] roles)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        return new UsuarioFormDto
        {
            Nombre = nombre,
            Email = $"aislamiento-{sufijo}@sonrisadigital.local",
            Password = "Prueba123",
            Roles = roles.ToList()
        };
    }

    private async Task<int> CrearClinicaAsync()
    {
        await using var db = new ApplicationDbContext(fixture.Options, SinClinica.Instancia);
        var clinica = new Clinica
        {
            Nombre = $"Clinica usuarios {Guid.NewGuid():N}",
            Codigo = $"usr-{Guid.NewGuid():N}"[..27]
        };
        db.Clinicas.Add(clinica);
        await db.SaveChangesAsync();
        return clinica.Id;
    }

    private async Task<ServiceProvider> CrearProveedorAsync()
    {
        await using var db = fixture.CreateContext();
        var cadenaConexion = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("La base de pruebas no tiene cadena de conexión.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(cadenaConexion));
        services.AddIdentityCore<Usuario>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton(new TenantMutable { ClinicaId = fixture.ClinicaId });
        services.AddSingleton<ITenantProvider>(sp => sp.GetRequiredService<TenantMutable>());
        services.AddSingleton<ICurrentUserService>(TestSession.Crear());
        services.AddSingleton<IAuthorizationService>(new FakeAuthorizationService());
        services.AddSingleton<IAuditoriaService>(new FakeAuditoriaService());
        services.AddScoped<IUsuarioService, UsuarioService>();

        var provider = services.BuildServiceProvider();
        await AsegurarRolesAsync(provider);
        return provider;
    }

    private static async Task AsegurarRolesAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();
        foreach (var nombre in new[] { RolesSonrisaDigital.Administrador })
        {
            if (!await roleManager.RoleExistsAsync(nombre))
            {
                await roleManager.CreateAsync(new Rol { Name = nombre, Estado = EstadoRegistro.Activo });
            }
        }
    }
}
