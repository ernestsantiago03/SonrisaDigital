using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public sealed class AuditoriaRepository(ApplicationDbContext dbContext) : IAuditoriaRepository
{
    public async Task AddAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default)
    {
        await dbContext.RegistrosAuditoria.AddAsync(registro, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RegistroAuditoria>> GetRecientesAsync(int cantidad, CancellationToken cancellationToken = default)
    {
        return await dbContext.RegistrosAuditoria
            .AsNoTracking()
            .Include(registro => registro.Usuario)
                .ThenInclude(usuario => usuario!.Odontologo)
            .OrderByDescending(registro => registro.FechaHora)
            .ThenByDescending(registro => registro.Id)
            .Take(cantidad)
            .ToListAsync(cancellationToken);
    }
}
