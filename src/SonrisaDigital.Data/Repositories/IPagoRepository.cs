using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IPagoRepository
{
    /// <summary>
    /// Tratamientos que pueden generar pagos (solo los que tienen precio), con su paciente,
    /// su tratamiento y sus pagos ya cargados. Si recibe texto, busca por nombre del paciente
    /// o por nombre del tratamiento.
    /// </summary>
    Task<IReadOnlyList<TratamientoRealizado>> GetTratamientosAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    /// <summary>Un tratamiento para registrarle un pago (rastreando los cambios).</summary>
    Task<TratamientoRealizado?> GetTratamientoAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el tratamiento con todo lo necesario para la ficha de detalle:
    /// paciente, odontólogo, pieza dental y sus pagos con el usuario que los registró.
    /// </summary>
    Task<TratamientoRealizado?> GetDetalleAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los tratamientos realizados de un paciente con el detalle completo
    /// (odontólogo, pieza dental y pagos) para su ficha.
    /// </summary>
    Task<IReadOnlyList<TratamientoRealizado>> GetByPacienteAsync(
        int pacienteId,
        CancellationToken cancellationToken = default);

    /// <summary>Últimos pagos registrados, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<Pago>> GetHistorialAsync(
        int cantidad,
        CancellationToken cancellationToken = default);

    /// <summary>Prepara un pago nuevo para insertarlo en la base de datos.</summary>
    Task AddAsync(Pago pago, CancellationToken cancellationToken = default);

    /// <summary>Aplica en la base de datos todos los cambios pendientes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
