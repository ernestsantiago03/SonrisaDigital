using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public sealed class PacienteRepository(ApplicationDbContext dbContext) : IPacienteRepository
{
    public async Task<IReadOnlyList<Paciente>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Paciente> query = dbContext.Pacientes.AsNoTracking();

        if (estado.HasValue)
        {
            query = query.Where(patient => patient.Estado == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedTerm = searchTerm.Trim();
            query = query.Where(patient =>
                patient.Nombres.Contains(normalizedTerm) ||
                patient.Apellidos.Contains(normalizedTerm) ||
                (patient.Identificacion != null && patient.Identificacion.Contains(normalizedTerm)) ||
                (patient.Telefono != null && patient.Telefono.Contains(normalizedTerm)));
        }

        return await query
            .OrderBy(patient => patient.Apellidos)
            .ThenBy(patient => patient.Nombres)
            .ToListAsync(cancellationToken);
    }

    public Task<Paciente?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Pacientes
            .Include(patient => patient.HistoriaClinica)
            .FirstOrDefaultAsync(patient => patient.Id == id, cancellationToken);

    public Task<bool> ExistsWithIdentificationAsync(
        string identification,
        int? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Pacientes
            .AsNoTracking()
            .Where(patient => patient.Identificacion == identification);

        if (excludedId.HasValue)
        {
            query = query.Where(patient => patient.Id != excludedId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Paciente paciente, CancellationToken cancellationToken = default)
    {
        await dbContext.Pacientes.AddAsync(paciente, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}