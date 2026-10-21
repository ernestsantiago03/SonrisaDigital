using SonrisaDigital.Services.Dashboard.DTOs;

namespace SonrisaDigital.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}
