using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IOdontologoRepository
{
    Task<IReadOnlyList<Odontologo>> SearchAsync(string? searchTerm, EstadoRegistro? estado, CancellationToken cancellationToken = default);
    Task<Odontologo?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Odontologo odontologo, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
