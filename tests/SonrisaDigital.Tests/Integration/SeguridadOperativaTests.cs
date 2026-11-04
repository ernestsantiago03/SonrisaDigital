using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Tests.Support;
using RegistroAuditoria = SonrisaDigital.Data.Entities.RegistroAuditoria;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Protecciones operativas: la bitácora de auditoría no se modifica y el Dueño puede recuperar su
/// segundo factor desde la consola.
/// </summary>
public sealed class SeguridadOperativaTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task LaAuditoriaNoSePuedeModificarNiBorrar()
    {
        await using var db = fixture.CreateContext();
        var usuario = await TestData.AsegurarUsuarioAsync(db);
        db.RegistrosAuditoria.Add(new RegistroAuditoria
        {
            UsuarioId = usuario.Id,
            Accion = "Prueba",
            Modulo = "Pruebas",
            Entidad = "Prueba",
            Descripcion = "Registro que no debe cambiar"
        });
        await db.SaveChangesAsync();
        var id = await db.RegistrosAuditoria
            .Where(item => item.Descripcion == "Registro que no debe cambiar")
            .Select(item => item.Id)
            .OrderByDescending(valor => valor)
            .FirstAsync();

        var modificacion = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE RegistrosAuditoria SET Accion = N'Alterada' WHERE Id = {id}"));
        Assert.Contains("no se puede modificar ni borrar", modificacion.Message, StringComparison.Ordinal);

        var borrado = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM RegistrosAuditoria WHERE Id = {id}"));
        Assert.Contains("no se puede modificar ni borrar", borrado.Message, StringComparison.Ordinal);

        var registro = await db.RegistrosAuditoria.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal("Prueba", registro.Accion);
    }

    [Fact]
    public async Task RestablecerSegundoFactorQuitaElFactorYLosCodigos()
    {
        await using var provider = CrearProveedorIdentity();
        using var ambito = provider.CreateScope();
        var userManager = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var roleManager = ambito.ServiceProvider.GetRequiredService<RoleManager<Rol>>();

        if (!await roleManager.RoleExistsAsync(RolesSonrisaDigital.Dueno))
        {
            await roleManager.CreateAsync(new Rol { Name = RolesSonrisaDigital.Dueno, Estado = EstadoRegistro.Activo });
        }

        var correo = $"dueno-{Guid.NewGuid():N}@sonrisadigital.local";
        var usuario = new Usuario
        {
            UserName = correo,
            Email = correo,
            Nombre = "Dueño de prueba",
            Estado = EstadoRegistro.Activo,
            EmailConfirmed = true
        };
        Assert.True((await userManager.CreateAsync(usuario, "Clave_De_Prueba_Larga_2026!")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(usuario, RolesSonrisaDigital.Dueno)).Succeeded);
        Assert.True((await userManager.SetTwoFactorEnabledAsync(usuario, true)).Succeeded);
        await userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, 10);

        await CuentasDueno.RestablecerSegundoFactorAsync(userManager, correo);

        var actualizado = await userManager.FindByEmailAsync(correo);
        Assert.NotNull(actualizado);
        Assert.False(actualizado.TwoFactorEnabled);
        Assert.Equal(0, await userManager.CountRecoveryCodesAsync(actualizado));
    }

    [Fact]
    public async Task RestablecerSegundoFactorRechazaUnUsuarioQueNoEsDueno()
    {
        await using var provider = CrearProveedorIdentity();
        using var ambito = provider.CreateScope();
        var userManager = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var correo = $"otro-{Guid.NewGuid():N}@sonrisadigital.local";
        await userManager.CreateAsync(new Usuario
        {
            UserName = correo,
            Email = correo,
            Nombre = "Recepcionista de prueba",
            Estado = EstadoRegistro.Activo,
            EmailConfirmed = true
        }, "Clave_De_Prueba_Larga_2026!");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CuentasDueno.RestablecerSegundoFactorAsync(userManager, correo));
        Assert.Contains("no tiene el rol Dueño", error.Message);
    }

    private ServiceProvider CrearProveedorIdentity()
    {
        var cadena = fixture.CreateContext().Database.GetConnectionString()
            ?? throw new InvalidOperationException("La base de pruebas no tiene cadena de conexión.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(cadena));
        services.AddSingleton<ITenantProvider>(new TenantFijo(fixture.ClinicaId));
        services.AddIdentityCore<Usuario>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }
}
