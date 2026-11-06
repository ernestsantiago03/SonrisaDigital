using SonrisaDigital.Services.Dashboard;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Unit;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task RechazaSinPermisoDePacientesAntesDeConsultar()
    {
        var servicio = new DashboardService(
            pacienteRepository: null!,
            odontologoRepository: null!,
            pagoRepository: null!,
            reporteRepository: null!,
            currentUserService: TestSession.Crear(),
            authorizationService: new FakeAuthorizationService(permitir: false));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.GetStatsAsync());
    }

    [Fact]
    public async Task LosCobrosDelMesRechazanSinPermisoDePagosAntesDeConsultar()
    {
        var servicio = new DashboardService(
            pacienteRepository: null!,
            odontologoRepository: null!,
            pagoRepository: null!,
            reporteRepository: null!,
            currentUserService: TestSession.Crear(),
            authorizationService: new FakeAuthorizationService(permitir: false));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.GetCobrosMesAsync());
    }
}
