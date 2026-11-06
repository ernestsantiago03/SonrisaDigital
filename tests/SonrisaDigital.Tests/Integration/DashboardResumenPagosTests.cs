using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Dashboard;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// La tarjeta "Pagos pendientes" del Inicio se ve con cualquier sesión, aunque el usuario no tenga
/// el permiso Pagos.Ver. Sin sesión, sigue negándose.
/// </summary>
public sealed class DashboardResumenPagosTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task ElResumenDePagosNoRequiereElPermisoDePagos()
    {
        await using var db = fixture.CreateContext();
        var servicio = new DashboardService(
            pacienteRepository: null!,
            odontologoRepository: null!,
            pagoRepository: new PagoRepository(db),
            reporteRepository: new ReporteRepository(db),
            currentUserService: TestSession.Crear(),
            authorizationService: new FakeAuthorizationService(permitir: false));

        var resumen = await servicio.GetResumenPagosAsync();

        Assert.NotNull(resumen);
    }

    [Fact]
    public async Task SinSesionNoSeEntregaElResumenDePagos()
    {
        await using var db = fixture.CreateContext();
        var servicio = new DashboardService(
            pacienteRepository: null!,
            odontologoRepository: null!,
            pagoRepository: new PagoRepository(db),
            reporteRepository: new ReporteRepository(db),
            currentUserService: new CurrentUserService(),
            authorizationService: new FakeAuthorizationService());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.GetResumenPagosAsync());
    }
}
