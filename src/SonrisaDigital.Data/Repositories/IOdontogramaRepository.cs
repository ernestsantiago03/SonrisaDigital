using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IOdontogramaRepository
{
    /// <summary>
    /// Pacientes activos para el panel de búsqueda del odontograma, ordenados
    /// alfabéticamente por nombre completo. Si se indica <paramref name="searchTerm"/>
    /// se filtran por nombres o apellidos.
    /// </summary>
    Task<IReadOnlyList<Paciente>> GetActivePatientsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PiezaDental>> GetPiecesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistroOdontograma>> GetRecordsAsync(int pacienteId, CancellationToken cancellationToken = default);
    Task<int?> GetOdontologoIdByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<RegistroOdontograma?> GetRecordAsync(int pacienteId, int piezaDentalId, CancellationToken cancellationToken = default);
    Task AddAsync(RegistroOdontograma record, CancellationToken cancellationToken = default);
    Task SaveChangesWithAuditAsync(RegistroAuditoria audit, CancellationToken cancellationToken = default);
}