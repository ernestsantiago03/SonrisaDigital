using SonrisaDigital.Services.Dashboard.DTOs;
using SonrisaDigital.Services.Pagos.DTOs;

namespace SonrisaDigital.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Saldo pendiente de la clínica para la tarjeta del Inicio. Lo ve cualquier usuario con sesión.</summary>
    Task<PagosResumenDto> GetResumenPagosAsync(CancellationToken cancellationToken = default);
}
