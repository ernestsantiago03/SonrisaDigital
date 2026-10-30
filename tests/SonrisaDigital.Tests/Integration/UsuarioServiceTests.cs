using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Odontologos;
using SonrisaDigital.Services.Odontologos.DTOs;
using SonrisaDigital.Services.Usuarios;
using SonrisaDigital.Services.Usuarios.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Pruebas de integración de usuarios y odontólogos. Cubren la separación entre
/// editar y cambiar el estado, la conservación del perfil al quitar el rol de
/// odontólogo y la protección del último administrador ante operaciones simultáneas.
/// </summary>
public sealed class UsuarioServiceTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task EditarUsuarioSinPermisoDesactivarNoCambiaEstado()
    {
        await using var provider = await CrearProveedorAsync("Usuarios.Desactivar");
        var usuario = await CrearUsuarioAsync(provider, "Recepcionista", RolesSonrisaDigital.Recepcionista);
        using var scope = provider.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();

        var modelo = Modelo(
            usuario,
            EstadoRegistro.Inactivo,
            [RolesSonrisaDigital.Recepcionista]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.UpdateAsync(modelo));

        await using var db = fixture.CreateContext();
        var actual = await db.Users.FindAsync(usuario.Id);
        Assert.NotNull(actual);
        Assert.Equal(EstadoRegistro.Activo, actual.Estado);
    }

    [Fact]
    public async Task EditarUsuarioConPermisoDesactivarCambiaEstado()
    {
        await using var provider = await CrearProveedorAsync();
        var usuario = await CrearUsuarioAsync(provider, "Recepcionista", RolesSonrisaDigital.Recepcionista);
        using var scope = provider.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();

        await servicio.UpdateAsync(Modelo(
            usuario,
            EstadoRegistro.Inactivo,
            [RolesSonrisaDigital.Recepcionista]));

        await using var db = fixture.CreateContext();
        var actual = await db.Users.FindAsync(usuario.Id);
        Assert.NotNull(actual);
        Assert.Equal(EstadoRegistro.Inactivo, actual.Estado);
    }

    [Fact]
    public async Task EditarUsuarioSinCambiarEstadoNoExigeDesactivar()
    {
        await using var provider = await CrearProveedorAsync("Usuarios.Desactivar");
        var usuario = await CrearUsuarioAsync(provider, "Recepcionista", RolesSonrisaDigital.Recepcionista);
        using var scope = provider.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();

        var modelo = Modelo(
            usuario,
            EstadoRegistro.Activo,
            [RolesSonrisaDigital.Recepcionista]);
        modelo.Nombre = "Nombre actualizado";

        await servicio.UpdateAsync(modelo);

        await using var db = fixture.CreateContext();
        var actual = await db.Users.FindAsync(usuario.Id);
        Assert.NotNull(actual);
        Assert.Equal("Nombre actualizado", actual.Nombre);
        Assert.Equal(EstadoRegistro.Activo, actual.Estado);
    }

    [Fact]
    public async Task QuitarRolOdontologoDejaPerfilHuerfanoEInactivo()
    {
        await using var provider = await CrearProveedorAsync();
        var usuario = await CrearUsuarioAsync(provider, "Odontólogo", RolesSonrisaDigital.Odontologo);
        var licencia = NuevaLicencia();
        var perfil = await CrearPerfilAsync(provider, usuario.Id, licencia);
        using var scope = provider.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();

        var modelo = Modelo(
            usuario,
            EstadoRegistro.Activo,
            [RolesSonrisaDigital.Recepcionista]);

        await servicio.UpdateAsync(modelo);

        await using var db = fixture.CreateContext();
        var actual = await db.Odontologos.SingleAsync(odontologo => odontologo.Id == perfil.Id);
        Assert.Null(actual.UsuarioId);
        Assert.Equal(EstadoRegistro.Inactivo, actual.Estado);
        Assert.Equal(licencia, actual.NumeroLicencia);
    }

    [Fact]
    public async Task ReasignarRolOdontologoReutilizaPerfilHuerfano()
    {
        await using var provider = await CrearProveedorAsync();
        var usuario = await CrearUsuarioAsync(provider, "Odontólogo", RolesSonrisaDigital.Odontologo);
        var licencia = NuevaLicencia();
        var perfil = await CrearPerfilAsync(provider, usuario.Id, licencia);

        using (var scope = provider.CreateScope())
        {
            var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();
            await servicio.UpdateAsync(Modelo(
                usuario,
                EstadoRegistro.Activo,
                [RolesSonrisaDigital.Recepcionista]));
        }

        using (var scope = provider.CreateScope())
        {
            var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();
            await servicio.UpdateAsync(Modelo(
                usuario,
                EstadoRegistro.Activo,
                [RolesSonrisaDigital.Odontologo],
                Perfil(licencia)));
        }

        await using var db = fixture.CreateContext();
        var actual = await db.Odontologos.SingleAsync(odontologo => odontologo.NumeroLicencia == licencia);
        Assert.Equal(perfil.Id, actual.Id);
        Assert.Equal(usuario.Id, actual.UsuarioId);
        Assert.Equal(EstadoRegistro.Activo, actual.Estado);
        Assert.Equal(1, await db.Odontologos.CountAsync(odontologo => odontologo.NumeroLicencia == licencia));
    }

    [Fact]
    public async Task LicenciaDeOtroUsuarioActivoSigueBloqueada()
    {
        await using var provider = await CrearProveedorAsync();
        var primerUsuario = await CrearUsuarioAsync(provider, "Primer odontólogo", RolesSonrisaDigital.Odontologo);
        var licencia = NuevaLicencia();
        await CrearPerfilAsync(provider, primerUsuario.Id, licencia);
        var segundoUsuario = await CrearUsuarioAsync(provider, "Segundo odontólogo");
        using var scope = provider.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();

        var modelo = Modelo(
            segundoUsuario,
            EstadoRegistro.Activo,
            [RolesSonrisaDigital.Odontologo],
            Perfil(licencia));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.UpdateAsync(modelo));

        Assert.Contains("licencia", excepcion.Message, StringComparison.OrdinalIgnoreCase);

        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Odontologos.CountAsync(odontologo => odontologo.NumeroLicencia == licencia));
    }

    [Fact]
    public async Task DosDesactivacionesConcurrentesNoDejanSistemaSinAdministrador()
    {
        // La barrera retiene la autorización hasta que las dos operaciones están
        // listas, de modo que las transacciones se solapen de verdad.
        var barrera = new BarrierAuthorizationService();
        await using var provider1 = await CrearProveedorConAutorizacionAsync(barrera);
        await using var provider2 = await CrearProveedorConAutorizacionAsync(barrera);
        var administrador1 = await CrearUsuarioAsync(provider1, "Administrador 1", RolesSonrisaDigital.Administrador);
        var administrador2 = await CrearUsuarioAsync(provider2, "Administrador 2", RolesSonrisaDigital.Administrador);

        using var scope1 = provider1.CreateScope();
        using var scope2 = provider2.CreateScope();
        var servicio1 = scope1.ServiceProvider.GetRequiredService<IUsuarioService>();
        var servicio2 = scope2.ServiceProvider.GetRequiredService<IUsuarioService>();

        var resultados = await Task.WhenAll(
                EjecutarAsync(() => servicio1.SetStatusAsync(administrador1.Id, EstadoRegistro.Inactivo)),
                EjecutarAsync(() => servicio2.SetStatusAsync(administrador2.Id, EstadoRegistro.Inactivo)))
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1, resultados.Count(resultado => resultado.Exito));
        Assert.Equal(1, resultados.Count(resultado => !resultado.Exito));

        await using var db = fixture.CreateContext();
        var administradoresActivos = await db.Users.CountAsync(usuario =>
            (usuario.Id == administrador1.Id || usuario.Id == administrador2.Id)
            && usuario.Estado == EstadoRegistro.Activo);
        Assert.Equal(1, administradoresActivos);
    }

    [Fact]
    public async Task EditarOdontologoSinPermisoDesactivarNoCambiaEstado()
    {
        await using var db = fixture.CreateContext();
        var odontologo = new Odontologo
        {
            Nombres = "Odontólogo",
            Apellidos = "Prueba",
            NumeroLicencia = NuevaLicencia(),
            Especialidad = "Odontología general",
            Estado = EstadoRegistro.Activo
        };
        db.Odontologos.Add(odontologo);
        await db.SaveChangesAsync();

        var servicio = new OdontologoService(
            new OdontologoRepository(db),
            new FakeAuditoriaService(),
            TestSession.Crear(),
            new FakeAuthorizationService(true, "Odontologos.Desactivar"));

        var modelo = new OdontologoFormDto
        {
            Id = odontologo.Id,
            Estado = EstadoRegistro.Inactivo,
            Nombres = odontologo.Nombres,
            Apellidos = odontologo.Apellidos,
            NumeroLicencia = odontologo.NumeroLicencia!,
            Especialidad = odontologo.Especialidad,
            Telefono = odontologo.Telefono,
            Correo = odontologo.Correo
        };

        await servicio.UpdateAsync(modelo);

        await using var comprobacion = fixture.CreateContext();
        var actual = await comprobacion.Odontologos.FindAsync(odontologo.Id);
        Assert.NotNull(actual);
        Assert.Equal(EstadoRegistro.Activo, actual.Estado);
    }

    private Task<ServiceProvider> CrearProveedorAsync(params string[] permisosDenegados) =>
        CrearProveedorConAutorizacionAsync(new FakeAuthorizationService(true, permisosDenegados));

    private async Task<ServiceProvider> CrearProveedorConAutorizacionAsync(IAuthorizationService autorizacion)
    {
        await using var db = fixture.CreateContext();
        var cadenaConexion = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("La base de pruebas no tiene cadena de conexión.");

        var services = new ServiceCollection();
        services.AddLogging();
        // Identity necesita protección de datos para generar tokens de seguridad.
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(cadenaConexion, sqlServer =>
                // Se reintenta el deadlock de SQL Server sin alargar demasiado
                // la prueba; la regla de negocio se valida en el segundo intento.
                sqlServer.EnableRetryOnFailure(2, TimeSpan.FromMilliseconds(50), null)));

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

        services.AddSingleton<ICurrentUserService>(TestSession.Crear());
        services.AddSingleton(autorizacion);
        services.AddSingleton<IAuditoriaService>(new FakeAuditoriaService());
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IOdontologoRepository, OdontologoRepository>();
        services.AddScoped<IOdontologoService, OdontologoService>();

        var provider = services.BuildServiceProvider();
        await AsegurarRolesAsync(provider);
        return provider;
    }

    private static async Task AsegurarRolesAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();

        foreach (var nombre in new[]
                 {
                     RolesSonrisaDigital.Administrador,
                     RolesSonrisaDigital.Recepcionista,
                     RolesSonrisaDigital.Odontologo
                 })
        {
            if (await roleManager.RoleExistsAsync(nombre))
            {
                continue;
            }

            AsegurarResultado(await roleManager.CreateAsync(new Rol
            {
                Name = nombre,
                Estado = EstadoRegistro.Activo
            }));
        }
    }

    private static async Task<Usuario> CrearUsuarioAsync(
        IServiceProvider provider,
        string nombre,
        params string[] roles)
    {
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var email = $"usuario-{sufijo}@sonrisadigital.local";

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            Nombre = nombre,
            Estado = EstadoRegistro.Activo,
            EmailConfirmed = true
        };

        AsegurarResultado(await userManager.CreateAsync(usuario, "Prueba123"));
        if (roles.Length > 0)
        {
            AsegurarResultado(await userManager.AddToRolesAsync(usuario, roles));
        }

        return usuario;
    }

    private static async Task<Odontologo> CrearPerfilAsync(
        IServiceProvider provider,
        string usuarioId,
        string licencia)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var perfil = new Odontologo
        {
            UsuarioId = usuarioId,
            Nombres = "Profesional",
            Apellidos = "Prueba",
            NumeroLicencia = licencia,
            Especialidad = "Odontología general",
            Estado = EstadoRegistro.Activo
        };

        db.Odontologos.Add(perfil);
        await db.SaveChangesAsync();
        return perfil;
    }

    private static UsuarioFormDto Modelo(
        Usuario usuario,
        EstadoRegistro estado,
        IReadOnlyList<string> roles,
        OdontologoFormDto? perfil = null) => new()
    {
        Id = usuario.Id,
        Nombre = usuario.Nombre,
        Email = usuario.Email ?? string.Empty,
        Estado = estado,
        Roles = roles.ToList(),
        PerfilOdontologo = perfil
    };

    private static OdontologoFormDto Perfil(string licencia) => new()
    {
        Estado = EstadoRegistro.Activo,
        Nombres = "Profesional",
        Apellidos = "Prueba",
        NumeroLicencia = licencia,
        Especialidad = "Odontología general"
    };

    private static string NuevaLicencia() => $"LIC-{Guid.NewGuid():N}";

    private static async Task<(bool Exito, Exception? Error)> EjecutarAsync(Func<Task> operacion)
    {
        try
        {
            await operacion();
            return (true, null);
        }
        catch (Exception excepcion)
        {
            return (false, excepcion);
        }
    }

    private static void AsegurarResultado(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(" ", resultado.Errors.Select(error => error.Description)));
        }
    }

    /// <summary>
    /// Autoriza siempre, pero retiene la primera llamada hasta que llega la segunda.
    /// Así las dos desactivaciones entran juntas a la zona transaccional.
    /// </summary>
    private sealed class BarrierAuthorizationService : IAuthorizationService
    {
        private readonly TaskCompletionSource ambosLlegaron =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int llegadas;

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements) =>
            EsperarAsync();

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName) =>
            EsperarAsync();

        private async Task<AuthorizationResult> EsperarAsync()
        {
            if (Interlocked.Increment(ref llegadas) == 2)
            {
                ambosLlegaron.TrySetResult();
            }

            await ambosLlegaron.Task;
            return AuthorizationResult.Success();
        }
    }
}
