using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IOdontogramaRepository
{
    /// <summary>
    /// Pacientes activos para el panel de búsqueda del odontograma, ordenados
    /// alfabéticamente por nombre completo. Si se indica <paramref name="searchTerm"/>
    /// se filtran por nombres o apellidos.
    /// </summary>
    Task<PagedResult<Paciente>> SearchActivePatientsPagedAsync(
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<Paciente?> GetActivePatientByIdAsync(int pacienteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PiezaDental>> GetPiecesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistroOdontograma>> GetRecordsAsync(int pacienteId, CancellationToken cancellationToken = default);
    Task<int?> GetOdontologoIdByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Odontólogos activos que pueden firmar un cambio del odontograma, ordenados
    /// por apellidos y luego por nombres.
    /// </summary>
    Task<IReadOnlyList<Odontologo>> GetActiveOdontologosAsync(CancellationToken cancellationToken = default);

    /// <summary>Un odontólogo por su id (solo lectura), para validar la firma.</summary>
    Task<Odontologo?> GetOdontologoAsync(int odontologoId, CancellationToken cancellationToken = default);

    Task<RegistroOdontograma?> GetRecordAsync(int pacienteId, int piezaDentalId, CancellationToken cancellationToken = default);
    Task AddAsync(RegistroOdontograma record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda el registro del odontograma y su auditoría en una sola transacción.
    /// Recibe también el registro para completar el EntidadId real después del
    /// primer SaveChanges, cuando una fila nueva ya tiene Id (ODO-03).
    /// </summary>
    Task SaveChangesWithAuditAsync(
        RegistroOdontograma record,
        RegistroAuditoria audit,
        CancellationToken cancellationToken = default);
}