using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Dashboard.DTOs;

namespace SonrisaDigital.Services.Dashboard;

public sealed class DashboardService(
    IPacienteRepository pacienteRepository,
    IOdontologoRepository odontologoRepository,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IDashboardService
{
    // La clínica trabaja con la hora de República Dominicana, pero las fechas de registro
    // se guardan en UTC. Esta zona se usa para encontrar el lunes de la semana actual.
    private static readonly TimeZoneInfo ZonaHorariaClinica = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SA Western Standard Time" : "America/Santo_Domingo");

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        // Las cifras de pacientes y odontólogos solo se entregan con sus permisos de lectura.
        await authorizationService.AsegurarPermisoAsync(
            currentUserService,
            "Pacientes.Ver",
            "No tienes permiso para consultar pacientes.");
        await authorizationService.AsegurarPermisoAsync(
            currentUserService,
            "Odontologos.Ver",
            "No tienes permiso para consultar odontólogos.");

        var inicioSemanaUtc = ObtenerInicioSemanaUtc();

        // Los conteos se resuelven en SQL. Antes se traían todos los pacientes y odontólogos
        // solo para contarlos en memoria, lo que crecía con el tamaño de la clínica.
        var pacientesActivos = await pacienteRepository.CountAsync(
            EstadoRegistro.Activo,
            cancellationToken: cancellationToken);

        var odontologosActivos = await odontologoRepository.CountAsync(
            EstadoRegistro.Activo,
            cancellationToken: cancellationToken);

        var pacientesEstaSemana = await pacienteRepository.CountAsync(
            EstadoRegistro.Activo,
            inicioSemanaUtc,
            cancellationToken);

        var odontologosEstaSemana = await odontologoRepository.CountAsync(
            EstadoRegistro.Activo,
            inicioSemanaUtc,
            cancellationToken);

        return new DashboardStatsDto(
            TotalPacientesActivos: pacientesActivos,
            TotalOdontologosActivos: odontologosActivos,
            PacientesRegistradosEstaSemana: pacientesEstaSemana,
            OdontologosRegistradosEstaSemana: odontologosEstaSemana);
    }

    /// <summary>
    /// Devuelve el lunes a las 00:00 de la semana actual en hora de la clínica, convertido
    /// a UTC para poder compararlo con la columna FechaRegistro.
    /// </summary>
    private static DateTime ObtenerInicioSemanaUtc()
    {
        var ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaHorariaClinica);
        var diasDesdeLunes = ((int)ahoraLocal.DayOfWeek + 6) % 7;
        var inicioSemanaLocal = DateTime.SpecifyKind(
            ahoraLocal.Date.AddDays(-diasDesdeLunes),
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(inicioSemanaLocal, ZonaHorariaClinica);
    }
}
