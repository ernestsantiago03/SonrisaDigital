using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Reportes.DTOs;

namespace SonrisaDigital.Services.Reportes;

/// <summary>
/// Coordina las consultas de Reportes y aplica las reglas de acceso.
///
/// La página ya exige Reportes.Ver, pero el Service vuelve a comprobar los permisos
/// para que ninguna llamada interna pueda leer datos sin autorización. Además, un
/// usuario con rol Odontólogo que no sea Administrador ni Recepcionista queda
/// limitado a su propia actividad clínica.
/// </summary>
public sealed class ReporteService(
    IReporteRepository repository,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IReporteService
{
    private const string PermisoReportes = "Reportes.Ver";
    private const string PermisoCitas = "Citas.Ver";
    private const string PermisoPacientes = "Pacientes.Ver";
    private const string PermisoTratamientos = "Tratamientos.Ver";
    private const string PermisoPagos = "Pagos.Ver";

    public async Task<ReporteOpcionesDto> GetOpcionesAsync(CancellationToken cancellationToken = default)
    {
        var contexto = await CrearContextoAsync(cancellationToken);

        // Un odontólogo restringido no necesita el selector de odontólogos: sus
        // reportes siempre se filtran por su propio perfil.
        IReadOnlyList<ReporteOdontologoOpcionDto> odontologos = contexto.OdontologoRestringidoId is null
            ? (await repository.GetOdontologosAsync(cancellationToken))
                .Select(odontologo => new ReporteOdontologoOpcionDto(
                    odontologo.Id,
                    $"{odontologo.Nombres} {odontologo.Apellidos}".Trim()))
                .ToList()
            : [];

        return new ReporteOpcionesDto(
            odontologos,
            PuedeFiltrarOdontologos: contexto.OdontologoRestringidoId is null,
            PuedeVerPagos: contexto.PuedeVerPagos);
    }

    public async Task<ReporteResumenDto> GetResumenAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var contexto = await CrearContextoAsync(cancellationToken);
        await EnsurePermisoAsync(contexto.Principal, PermisoCitas);
        await EnsurePermisoAsync(contexto.Principal, PermisoPacientes);
        await EnsurePermisoAsync(contexto.Principal, PermisoTratamientos);
        ValidarFiltro(filtro);

        var odontologoId = ObtenerOdontologoId(filtro, contexto);
        var (desdeUtc, hastaUtcExclusivo) = ConvertirRangoLocalAUtc(filtro.Desde, filtro.Hasta);

        // Las citas del período alimentan tanto el conteo por estado como el de
        // pacientes atendidos (pacientes únicos con cita Atendida).
        var citas = await repository.GetCitasAsync(
            filtro.Desde,
            filtro.Hasta,
            odontologoId,
            estado: null,
            cancellationToken);

        var pacientesActivos = await repository.CountPacientesAsync(
            EstadoRegistro.Activo,
            cancellationToken);
        var pacientesInactivos = await repository.CountPacientesAsync(
            EstadoRegistro.Inactivo,
            cancellationToken);
        var pacientesNuevos = await repository.CountPacientesRegistradosAsync(
            desdeUtc,
            hastaUtcExclusivo,
            cancellationToken);

        var tratamientos = await repository.GetTratamientosAsync(
            desdeUtc,
            hastaUtcExclusivo,
            odontologoId,
            estado: null,
            cancellationToken);

        // El bloque de pagos solo se calcula si la sesión tiene Pagos.Ver. Así un
        // Odontólogo puede usar el resto del panel sin recibir datos financieros.
        ReportePagosResumenDto? pagos = null;
        if (contexto.PuedeVerPagos)
        {
            // El resumen financiero respeta el mismo alcance que las demás
            // pestañas: un odontólogo restringido solo ve su propia actividad.
            IReadOnlyList<Pago> pagosPeriodo = await repository.GetPagosAsync(
                desdeUtc,
                hastaUtcExclusivo,
                odontologoId,
                metodoPago: null,
                cancellationToken);

            var tratamientosConSaldo = await repository.GetTratamientosConSaldoAsync(
                odontologoId,
                cancellationToken);
            var saldoPendiente = tratamientosConSaldo.Sum(tratamiento =>
                Math.Max(0m, tratamiento.Precio - tratamiento.Pagos.Sum(pago => pago.Monto)));

            var porMetodo = pagosPeriodo
                .GroupBy(pago => pago.MetodoPago)
                .OrderBy(grupo => grupo.Key)
                .Select(grupo => new ReportePagoMetodoDto(
                    grupo.Key,
                    grupo.Count(),
                    grupo.Sum(pago => pago.Monto)))
                .ToList();

            pagos = new ReportePagosResumenDto(
                TotalCobrado: pagosPeriodo.Sum(pago => pago.Monto),
                CantidadPagos: pagosPeriodo.Count,
                SaldoPendiente: saldoPendiente,
                TratamientosConSaldo: tratamientosConSaldo.Count(tratamiento =>
                    tratamiento.Precio - tratamiento.Pagos.Sum(pago => pago.Monto) > 0),
                PorMetodo: porMetodo);
        }

        return new ReporteResumenDto(
            CitasTotal: citas.Count,
            CitasProgramadas: citas.Count(cita => cita.Estado == EstadoCita.Programada),
            CitasConfirmadas: citas.Count(cita => cita.Estado == EstadoCita.Confirmada),
            CitasAtendidas: citas.Count(cita => cita.Estado == EstadoCita.Atendida),
            CitasCanceladas: citas.Count(cita => cita.Estado == EstadoCita.Cancelada),
            CitasNoAsistio: citas.Count(cita => cita.Estado == EstadoCita.NoAsistio),
            PacientesTotal: pacientesActivos + pacientesInactivos,
            PacientesActivos: pacientesActivos,
            PacientesInactivos: pacientesInactivos,
            PacientesNuevos: pacientesNuevos,
            PacientesAtendidos: citas
                .Where(cita => cita.Estado == EstadoCita.Atendida)
                .Select(cita => cita.PacienteId)
                .Distinct()
                .Count(),
            TratamientosTotal: tratamientos.Count,
            TratamientosEnProceso: tratamientos.Count(tratamiento => tratamiento.Estado == EstadoTratamiento.EnProceso),
            TratamientosCompletados: tratamientos.Count(tratamiento => tratamiento.Estado == EstadoTratamiento.Completado),
            TratamientosCancelados: tratamientos.Count(tratamiento => tratamiento.Estado == EstadoTratamiento.Cancelado),
            TratamientosFacturado: tratamientos.Sum(tratamiento => tratamiento.Precio),
            Pagos: pagos);
    }

    public async Task<IReadOnlyList<ReporteCitaItemDto>> GetCitasAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var contexto = await CrearContextoAsync(cancellationToken);
        await EnsurePermisoAsync(contexto.Principal, PermisoCitas);
        ValidarFiltro(filtro);

        var citas = await repository.GetCitasAsync(
            filtro.Desde,
            filtro.Hasta,
            ObtenerOdontologoId(filtro, contexto),
            filtro.EstadoCitaFiltro,
            cancellationToken);

        return citas
            .Select(cita => new ReporteCitaItemDto(
                cita.Id,
                cita.Fecha,
                cita.HoraInicio,
                cita.HoraFin,
                NombreCompleto(cita.Paciente),
                NombreCompleto(cita.Odontologo),
                cita.Estado,
                cita.Motivo))
            .ToList();
    }

    public async Task<IReadOnlyList<ReportePacienteItemDto>> GetPacientesAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var contexto = await CrearContextoAsync(cancellationToken);
        await EnsurePermisoAsync(contexto.Principal, PermisoPacientes);
        ValidarFiltro(filtro);

        var citasAtendidas = await repository.GetCitasAtendidasAsync(
            filtro.Desde,
            filtro.Hasta,
            ObtenerOdontologoId(filtro, contexto),
            cancellationToken);

        // Se agrupa en memoria porque la cantidad de citas de un período mensual
        // es manejable y así se evita una consulta con GroupBy más compleja.
        return citasAtendidas
            .GroupBy(cita => cita.PacienteId)
            .Select(grupo =>
            {
                var paciente = grupo.First().Paciente;
                return new ReportePacienteItemDto(
                    paciente.Id,
                    NombreCompleto(paciente),
                    paciente.Identificacion,
                    paciente.Telefono,
                    paciente.FechaRegistro,
                    grupo.Count(),
                    grupo.Max(cita => cita.Fecha));
            })
            .OrderByDescending(paciente => paciente.UltimaCita)
            .ThenBy(paciente => paciente.Nombre)
            .ToList();
    }

    public async Task<IReadOnlyList<ReporteTratamientoItemDto>> GetTratamientosAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var contexto = await CrearContextoAsync(cancellationToken);
        await EnsurePermisoAsync(contexto.Principal, PermisoTratamientos);
        ValidarFiltro(filtro);

        var (desdeUtc, hastaUtcExclusivo) = ConvertirRangoLocalAUtc(filtro.Desde, filtro.Hasta);

        var tratamientos = await repository.GetTratamientosAsync(
            desdeUtc,
            hastaUtcExclusivo,
            ObtenerOdontologoId(filtro, contexto),
            filtro.EstadoTratamientoFiltro,
            cancellationToken);

        return tratamientos
            .Select(tratamiento =>
            {
                var pagado = tratamiento.Pagos.Sum(pago => pago.Monto);
                return new ReporteTratamientoItemDto(
                    tratamiento.Id,
                    tratamiento.Fecha,
                    NombreCompleto(tratamiento.Paciente),
                    tratamiento.Tratamiento.Nombre,
                    NombreCompleto(tratamiento.Odontologo),
                    tratamiento.Precio,
                    pagado,
                    Math.Max(0m, tratamiento.Precio - pagado),
                    tratamiento.Estado);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ReportePagoItemDto>> GetPagosAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var contexto = await CrearContextoAsync(cancellationToken);
        await EnsurePermisoAsync(contexto.Principal, PermisoPagos);
        ValidarFiltro(filtro);

        var (desdeUtc, hastaUtcExclusivo) = ConvertirRangoLocalAUtc(filtro.Desde, filtro.Hasta);

        var pagos = await repository.GetPagosAsync(
            desdeUtc,
            hastaUtcExclusivo,
            ObtenerOdontologoId(filtro, contexto),
            filtro.MetodoPagoFiltro,
            cancellationToken);

        return pagos
            .Select(pago => new ReportePagoItemDto(
                pago.Id,
                pago.Fecha,
                NombreCompleto(pago.TratamientoRealizado.Paciente),
                pago.TratamientoRealizado.Tratamiento.Nombre,
                pago.Monto,
                pago.MetodoPago,
                ObtenerNombreUsuario(pago.Usuario)))
            .ToList();
    }

    /// <summary>
    /// Crea el contexto de autorización de la llamada actual. Si el usuario tiene
    /// rol Odontólogo y no es Administrador ni Recepcionista, busca su perfil para
    /// forzar el filtro por odontólogo en todas las consultas clínicas.
    /// </summary>
    private async Task<ContextoReporte> CrearContextoAsync(CancellationToken cancellationToken)
    {
        var principal = currentUserService.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException(
                "No hay una sesión autenticada para consultar los reportes.");
        }

        await EnsurePermisoAsync(principal, PermisoReportes);

        var usuarioId = currentUserService.UsuarioId
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            throw new UnauthorizedAccessException(
                "No se pudo identificar al usuario de la sesión.");
        }

        var esAdministrador = principal.IsInRole(RolesSonrisaDigital.Administrador);
        var esRecepcionista = principal.IsInRole(RolesSonrisaDigital.Recepcionista);
        var esOdontologo = principal.IsInRole(RolesSonrisaDigital.Odontologo);

        int? odontologoRestringidoId = null;
        if (esOdontologo && !esAdministrador && !esRecepcionista)
        {
            odontologoRestringidoId = await repository.GetOdontologoIdByUserAsync(
                usuarioId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "El usuario odontólogo no tiene un perfil de odontólogo asociado.");
        }

        var puedeVerPagos = await TienePermisoAsync(principal, PermisoPagos);

        return new ContextoReporte(
            principal,
            odontologoRestringidoId,
            puedeVerPagos);
    }

    /// <summary>Comprueba una política concreta y lanza un error de negocio si falla.</summary>
    private async Task EnsurePermisoAsync(ClaimsPrincipal principal, string permiso)
    {
        if (!await TienePermisoAsync(principal, permiso))
        {
            throw new UnauthorizedAccessException(
                $"No tienes permiso para consultar este reporte ({permiso}).");
        }
    }

    private async Task<bool> TienePermisoAsync(ClaimsPrincipal principal, string permiso)
    {
        var resultado = await authorizationService.AuthorizeAsync(principal, permiso);
        return resultado.Succeeded;
    }

    /// <summary>
    /// Devuelve el odontólogo que debe aplicarse al filtro. Si la sesión está
    /// restringida, se ignora cualquier selección manual de la UI.
    /// </summary>
    private static int? ObtenerOdontologoId(
        ReporteFiltroDto filtro,
        ContextoReporte contexto) =>
        contexto.OdontologoRestringidoId ?? filtro.OdontologoId;

    /// <summary>Valida que el rango de fechas locales tenga sentido.</summary>
    private static void ValidarFiltro(ReporteFiltroDto filtro)
    {
        if (filtro.Hasta < filtro.Desde)
        {
            throw new ArgumentException(
                "La fecha final no puede ser anterior a la fecha inicial.");
        }
    }

    /// <summary>
    /// Convierte el rango de fechas locales a UTC. El límite superior se envía
    /// exclusivo para incluir completo el último día seleccionado.
    /// </summary>
    private static (DateTime DesdeUtc, DateTime HastaUtcExclusivo) ConvertirRangoLocalAUtc(
        DateOnly desde,
        DateOnly hasta)
    {
        var desdeLocal = DateTime.SpecifyKind(
            desde.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local);
        var hastaLocalExclusivo = DateTime.SpecifyKind(
            hasta.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local);

        return (desdeLocal.ToUniversalTime(), hastaLocalExclusivo.ToUniversalTime());
    }

    private static string NombreCompleto(Paciente paciente) =>
        $"{paciente.Nombres} {paciente.Apellidos}".Trim();

    private static string NombreCompleto(Odontologo odontologo) =>
        $"{odontologo.Nombres} {odontologo.Apellidos}".Trim();

    private static string ObtenerNombreUsuario(Usuario? usuario)
    {
        if (usuario?.Odontologo is not null)
        {
            var nombreOdontologo = NombreCompleto(usuario.Odontologo);
            if (!string.IsNullOrWhiteSpace(nombreOdontologo))
            {
                return nombreOdontologo;
            }
        }

        return string.IsNullOrWhiteSpace(usuario?.Nombre) ? "Sistema" : usuario.Nombre;
    }

    /// <summary>Datos de autorización resueltos una vez por operación.</summary>
    private sealed record ContextoReporte(
        ClaimsPrincipal Principal,
        int? OdontologoRestringidoId,
        bool PuedeVerPagos);
}
