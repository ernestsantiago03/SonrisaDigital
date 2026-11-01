using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Odontograma.DTOs;

namespace SonrisaDigital.Services.Odontograma;

public interface IOdontogramaService
{
    /// <summary>
    /// Una página de pacientes activos para el panel de búsqueda, ordenados alfabéticamente.
    /// Si se indica <paramref name="searchTerm"/> se filtran por nombres o apellidos.
    /// <c>Total</c> dice cuántos coinciden en total, para mostrar la paginación.
    /// </summary>
    Task<PagedResult<OdontogramaPacienteDto>> SearchActivePatientsPagedAsync(
        string? searchTerm,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Un paciente activo por Id, aunque no esté entre los primeros resultados de la búsqueda.
    /// Devuelve null si no existe o está inactivo.
    /// </summary>
    Task<OdontogramaPacienteDto?> GetActivePatientAsync(int pacienteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OdontogramaPiezaDto>> GetByPatientAsync(int pacienteId, CancellationToken cancellationToken = default);

    /// <summary>Todos los guardados de una pieza de un paciente, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<OdontogramaHistorialDto>> GetHistorialAsync(int pacienteId, int piezaDentalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Contexto de edición del odontograma. Indica si el usuario puede guardar, cuál es
    /// su odontólogo vinculado y, si es un administrador sin perfil, qué odontólogos
    /// activos puede elegir como firmante.
    /// </summary>
    Task<OdontogramaEditContextDto> GetEditContextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda una pieza dental. El usuario responsable se obtiene de la sesión actual,
    /// no de un parámetro que el llamador pueda falsificar (ARQ-05). Un odontólogo
    /// vinculado solo puede guardar como él mismo; un administrador sin perfil debe
    /// indicar en <paramref name="odontologoId"/> un odontólogo activo.
    /// </summary>
    Task SaveAsync(
        int pacienteId,
        int piezaDentalId,
        EstadoOdontograma estado,
        string? observaciones,
        int? odontologoId = null,
        CancellationToken cancellationToken = default);
}