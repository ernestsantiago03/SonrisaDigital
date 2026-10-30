using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Clinicas;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// "Acerca De" es por clínica: cada clínica lee y edita solo su identidad, y solo con el permiso
/// AcercaDe.Editar (plan multiclínica, criterio 9).
/// </summary>
public sealed class IdentidadClinicaTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task CadaClinicaVeSoloSuIdentidad()
    {
        var clinicaA = await CrearClinicaAsync("Clinica A identidad");
        var clinicaB = await CrearClinicaAsync("Clinica B identidad");
        var tenant = new TenantMutable { ClinicaId = clinicaA };
        var servicio = CrearServicio(tenant, new FakeAuthorizationService());

        await servicio.ActualizarAsync(Modelo("Nombre nuevo de A"));

        Assert.Equal("Nombre nuevo de A", (await servicio.ObtenerAsync())!.Nombre);
        tenant.ClinicaId = clinicaB;
        Assert.Equal("Clinica B identidad", (await servicio.ObtenerAsync())!.Nombre);
    }

    [Fact]
    public async Task SinPermisoNoSeGuardaLaIdentidad()
    {
        var clinica = await CrearClinicaAsync("Clinica sin permiso");
        var servicio = CrearServicio(new TenantMutable { ClinicaId = clinica }, new FakeAuthorizationService(true, "AcercaDe.Editar"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ActualizarAsync(Modelo("No debe guardarse")));

        await using var db = fixture.CreateContext();
        var actual = await db.Clinicas.AsNoTracking().SingleAsync(clinic => clinic.Id == clinica);
        Assert.Equal("Clinica sin permiso", actual.Nombre);
    }

    [Fact]
    public async Task SinClinicaNoHayIdentidad()
    {
        var servicio = CrearServicio(new TenantMutable { ClinicaId = null }, new FakeAuthorizationService());

        Assert.Null(await servicio.ObtenerAsync());
    }

    private static IdentidadClinicaFormDto Modelo(string nombre) =>
        new() { Nombre = nombre, Telefono = "809-555-0000", Correo = "contacto@clinica.local" };

    private IIdentidadClinicaService CrearServicio(ITenantProvider tenant, IAuthorizationService autorizacion)
    {
        var db = new ApplicationDbContext(fixture.Options, tenant);
        return new IdentidadClinicaService(
            db,
            tenant,
            new FakeAuditoriaService(),
            TestSession.Crear(),
            autorizacion);
    }

    private async Task<int> CrearClinicaAsync(string nombre)
    {
        await using var db = new ApplicationDbContext(fixture.Options, SinClinica.Instancia);
        var clinica = new Clinica { Nombre = nombre, Codigo = $"id-{Guid.NewGuid():N}"[..20] };
        db.Clinicas.Add(clinica);
        await db.SaveChangesAsync();
        return clinica.Id;
    }
}
