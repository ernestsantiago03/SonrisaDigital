using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public sealed class PacienteRepository(ApplicationDbContext dbContext) : IPacienteRepository
{
    public async Task<IReadOnlyList<Paciente>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default)
    {
        // Se mantiene sin paginar para los desplegables que necesitan todos los pacientes.
        return await BuildQuery(searchTerm, estado)
            .OrderBy(patient => patient.Apellidos)
            .ThenBy(patient => patient.Nombres)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Página de pacientes para la tabla principal. El orden coincide con el nombre
    /// completo que ve el usuario y el Id desempata para que la paginación sea estable.
    /// </summary>
    public async Task<PagedResult<Paciente>> SearchPagedAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = BuildQuery(searchTerm, estado);

        // CountAsync se traduce a COUNT en SQL; no se traen las filas para contarlas.
        var total = await query.CountAsync(cancellationToken);

        // Si la página quedó fuera de rango después de un filtro, se baja a la última válida.
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderBy(patient => patient.Nombres)
            .ThenBy(patient => patient.Apellidos)
            .ThenBy(patient => patient.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Paciente>(items, total, page, pageSize);
    }

    /// <summary>
    /// Arma la consulta base de pacientes para que la búsqueda normal y la paginada
    /// compartan exactamente los mismos filtros.
    /// </summary>
    private IQueryable<Paciente> BuildQuery(string? searchTerm, EstadoRegistro? estado)
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

        return query;
    }

    public async Task<int> CountAsync(
        EstadoRegistro? estado = null,
        DateTime? registradosDesdeUtc = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Paciente> query = dbContext.Pacientes.AsNoTracking();

        if (estado.HasValue)
        {
            query = query.Where(patient => patient.Estado == estado.Value);
        }

        if (registradosDesdeUtc.HasValue)
        {
            query = query.Where(patient => patient.FechaRegistro >= registradosDesdeUtc.Value);
        }

        // CountAsync se traduce a un COUNT en SQL; no se cargan los pacientes en memoria.
        return await query.CountAsync(cancellationToken);
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