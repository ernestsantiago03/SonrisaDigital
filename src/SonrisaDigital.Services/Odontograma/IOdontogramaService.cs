using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Odontograma.DTOs;

namespace SonrisaDigital.Services.Odontograma;

public interface IOdontogramaService
{
    /// <summary>
    /// Pacientes activos para el panel de búsqueda, ordenados alfabéticamente por
    /// nombre completo. Si se indica <paramref name="searchTerm"/> se filtran por
    /// nombres o apellidos.
    /// </summary>
    Task<IReadOnlyList<OdontogramaPacienteDto>> GetActivePatientsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OdontogramaPiezaDto>> GetByPatientAsync(int pacienteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda una pieza dental. El usuario responsable se obtiene de la sesión
    /// actual, no de un parámetro que el llamador pueda falsificar (ARQ-05).
    /// </summary>
    Task SaveAsync(
        int pacienteId,
        int piezaDentalId,
        EstadoOdontograma estado,
        string? observaciones,
        CancellationToken cancellationToken = default);
}