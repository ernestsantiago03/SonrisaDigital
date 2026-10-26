using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Citas.DTOs;

namespace SonrisaDigital.Services.Citas;

public interface ICitaService
{
    Task<IReadOnlyList<CitaListItemDto>> SearchAsync(
        DateOnly? fecha = null,
        int? pacienteId = null,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Página de citas para la tabla principal. Devuelve también el total que cumple
    /// los filtros para poder mostrar la paginación.
    /// </summary>
    Task<PagedResult<CitaListItemDto>> SearchPagedAsync(
        DateOnly? fecha = null,
        int? pacienteId = null,
        int? odontologoId = null,
        EstadoCita? estado = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<CitaDetalleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CitaFormDto?> GetFormAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CitaFormDto model, CancellationToken cancellationToken = default);
    Task UpdateAsync(CitaFormDto model, CancellationToken cancellationToken = default);
    Task SetEstadoAsync(int id, EstadoCita estado, CancellationToken cancellationToken = default);
}
