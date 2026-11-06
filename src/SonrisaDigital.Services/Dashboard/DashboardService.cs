using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Dashboard.DTOs;
using SonrisaDigital.Services.Pagos.DTOs;

namespace SonrisaDigital.Services.Dashboard;

public sealed class DashboardService(
    IPacienteRepository pacienteRepository,
    IOdontologoRepository odontologoRepository,
    IPagoRepository pagoRepository,
    IReporteRepository reporteRepository,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IDashboardService
{
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
    /// Totales de cobro de la clínica para la tarjeta "Pagos pendientes" del Inicio. Solo exige una
    /// sesión: el detalle de pagos (módulo Pagos) sigue protegido por Pagos.Ver.
    /// </summary>
    public async Task<PagosResumenDto> GetResumenPagosAsync(CancellationToken cancellationToken = default)
    {
        if (currentUserService.Principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Debes iniciar sesión para ver el resumen de pagos.");
        }

        var totales = await pagoRepository.GetTotalesAsync(cancellationToken);

        return new PagosResumenDto(
            totales.TotalFacturado,
            totales.TotalCobrado,
            totales.TotalFacturado - totales.TotalCobrado,
            totales.TratamientosConSaldo);
    }

    /// <summary>
    /// Suma los pagos desde el primer día del mes hasta hoy. Se apoya en el rango de
    /// calendario que ya usa Reportes: los pagos guardan su fecha en hora local.
    /// </summary>
    public async Task<DashboardCobrosDto> GetCobrosMesAsync(CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(
            currentUserService,
            "Pagos.Ver",
            "No tienes permiso para consultar pagos.");

        var hoy = RelojNegocio.HoyDateOnly();
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var desde = inicioMes.ToDateTime(TimeOnly.MinValue);
        var hastaExclusivo = hoy.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var inicioDia = hoy.ToDateTime(TimeOnly.MinValue);

        var pagos = await reporteRepository.GetPagosAsync(
            desde,
            hastaExclusivo,
            cancellationToken: cancellationToken);

        return new DashboardCobrosDto(
            CobradoHoy: pagos.Where(pago => pago.Fecha >= inicioDia).Sum(pago => pago.Monto),
            CobradoMes: pagos.Sum(pago => pago.Monto),
            PagosMes: pagos.Count);
    }

    /// <summary>
    /// Devuelve el lunes a las 00:00 de la semana actual en hora de la clínica, convertido
    /// a UTC para poder compararlo con la columna FechaRegistro.
    /// </summary>
    private static DateTime ObtenerInicioSemanaUtc()
    {
        var ahoraLocal = RelojNegocio.Ahora();
        var diasDesdeLunes = ((int)ahoraLocal.DayOfWeek + 6) % 7;
        return RelojNegocio.AUtc(ahoraLocal.Date.AddDays(-diasDesdeLunes));
    }
}
