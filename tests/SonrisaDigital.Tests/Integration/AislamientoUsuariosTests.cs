using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task NadieMasPuedeAsignarElRolDueno()
    {
        await using var provider = await CrearProveedorAsync();
        var servicio = provider.GetRequiredService<IUsuarioService>();
        provider.GetRequiredService<TenantMutable>().ClinicaId = fixture.ClinicaId;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CreateAsync(ModeloUsuario($"Intento dueno {Guid.NewGuid():N}", RolesSonrisaDigital.Dueno)));
    }

    [Fact]
    public async Task NoSeCreaUsuarioSinClinicaActiva()
    {
        await using var provider = await CrearProveedorAsync();
        var tenant = provider.GetRequiredService<TenantMutable>();
        var servicio = provider.GetRequiredService<IUsuarioService>();
        tenant.ClinicaId = null;

        var modelo = ModeloUsuario($"Sin clinica {Guid.NewGuid():N}");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CreateAsync(modelo));

        Assert.Contains("clínica activa", error.Message, StringComparison.OrdinalIgnoreCase);

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        Assert.Null(await userManager.FindByEmailAsync(modelo.Email));
    }

    [Fact]
    public async Task NoSeCreaUsuarioEnClinicaSuspendida()
    {
        var clinicaSuspendida = await CrearClinicaAsync(EstadoClinica.Suspendida);
        await using var provider = await CrearProveedorAsync();
        var tenant = provider.GetRequiredService<TenantMutable>();
        var servicio = provider.GetRequiredService<IUsuarioService>();
        tenant.ClinicaId = clinicaSuspendida;

        var modelo = ModeloUsuario($"Suspendida {Guid.NewGuid():N}");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CreateAsync(modelo));

        Assert.Contains("suspendida", error.Message, StringComparison.OrdinalIgnoreCase);

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        Assert.Null(await userManager.FindByEmailAsync(modelo.Email));
    }

    [Fact]
    public async Task ObtieneLaClinicaActivaDeLaSesion()
    {
        await using var provider = await CrearProveedorAsync();
        var tenant = provider.GetRequiredService<TenantMutable>();
        var servicio = provider.GetRequiredService<IUsuarioService>();
        tenant.ClinicaId = fixture.ClinicaId;

        var clinica = await servicio.GetClinicaActualAsync();

        Assert.NotNull(clinica);
        Assert.Equal(fixture.ClinicaId, clinica.Id);
        Assert.Equal(EstadoClinica.Activa, clinica.Estado);
        Assert.False(string.IsNullOrWhiteSpace(clinica.Nombre));

        tenant.ClinicaId = null;
        Assert.Null(await servicio.GetClinicaActualAsync());
    }

    [Fact]
    public async Task LaAuditoriaDelAltaGuardaLaClinica()
    {
        await using var provider = await CrearProveedorAsync();
        var tenant = provider.GetRequiredService<TenantMutable>();
        var servicio = provider.GetRequiredService<IUsuarioService>();
        tenant.ClinicaId = fixture.ClinicaId;

        await servicio.CreateAsync(ModeloUsuario($"Auditoria {Guid.NewGuid():N}"));

        var auditoria = Assert.IsType<FakeAuditoriaService>(provider.GetRequiredService<IAuditoriaService>());
        var registro = Assert.Single(auditoria.Registros);
        Assert.Equal("Creó", registro.Accion);
        Assert.Equal(fixture.ClinicaId, registro.ClinicaId);
    }

    [Fact]
    public async Task ElSeederCreaLaCuentaDelDuenoSinClinica()
    {
        await using var provider = await CrearProveedorAsync();
        var correo = $"dueno-{Guid.NewGuid():N}@sonrisadigital.local"[..40];
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Dueno:Email"] = correo, ["Dueno:Password"] = "PruebaDueno12345678" })
            .Build();

        await IdentitySeeder.SeedAsync(provider, configuracion, NullLogger.Instance, esDesarrollo: true);

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var dueno = await userManager.FindByEmailAsync(correo);
        Assert.NotNull(dueno);
        Assert.Null(dueno.ClinicaId);
        Assert.True(await userManager.IsInRoleAsync(dueno, RolesSonrisaDigital.Dueno));
    }

    [Fact]
    public async Task ElSeederRechazaLaClaveDelDuenoMenorDe16CaracteresSinCrearLaCuenta()
    {
        await using var provider = await CrearProveedorAsync();
        var correo = $"dueno-{Guid.NewGuid():N}@sonrisadigital.local"[..40];
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Dueno:Email"] = correo, ["Dueno:Password"] = "Prueba123" })
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            IdentitySeeder.SeedAsync(provider, configuracion, NullLogger.Instance, esDesarrollo: true));

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        Assert.Null(await userManager.FindByEmailAsync(correo));
    }

    [Fact]
    public async Task ElSeederNoAsignaElRolDuenoAUnaCuentaDeClinica()
    {
        await using var provider = await CrearProveedorAsync();
        var correo = $"clinica{Guid.NewGuid():N}@sonrisadigital.local";

        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var usuario = new Usuario
            {
                UserName = correo,
                Email = correo,
                Nombre = "Usuario de clinica",
                Estado = EstadoRegistro.Activo,
                EmailConfirmed = true,
                ClinicaId = fixture.ClinicaId
            };
            var creado = await userManager.CreateAsync(usuario, "PruebaClinica12345678");
            Assert.True(creado.Succeeded, string.Join("; ", creado.Errors.Select(error => error.Description)));
        }

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Dueno:Email"] = correo, ["Dueno:Password"] = "PruebaDueno12345678" })
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            IdentitySeeder.SeedAsync(provider, configuracion, NullLogger.Instance, esDesarrollo: true));

        using var verificacion = provider.CreateScope();
        var manager = verificacion.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var cuenta = await manager.FindByEmailAsync(correo);
        Assert.NotNull(cuenta);
        Assert.False(await manager.IsInRoleAsync(cuenta, RolesSonrisaDigital.Dueno));
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

    private async Task<int> CrearClinicaAsync(EstadoClinica estado = EstadoClinica.Activa)
    {
        await using var db = new ApplicationDbContext(fixture.Options, SinClinica.Instancia);
        var clinica = new Clinica
        {
            Nombre = $"Clinica usuarios {Guid.NewGuid():N}",
            Codigo = $"usr-{Guid.NewGuid():N}"[..27],
            Estado = estado
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
