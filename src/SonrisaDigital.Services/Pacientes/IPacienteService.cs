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

    /// <summary>
    /// Devuelve solo los datos clínicos editables desde la ficha del paciente.
    /// Requiere el permiso de consulta de historia clínica.
    /// </summary>
    Task<PacienteClinicoDto?> GetClinicoAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea el paciente y, si se recibe el bloque clínico, guarda también sus alergias
    /// dentro de la misma operación.
    /// </summary>
    Task<int> CreateAsync(PacienteFormDto model, PacienteClinicoDto? clinico = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los datos administrativos y, si se recibe el bloque clínico, sus alergias.
    /// El estado del paciente no se modifica por esta vía.
    /// </summary>
    Task UpdateAsync(PacienteFormDto model, PacienteClinicoDto? clinico = null, CancellationToken cancellationToken = default);

    Task SetStatusAsync(int id, EstadoRegistro estado, CancellationToken cancellationToken = default);
}