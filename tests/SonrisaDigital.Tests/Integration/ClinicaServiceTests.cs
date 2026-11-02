using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Clinicas;
using SonrisaDigital.Services.Clinicas.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>Panel de clínicas: alta, edición y estado (plan multiclínica, criterios 6 y 7).</summary>
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
    public async Task SinPermisoDeClinicasNoSeListan()
    {
        await using var provider = await CrearProveedorAsync(new FakeAuthorizationService(true, "Clinicas.Ver"));
        var servicio = provider.GetRequiredService<IClinicaService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ListarAsync());
    }

    [Fact]
    public async Task SuspenderUnaClinicaCambiaSuEstadoYGuardaElMotivo()
    {
        await using var provider = await CrearProveedorAsync(new FakeAuthorizationService());
        var servicio = provider.GetRequiredService<IClinicaService>();
        var id = await servicio.CrearAsync(ModeloClinica("Para suspender", $"sus-{Guid.NewGuid():N}"[..20]));

        await servicio.CambiarEstadoAsync(id, EstadoClinica.Suspendida, MotivoSuspension.FaltaDePago);

        var clinica = (await servicio.ListarAsync()).Single(item => item.Id == id);
        Assert.Equal(EstadoClinica.Suspendida, clinica.Estado);
        Assert.Equal(MotivoSuspension.FaltaDePago, clinica.MotivoSuspension);
    }

    [Fact]
    public async Task NoSePuedeSuspenderSinIndicarElMotivo()
    {
        await using var provider = await CrearProveedorAsync(new FakeAuthorizationService());
        var servicio = provider.GetRequiredService<IClinicaService>();
        var id = await servicio.CrearAsync(ModeloClinica("Sin motivo", $"sin-{Guid.NewGuid():N}"[..20]));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CambiarEstadoAsync(id, EstadoClinica.Suspendida));

        Assert.Contains("suspensión", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReactivarUnaClinicaLimpiaElMotivoDeSuspension()
    {
        await using var provider = await CrearProveedorAsync(new FakeAuthorizationService());
        var servicio = provider.GetRequiredService<IClinicaService>();
        var id = await servicio.CrearAsync(ModeloClinica("Para reactivar", $"rea-{Guid.NewGuid():N}"[..20]));
        await servicio.CambiarEstadoAsync(id, EstadoClinica.Suspendida, MotivoSuspension.DecisionAdministrativa);

        await servicio.CambiarEstadoAsync(id, EstadoClinica.Activa);

        var clinica = (await servicio.ListarAsync()).Single(item => item.Id == id);
        Assert.Equal(EstadoClinica.Activa, clinica.Estado);
        Assert.Null(clinica.MotivoSuspension);
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
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(cadenaConexion));

        services.AddSingleton<ITenantProvider>(new TenantMutable { ClinicaId = fixture.ClinicaId });
        services.AddSingleton<ICurrentUserService>(TestSession.Crear());
        services.AddSingleton(autorizacion);
        services.AddSingleton<IAuditoriaService>(new FakeAuditoriaService());
        services.AddScoped<IClinicaService, ClinicaService>();

        return services.BuildServiceProvider();
    }
}
