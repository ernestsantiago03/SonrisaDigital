using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Pagos.DTOs;

namespace SonrisaDigital.Services.Pagos;

public interface IPagoService
{
    /// <summary>
    /// Totales del módulo de pagos: total facturado, total cobrado, saldo pendiente y
    /// cuántos tratamientos siguen con saldo.
    /// </summary>
    Task<PagosResumenDto> GetResumenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ficha de un tratamiento con su historial de cuotas. Devuelve <c>null</c> si el
    /// tratamiento no existe.
    /// </summary>
    Task<PagoDetalleDto?> GetDetalleAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tratamientos realizados de un paciente con su estado financiero, para las
    /// pestañas de Tratamientos y Pagos de la ficha del paciente.
    /// </summary>
    Task<IReadOnlyList<PagoDetalleDto>> GetByPacienteAsync(
        int pacienteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Página de tratamientos que pueden generar pagos, filtrando por texto, por estado
    /// financiero y, si se indica, por paciente. Devuelve también el total del filtro.
    /// </summary>
    Task<PagedResult<TratamientoPagoListItemDto>> SearchTratamientosPagedAsync(
        string? searchTerm,
        EstadoFinanciero? estado,
        int? pacienteId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>Últimos pagos registrados, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<PagoHistorialDto>> GetHistorialAsync(
        int cantidad = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un pago nuevo sobre un tratamiento y devuelve el id del pago creado.
    /// Valida que el monto no pase del saldo pendiente.
    /// </summary>
    Task<int> RegistrarAsync(RegistrarPagoDto model, CancellationToken cancellationToken = default);
}
