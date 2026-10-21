using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IAuditoriaRepository
{
    Task AddAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegistroAuditoria>> GetRecientesAsync(int cantidad, CancellationToken cancellationToken = default);
}
