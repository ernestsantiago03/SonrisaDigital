using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// El bloqueo de cuentas se guarda en la base: debe aplicarse con la clave correcta, sobrevivir a un
/// reinicio y poder levantarse desde la consola.
/// </summary>
public sealed class BloqueoCuentaTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private const string ClaveCorrecta = "Clave_De_Prueba_Larga_2026!";
    private const string ClaveIncorrecta = "Otra_Clave_Erronea_2026!";

    [Fact]
    public async Task CincoFallosBloqueanLaCuentaAunqueLaClaveSeaCorrecta()
    {
        await using var provider = CrearProveedor();
        using var ambito = provider.CreateScope();
        var userManager = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var signInManager = ambito.ServiceProvider.GetRequiredService<SignInManager<Usuario>>();

        var correo = await CrearUsuarioAsync(userManager);
        var usuario = await userManager.FindByEmailAsync(correo);
        Assert.NotNull(usuario);
        Assert.True(usuario.LockoutEnabled);

        for (var i = 0; i < PoliticaBloqueoCuentas.IntentosFallidosMaximos; i++)
        {
            var intento = await signInManager.CheckPasswordSignInAsync(usuario, ClaveIncorrecta, lockoutOnFailure: true);
            Assert.False(intento.Succeeded);
        }

        var conClaveCorrecta = await signInManager.CheckPasswordSignInAsync(usuario, ClaveCorrecta, lockoutOnFailure: true);
        Assert.True(conClaveCorrecta.IsLockedOut);
        Assert.False(conClaveCorrecta.Succeeded);
    }

    [Fact]
    public async Task ElBloqueoSobreviveAUnReinicioDeLaAplicacion()
    {
        string correo;
        await using (var antes = CrearProveedor())
        using (var ambito = antes.CreateScope())
        {
            var userManager = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var signInManager = ambito.ServiceProvider.GetRequiredService<SignInManager<Usuario>>();

            correo = await CrearUsuarioAsync(userManager);
            var usuario = await userManager.FindByEmailAsync(correo);
            Assert.NotNull(usuario);

            for (var i = 0; i < PoliticaBloqueoCuentas.IntentosFallidosMaximos; i++)
            {
                await signInManager.CheckPasswordSignInAsync(usuario, ClaveIncorrecta, lockoutOnFailure: true);
            }
        }

        await using var despues = CrearProveedor();
        using var nuevoAmbito = despues.CreateScope();
        var nuevoUserManager = nuevoAmbito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var nuevoSignInManager = nuevoAmbito.ServiceProvider.GetRequiredService<SignInManager<Usuario>>();

        var cuenta = await nuevoUserManager.FindByEmailAsync(correo);
        Assert.NotNull(cuenta);
        Assert.True(await nuevoUserManager.IsLockedOutAsync(cuenta));

        var resultado = await nuevoSignInManager.CheckPasswordSignInAsync(cuenta, ClaveCorrecta, lockoutOnFailure: true);
        Assert.True(resultado.IsLockedOut);
    }

    [Fact]
    public async Task DesbloquearPermiteEntrarDeNuevoConLaClaveCorrecta()
    {
        await using var provider = CrearProveedor();
        using var ambito = provider.CreateScope();
        var userManager = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var signInManager = ambito.ServiceProvider.GetRequiredService<SignInManager<Usuario>>();

        var correo = await CrearUsuarioAsync(userManager);
        var usuario = await userManager.FindByEmailAsync(correo);
        Assert.NotNull(usuario);

        for (var i = 0; i < PoliticaBloqueoCuentas.IntentosFallidosMaximos; i++)
        {
            await signInManager.CheckPasswordSignInAsync(usuario, ClaveIncorrecta, lockoutOnFailure: true);
        }

        await CuentasBloqueo.DesbloquearAsync(userManager, correo);

        var desbloqueada = await userManager.FindByEmailAsync(correo);
        Assert.NotNull(desbloqueada);
        var resultado = await signInManager.CheckPasswordSignInAsync(desbloqueada, ClaveCorrecta, lockoutOnFailure: true);
        Assert.True(resultado.Succeeded);
    }

    [Fact]
    public async Task HabilitarBloqueoCuentasActivaLasCuentasExistentes()
    {
        await using var provider = CrearProveedor();
        using var ambito = provider.CreateScope();
        var userManager = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var correo = await CrearUsuarioAsync(userManager);

        await using (var db = fixture.CreateContext())
        {
            await db.Users
                .Where(usuario => usuario.Email == correo)
                .ExecuteUpdateAsync(setters => setters.SetProperty(usuario => usuario.LockoutEnabled, false));
        }

        await using (var db = fixture.CreateContext())
        {
            await IdentitySeeder.HabilitarBloqueoCuentasAsync(db);
        }

        await using var verificacion = fixture.CreateContext();
        var habilitada = await verificacion.Users.AsNoTracking()
            .Where(usuario => usuario.Email == correo)
            .Select(usuario => usuario.LockoutEnabled)
            .SingleAsync();
        Assert.True(habilitada);
    }

    private static async Task<string> CrearUsuarioAsync(UserManager<Usuario> userManager)
    {
        var correo = $"bloqueo-{Guid.NewGuid():N}@sonrisadigital.local";
        var resultado = await userManager.CreateAsync(new Usuario
        {
            UserName = correo,
            Email = correo,
            Nombre = "Usuario de bloqueo",
            Estado = EstadoRegistro.Activo,
            EmailConfirmed = true
        }, ClaveCorrecta);

        Assert.True(resultado.Succeeded);
        return correo;
    }

    private ServiceProvider CrearProveedor()
    {
        using var contexto = fixture.CreateContext();
        var cadena = contexto.Database.GetConnectionString()
            ?? throw new InvalidOperationException("La base de pruebas no tiene cadena de conexión.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(cadena));
        services.AddSingleton<ITenantProvider>(new TenantFijo(fixture.ClinicaId));
        services.AddIdentityCore<Usuario>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                PoliticaBloqueoCuentas.Aplicar(options.Lockout);
            })
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }
}
