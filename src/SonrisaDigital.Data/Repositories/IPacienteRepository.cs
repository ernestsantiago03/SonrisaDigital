using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IPacienteRepository
{
    Task<IReadOnlyList<Paciente>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default);

    Task<Paciente?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithIdentificationAsync(string identification, int? excludedId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Paciente paciente, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}