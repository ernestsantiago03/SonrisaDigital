using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Clinicas;
using SonrisaDigital.Services.Clinicas.DTOs;
using SonrisaDigital.Services.Usuarios;
using SonrisaDigital.Services.Usuarios.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Arranque limpio de producción: una base nueva no conserva la clínica demo y
/// fuera de Development el seeding solo crea la cuenta global del Dueño.
/// </summary>
public sealed class SeedFase2Tests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public void UnaBaseNuevaNoConservaLaClinicaDemoDespuesDeMigrar()
    {
        Assert.Null(fixture.DemoIdTrasMigrar);
    }

    [Fact]
    public async Task ElSeederDeDemoCreaLaPruebaSinFechasYEsIdempotente()
    {
        await using var db = fixture.CreateContext();
        var demo = await db.Clinicas
            .AsNoTracking()
            .SingleAsync(clinic => clinic.Codigo == ClinicaDemoSeeder.Codigo);

        Assert.Equal(EstadoClinica.Activa, demo.Estado);
        Assert.True(demo.EnPrueba);
        Assert.Equal(ClinicaDemoSeeder.PlanPrueba, demo.Plan);
        Assert.Null(demo.FechaPrimerIngreso);
        Assert.Null(demo.FechaVencimiento);
        Assert.Equal(ClinicaDemoSeeder.LimiteUsuarios, demo.LimiteUsuarios);

        var creada = await ClinicaDemoSeeder.SeedAsync(db);

        Assert.False(creada);
        Assert.Equal(1, await db.Clinicas.CountAsync(clinic => clinic.Codigo == ClinicaDemoSeeder.Codigo));
    }

    [Fact]
    public async Task EnProduccionElSeederNoCreaAdministradorNiDependeDeLaDemo()
    {
        var correoAdmin = $"prod-admin-{Guid.NewGuid():N}@sonrisadigital.local";
        var correoDueno = $"prod-dueno-{Guid.NewGuid():N}@sonrisadigital.local";
        var configuracion = CrearConfiguracion(correoDueno, correoAdmin);

        await using var provider = await CrearProveedorAsync();

        await IdentitySeeder.SeedAsync(
            provider,
            configuracion,
            NullLogger.Instance,
            esDesarrollo: false);

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        Assert.Null(await userManager.FindByEmailAsync(correoAdmin));

        var dueno = await userManager.FindByEmailAsync(correoDueno);
        Assert.NotNull(dueno);
        Assert.Null(dueno.ClinicaId);
        Assert.True(await userManager.IsInRoleAsync(dueno, RolesSonrisaDigital.Dueno));
    }

    [Fact]
    public async Task EnDesarrolloElSeederCreaElAdministradorDentroDeLaDemo()
    {
        var correoAdmin = $"dev-admin-{Guid.NewGuid():N}@sonrisadigital.local";
        var correoDueno = $"dev-dueno-{Guid.NewGuid():N}@sonrisadigital.local";
        var configuracion = CrearConfiguracion(correoDueno, correoAdmin);

        await using var provider = await CrearProveedorAsync();

        await IdentitySeeder.SeedAsync(
            provider,
            configuracion,
            NullLogger.Instance,
            esDesarrollo: true);

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var admin = await userManager.FindByEmailAsync(correoAdmin);
        Assert.NotNull(admin);
        Assert.Equal(fixture.ClinicaId, admin.ClinicaId);
        Assert.True(await userManager.IsInRoleAsync(admin, RolesSonrisaDigital.Administrador));
    }

    [Fact]
    public async Task ElDuenoCreaLaClinicaYSuAdministradorDesdeLaInterfaz()
    {
        // Producción: el seeder solo deja la cuenta global del Dueño.
        var correoDueno = $"flujo-dueno-{Guid.NewGuid():N}@sonrisadigital.local";
        var configuracion = CrearConfiguracion(correoDueno, string.Empty);

        await using var provider = await CrearProveedorAsync();

        await IdentitySeeder.SeedAsync(
            provider,
            configuracion,
            NullLogger.Instance,
            esDesarrollo: false);

        using var scope = provider.CreateScope();
        var clinicas = scope.ServiceProvider.GetRequiredService<IClinicaService>();
        var codigo = $"nueva-{Guid.NewGuid():N}"[..20];

        // Paso 1: el Dueño registra la clínica desde el panel.
        var clinicaId = await clinicas.CrearAsync(new ClinicaFormDto
        {
            Nombre = "Clínica Nueva",
            Codigo = codigo,
            Correo = "nueva@clinica.local"
        });

        // Paso 2: selecciona esa clínica y crea su administrador.
        provider.GetRequiredService<TenantMutable>().ClinicaId = clinicaId;
        var usuarios = scope.ServiceProvider.GetRequiredService<IUsuarioService>();
        var correoAdmin = $"flujo-admin-{Guid.NewGuid():N}@sonrisadigital.local";

        await usuarios.CreateAsync(new UsuarioFormDto
        {
            Nombre = "Administrador de la Clínica Nueva",
            Email = correoAdmin,
            Password = "Prueba123",
            Estado = EstadoRegistro.Activo,
            Roles = [RolesSonrisaDigital.Administrador]
        });

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var admin = await userManager.FindByEmailAsync(correoAdmin);

        Assert.NotNull(admin);
        Assert.Equal(clinicaId, admin.ClinicaId);
        Assert.True(await userManager.IsInRoleAsync(admin, RolesSonrisaDigital.Administrador));
    }

    private static IConfiguration CrearConfiguracion(string correoDueno, string correoAdmin) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dueno:Email"] = correoDueno,
                ["Dueno:Password"] = "Prueba123",
                ["Authentication:AdminEmail"] = correoAdmin,
                ["Authentication:AdminPassword"] = "Prueba123"
            })
            .Build();

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
        services.AddSingleton<ITenantProvider>(provider => provider.GetRequiredService<TenantMutable>());
        services.AddSingleton<ICurrentUserService>(TestSession.Crear());
        services.AddSingleton<IAuthorizationService>(new FakeAuthorizationService());
        services.AddSingleton<IAuditoriaService>(new FakeAuditoriaService());
        services.AddScoped<IClinicaService, ClinicaService>();
        services.AddScoped<IUsuarioService, UsuarioService>();

        return services.BuildServiceProvider();
    }
}
