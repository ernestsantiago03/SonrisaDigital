using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

/// <summary>
/// Consultas de solo lectura que alimentan el módulo de Reportes.
/// No crea ni modifica entidades: reutiliza las tablas existentes y aplica los
/// filtros de período, odontólogo, estado y método de pago.
/// </summary>
public interface IReporteRepository
{
    /// <summary>Citas del período, con paciente y odontólogo, para el detalle y el resumen.</summary>
    Task<IReadOnlyList<Cita>> GetCitasAsync(
        DateOnly desde,
        DateOnly hasta,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Citas atendidas del período. Se usa para calcular los pacientes únicos
    /// atendidos y para construir la tabla de pacientes atendidos.
    /// </summary>
    Task<IReadOnlyList<Cita>> GetCitasAtendidasAsync(
        DateOnly desde,
        DateOnly hasta,
        int? odontologoId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Cuenta pacientes, opcionalmente filtrando por estado activo/inactivo.</summary>
    Task<int> CountPacientesAsync(
        EstadoRegistro? estado = null,
        CancellationToken cancellationToken = default);

    /// <summary>Cuenta los pacientes cuyo registro ocurrió dentro del rango UTC.</summary>
    Task<int> CountPacientesRegistradosAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        CancellationToken cancellationToken = default);

    /// <summary>Tratamientos realizados en el período, con sus pagos y datos relacionados.</summary>
    Task<IReadOnlyList<TratamientoRealizado>> GetTratamientosAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? odontologoId = null,
        EstadoTratamiento? estado = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Todos los tratamientos con precio y sus pagos, sin filtro de fecha. Sirve
    /// para calcular el saldo pendiente actual. Se puede limitar al odontólogo
    /// indicado para respetar la restricción de los usuarios clínicos.
    /// </summary>
    Task<IReadOnlyList<TratamientoRealizado>> GetTratamientosConSaldoAsync(
        int? odontologoId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pagos del período, con el tratamiento, paciente y usuario que los registró.
    /// Se puede limitar a los pagos de los tratamientos de un odontólogo.
    /// </summary>
    Task<IReadOnlyList<Pago>> GetPagosAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? odontologoId = null,
        MetodoPago? metodoPago = null,
        CancellationToken cancellationToken = default);

    /// <summary>Odontólogos disponibles para el filtro del reporte.</summary>
    Task<IReadOnlyList<Odontologo>> GetOdontologosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el odontólogo vinculado a una cuenta. Se usa para limitar a un
    /// usuario con rol Odontólogo a su propia actividad clínica.
    /// </summary>
    Task<int?> GetOdontologoIdByUserAsync(
        string usuarioId,
        CancellationToken cancellationToken = default);
}
