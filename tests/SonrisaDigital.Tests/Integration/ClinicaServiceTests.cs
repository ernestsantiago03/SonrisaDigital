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
using SonrisaDigital.Services.Clinicas;
using SonrisaDigital.Services.Clinicas.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>Panel de clínicas: alta, edición, estado y primer administrador (plan multiclínica, criterios 6 y 7).</summary>
public sealed class ClinicaServiceTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task ElDuenoCreaUnaClinicaYLaVeEnElListado()
    {
        await using var provider = await CrearProveedorAsync(new FakeAuthorizationService());
        var servicio = provider.GetRequiredService<IClinicaService>();
        var codigo = $"Clinica {Guid.NewGuid():N}"[..25];

        var id = await servicio.CrearAsync(ModeloClinica("Clinica de prueba", codigo));

        var listado = await servicio.ListarAsync();
        Assert.Contains(listado, clinica => clinica.Id == id && clinica.Codigo == codigo.ToLowerInvariant().Replace(' ', '-'));
    }

    [Fact]
    public async Task NoSePuedeRepetirElCodigoDeUnaClinica()
    {
        await using var provider = await CrearProveedorAsync(new FakeAuthorizationService());
        var servicio = provider.GetRequiredService<IClinicaService>();
        var codigo = $"dup-{Guid.NewGuid():N}"[..20];
        await servicio.CrearAsync(ModeloClinica("Original", codigo));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearAsync(ModeloClinica("Repetida", codigo)));

        Assert.Equal("Ya existe una clínica con ese código.", error.Message);
    }

    [Fact]
    public async Task ElPrimerAdministradorQuedaEnLaClinicaConSuRol()
    {
        await using var provider = await CrearProveedorAsync(new FakeAuthorizationService());
        var servicio = provider.GetRequiredService<IClinicaService>();
        var clinicaId = await servicio.CrearAsync(ModeloClinica("Con administrador", $"adm-{Guid.NewGuid():N}"[..20]));
        var correo = $"admin-{Guid.NewGuid():N}@sonrisadigital.local"[..40];

        var adminId = await servicio.CrearAdministradorAsync(clinicaId, new AdministradorClinicaFormDto
        {
            Nombre = "Administrador de prueba",
            Email = correo,
            Password = "Prueba123"
        });

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var admin = await userManager.FindByIdAsync(adminId);
        Assert.NotNull(admin);
        Assert.Equal(clinicaId, admin.ClinicaId);
        Assert.True(await userManager.IsInRoleAsync(admin, RolesSonrisaDigital.Administrador));
    }

    [Fact]
    public async Task SinPermisoDeClinicasNoSeListan()
    {
        await using var provider = await CrearProveedorAsync(new FakeAuthorizationService(true, "Clinicas.Ver"));
        var servicio = provider.GetRequiredService<IClinicaService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ListarAsync());
    }

    [Fact]
    public async Task SuspenderUnaClinicaCambiaSuEstado()
    {
        await using var provider = await CrearProveedorAsync(new FakeAuthorizationService());
        var servicio = provider.GetRequiredService<IClinicaService>();
        var id = await servicio.CrearAsync(ModeloClinica("Para suspender", $"sus-{Guid.NewGuid():N}"[..20]));

        await servicio.CambiarEstadoAsync(id, EstadoClinica.Suspendida);

        var clinica = (await servicio.ListarAsync()).Single(item => item.Id == id);
        Assert.Equal(EstadoClinica.Suspendida, clinica.Estado);
    }

    private static ClinicaFormDto ModeloClinica(string nombre, string codigo) =>
        new() { Nombre = nombre, Codigo = codigo, Correo = "contacto@clinica.local" };

    private async Task<ServiceProvider> CrearProveedorAsync(IAuthorizationService autorizacion)
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

        services.AddSingleton<ITenantProvider>(new TenantMutable { ClinicaId = fixture.ClinicaId });
        services.AddSingleton<ICurrentUserService>(TestSession.Crear());
        services.AddSingleton(autorizacion);
        services.AddSingleton<IAuditoriaService>(new FakeAuditoriaService());
        services.AddScoped<IClinicaService, ClinicaService>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();
        if (!await roleManager.RoleExistsAsync(RolesSonrisaDigital.Administrador))
        {
            await roleManager.CreateAsync(new Rol { Name = RolesSonrisaDigital.Administrador, Estado = EstadoRegistro.Activo });
        }

        return provider;
    }
}
