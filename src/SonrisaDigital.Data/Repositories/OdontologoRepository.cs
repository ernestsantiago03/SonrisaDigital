using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public sealed class OdontologoRepository(ApplicationDbContext dbContext) : IOdontologoRepository
{
    public async Task<IReadOnlyList<Odontologo>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Odontologo> query = dbContext.Odontologos.AsNoTracking();

        if (estado.HasValue)
        {
            query = query.Where(odontologo => odontologo.Estado == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedTerm = searchTerm.Trim();
            query = query.Where(odontologo =>
                odontologo.Nombres.Contains(normalizedTerm) ||
                odontologo.Apellidos.Contains(normalizedTerm) ||
                (odontologo.NumeroLicencia != null && odontologo.NumeroLicencia.Contains(normalizedTerm)) ||
                (odontologo.Especialidad != null && odontologo.Especialidad.Contains(normalizedTerm)) ||
                (odontologo.Telefono != null && odontologo.Telefono.Contains(normalizedTerm)));
        }

        return await query
            .OrderBy(odontologo => odontologo.Apellidos)
            .ThenBy(odontologo => odontologo.Nombres)
            .ToListAsync(cancellationToken);
    }

    public Task<Odontologo?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Odontologos
            .FirstOrDefaultAsync(odontologo => odontologo.Id == id, cancellationToken);

    public async Task AddAsync(Odontologo odontologo, CancellationToken cancellationToken = default)
    {
        await dbContext.Odontologos.AddAsync(odontologo, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
