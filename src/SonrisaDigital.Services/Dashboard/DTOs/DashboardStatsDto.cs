namespace SonrisaDigital.Services.Dashboard.DTOs;

public sealed record DashboardStatsDto(
    int TotalPacientesActivos,
    int TotalOdontologosActivos,
    int PacientesRegistradosEstaSemana,
    int OdontologosRegistradosEstaSemana);
