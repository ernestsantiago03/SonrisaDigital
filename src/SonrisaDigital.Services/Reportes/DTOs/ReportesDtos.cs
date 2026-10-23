using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Reportes.DTOs;

/// <summary>
/// Filtro común de todos los reportes. El período se expresa con fechas locales
/// (las que ve el usuario); el Service las convierte a UTC para las entidades que
/// guardan fecha y hora (pagos, tratamientos y pacientes).
/// </summary>
public sealed record ReporteFiltroDto(
    DateOnly Desde,
    DateOnly Hasta,
    int? OdontologoId = null,
    EstadoCita? EstadoCitaFiltro = null,
    EstadoTratamiento? EstadoTratamientoFiltro = null,
    MetodoPago? MetodoPagoFiltro = null);

/// <summary>
/// Opciones que alimentan los filtros de la página. También le indican a la UI
/// qué pestañas y filtros puede mostrar según los permisos de la sesión.
/// </summary>
public sealed record ReporteOpcionesDto(
    IReadOnlyList<ReporteOdontologoOpcionDto> Odontologos,
    bool PuedeFiltrarOdontologos,
    bool PuedeVerPagos);

/// <summary>Odontólogo disponible para filtrar los reportes.</summary>
public sealed record ReporteOdontologoOpcionDto(int Id, string Nombre);

/// <summary>
/// Resumen general del período. Se muestra en las tarjetas superiores y en los
/// bloques de totales de cada pestaña.
/// </summary>
public sealed record ReporteResumenDto(
    int CitasTotal,
    int CitasProgramadas,
    int CitasConfirmadas,
    int CitasAtendidas,
    int CitasCanceladas,
    int CitasNoAsistio,
    int PacientesTotal,
    int PacientesActivos,
    int PacientesInactivos,
    int PacientesNuevos,
    int PacientesAtendidos,
    int TratamientosTotal,
    int TratamientosEnProceso,
    int TratamientosCompletados,
    int TratamientosCancelados,
    decimal TratamientosFacturado,
    ReportePagosResumenDto? Pagos);

/// <summary>
/// Resumen financiero. Es nulo cuando el usuario no tiene Pagos.Ver, de modo que
/// el reporte clínico pueda consultarse sin exponer información económica.
/// </summary>
public sealed record ReportePagosResumenDto(
    decimal TotalCobrado,
    int CantidadPagos,
    decimal SaldoPendiente,
    int TratamientosConSaldo,
    IReadOnlyList<ReportePagoMetodoDto> PorMetodo);

/// <summary>Total cobrado y cantidad de operaciones agrupadas por método de pago.</summary>
public sealed record ReportePagoMetodoDto(MetodoPago Metodo, int Cantidad, decimal Total);

/// <summary>Fila del reporte de citas.</summary>
public sealed record ReporteCitaItemDto(
    int Id,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    string Paciente,
    string Odontologo,
    EstadoCita Estado,
    string? Motivo);

/// <summary>
/// Fila del reporte de pacientes atendidos. Se agrupa por paciente para mostrar
/// cuántas citas atendidas tuvo y la fecha de la última dentro del período.
/// </summary>
public sealed record ReportePacienteItemDto(
    int PacienteId,
    string Nombre,
    string? Identificacion,
    string? Telefono,
    DateTime FechaRegistro,
    int CitasAtendidas,
    DateOnly UltimaCita);

/// <summary>Fila del reporte de tratamientos realizados.</summary>
public sealed record ReporteTratamientoItemDto(
    int Id,
    DateTime Fecha,
    string Paciente,
    string Tratamiento,
    string Odontologo,
    decimal Precio,
    decimal Pagado,
    decimal Saldo,
    EstadoTratamiento Estado);

/// <summary>Fila del reporte de pagos.</summary>
public sealed record ReportePagoItemDto(
    int Id,
    DateTime Fecha,
    string Paciente,
    string Tratamiento,
    decimal Monto,
    MetodoPago Metodo,
    string RegistradoPor);
