using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IPagoRepository
{
    /// <summary>
    /// Página de tratamientos que pueden generar pagos (solo los que tienen precio), con
    /// su paciente, su tratamiento y sus pagos cargados. Filtra por texto, por estado
    /// financiero y, si se indica, por paciente. El conteo y el recorte se hacen en SQL.
    /// </summary>
    Task<PagedResult<TratamientoRealizado>> SearchTratamientosPagedAsync(
        string? searchTerm,
        EstadoFinanciero? estado,
        int? pacienteId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula en SQL el total facturado, el total cobrado y cuántos tratamientos siguen
    /// con saldo. Evita cargar todos los tratamientos y sus pagos solo para sumarlos.
    /// </summary>
    Task<PagoTotales> GetTotalesAsync(CancellationToken cancellationToken = default);

    /// <summary>Un tratamiento para registrarle un pago (rastreando los cambios).</summary>
    Task<TratamientoRealizado?> GetTratamientoAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual que <see cref="GetTratamientoAsync"/>, pero bloquea la fila del tratamiento
    /// hasta que termine la transacción. Se usa al registrar pagos para que dos cobros
    /// simultáneos no calculen el saldo sobre los mismos datos.
    /// </summary>
    Task<TratamientoRealizado?> GetTratamientoParaPagoAsync(
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
