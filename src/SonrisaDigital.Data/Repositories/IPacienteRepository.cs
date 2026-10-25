using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IPacienteRepository
{
    Task<IReadOnlyList<Paciente>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta pacientes sin traer las filas completas. El filtro por fecha usa la fecha
    /// de registro en UTC, que es como se guarda en la base de datos.
    /// </summary>
    /// <param name="estado">Si se indica, cuenta solo los pacientes en ese estado.</param>
    /// <param name="registradosDesdeUtc">Si se indica, cuenta solo los registrados desde esa fecha UTC.</param>
    /// <param name="cancellationToken">Token para cancelar la consulta.</param>
    Task<int> CountAsync(
        EstadoRegistro? estado = null,
        DateTime? registradosDesdeUtc = null,
        CancellationToken cancellationToken = default);

    Task<Paciente?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithIdentificationAsync(string identification, int? excludedId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Paciente paciente, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}