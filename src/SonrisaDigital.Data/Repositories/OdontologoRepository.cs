using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public sealed class OdontologoRepository(ApplicationDbContext dbContext) : IOdontologoRepository
{
    public async Task<IReadOnlyList<Odontologo>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default)
    {
        // Se mantiene sin paginar para el desplegable de "Nueva cita", que necesita
        // todos los odontólogos activos.
        return await BuildQuery(searchTerm, estado)
            .OrderBy(odontologo => odontologo.Apellidos)
            .ThenBy(odontologo => odontologo.Nombres)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Página de odontólogos para la tabla principal. El orden coincide con el nombre
    /// completo que ve el usuario y el Id desempata para que la paginación sea estable.
    /// </summary>
    public async Task<PagedResult<Odontologo>> SearchPagedAsync(
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
            .OrderBy(odontologo => odontologo.Nombres)
            .ThenBy(odontologo => odontologo.Apellidos)
            .ThenBy(odontologo => odontologo.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Odontologo>(items, total, page, pageSize);
    }

    /// <summary>
    /// Arma la consulta base de odontólogos para que la búsqueda normal y la paginada
    /// compartan exactamente los mismos filtros.
    /// </summary>
    private IQueryable<Odontologo> BuildQuery(string? searchTerm, EstadoRegistro? estado)
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

        return query;
    }

    public async Task<int> CountAsync(
        EstadoRegistro? estado = null,
        DateTime? registradosDesdeUtc = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Odontologo> query = dbContext.Odontologos.AsNoTracking();

        if (estado.HasValue)
        {
            query = query.Where(odontologo => odontologo.Estado == estado.Value);
        }

        if (registradosDesdeUtc.HasValue)
        {
            query = query.Where(odontologo => odontologo.FechaRegistro >= registradosDesdeUtc.Value);
        }

        // CountAsync se traduce a un COUNT en SQL; no se cargan los odontólogos en memoria.
        return await query.CountAsync(cancellationToken);
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
