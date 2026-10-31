using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Clinicas;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// El selector del Dueño lista las clínicas aunque otra consulta esté en curso en el mismo circuito.
/// Antes compartía el DbContext de la página y fallaba al cargar datos.
/// </summary>
public sealed class SelectorClinicasTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task ListaLasClinicasMientrasOtraConsultaEstaEnCurso()
    {
        var nombre = $"Selector {Guid.NewGuid():N}";
        await using (var db = new ApplicationDbContext(fixture.Options, SinClinica.Instancia))
        {
            db.Clinicas.Add(new Clinica { Nombre = nombre, Codigo = $"sel-{Guid.NewGuid():N}"[..20] });
            await db.SaveChangesAsync();
        }

        var contextoCompartido = new ApplicationDbContext(fixture.Options, new TenantFijo(fixture.ClinicaId));
        var selector = new SelectorClinicasService(
            fixture.Options,
            TestSession.Crear(),
            new FakeAuthorizationService());

        var otraConsulta = contextoCompartido.Pacientes.AsNoTracking().ToListAsync();
        var listado = selector.ListarAsync();
        await Task.WhenAll(otraConsulta, listado);

        Assert.Contains(await listado, clinica => clinica.Nombre == nombre);
        await contextoCompartido.DisposeAsync();
    }

    [Fact]
    public async Task SinPermisoNoListaLasClinicas()
    {
        var selector = new SelectorClinicasService(
            fixture.Options,
            TestSession.Crear(),
            new FakeAuthorizationService(true, "Clinicas.Ver"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => selector.ListarAsync());
    }
}
