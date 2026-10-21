using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Tratamientos.DTOs;

namespace SonrisaDigital.Services.Tratamientos;

public interface ITratamientoService
{
    // Catálogo
    Task<PagedResult<TratamientoListItemDto>> SearchCatalogoAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<TratamientoFormDto?> GetCatalogoFormAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateCatalogoAsync(TratamientoFormDto model, CancellationToken cancellationToken = default);

    Task UpdateCatalogoAsync(TratamientoFormDto model, CancellationToken cancellationToken = default);

    // Realizados
    Task<PagedResult<TratamientoRealizadoListItemDto>> SearchRealizadosAsync(
        string? searchTerm,
        EstadoTratamiento? estado,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<TratamientoRealizadoDetalleDto?> GetRealizadoAsync(int id, CancellationToken cancellationToken = default);

    Task<TratamientoRealizadoFormDto?> GetRealizadoFormAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateRealizadoAsync(TratamientoRealizadoFormDto model, CancellationToken cancellationToken = default);

    Task UpdateRealizadoAsync(TratamientoRealizadoFormDto model, CancellationToken cancellationToken = default);

    Task CancelarRealizadoAsync(int id, CancellationToken cancellationToken = default);

    // Opciones de los formularios
    Task<IReadOnlyList<TratamientoOptionDto>> GetTratamientosAsync(bool soloActivos, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PacienteOptionDto>> GetPacientesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OdontologoOptionDto>> GetOdontologosAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PiezaDentalOptionDto>> GetPiezasAsync(CancellationToken cancellationToken = default);
}
