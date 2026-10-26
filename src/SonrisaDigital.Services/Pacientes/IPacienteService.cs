using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Pacientes.DTOs;

namespace SonrisaDigital.Services.Pacientes;

public interface IPacienteService
{
    Task<IReadOnlyList<PacienteListItemDto>> SearchAsync(string? searchTerm, EstadoRegistro? estado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Página de pacientes para la tabla principal. Devuelve también el total que
    /// cumple el filtro para poder mostrar la paginación.
    /// </summary>
    Task<PagedResult<PacienteListItemDto>> SearchPagedAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<PacienteDetalleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PacienteFormDto?> GetFormAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(PacienteFormDto model, CancellationToken cancellationToken = default);
    Task UpdateAsync(PacienteFormDto model, CancellationToken cancellationToken = default);
    Task SetStatusAsync(int id, EstadoRegistro estado, CancellationToken cancellationToken = default);
}