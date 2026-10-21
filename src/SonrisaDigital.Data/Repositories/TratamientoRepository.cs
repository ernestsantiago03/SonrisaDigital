using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public sealed class TratamientoRepository(ApplicationDbContext dbContext) : ITratamientoRepository
{
    // =====================================================================
    // Catálogo
    // =====================================================================

    public async Task<PagedResult<Tratamiento>> SearchCatalogoAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Tratamiento> query = dbContext.Tratamientos.AsNoTracking();

        if (estado.HasValue)
        {
            query = query.Where(tratamiento => tratamiento.Estado == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(tratamiento =>
                tratamiento.Nombre.Contains(term) ||
                (tratamiento.Descripcion != null && tratamiento.Descripcion.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(tratamiento => tratamiento.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Tratamiento>(items, total, page, pageSize);
    }

    public Task<Tratamiento?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Tratamientos
            .AsNoTracking()
            .FirstOrDefaultAsync(tratamiento => tratamiento.Id == id, cancellationToken);

    public Task<Tratamiento?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Tratamientos
            .FirstOrDefaultAsync(tratamiento => tratamiento.Id == id, cancellationToken);

    public Task<bool> ExistsNombreAsync(
        string nombre,
        int? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Tratamientos
            .AsNoTracking()
            .Where(tratamiento => tratamiento.Nombre == nombre);

        if (excludedId.HasValue)
        {
            query = query.Where(tratamiento => tratamiento.Id != excludedId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tratamiento>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await ProyectarCatalogo(dbContext.Tratamientos)
            .OrderBy(tratamiento => tratamiento.Nombre)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Tratamiento>> GetActivosAsync(CancellationToken cancellationToken = default) =>
        await ProyectarCatalogo(dbContext.Tratamientos.Where(tratamiento => tratamiento.Estado == EstadoRegistro.Activo))
            .OrderBy(tratamiento => tratamiento.Nombre)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Tratamiento tratamiento, CancellationToken cancellationToken = default)
    {
        await dbContext.Tratamientos.AddAsync(tratamiento, cancellationToken);
    }

    // =====================================================================
    // Tratamientos realizados
    // =====================================================================

    public async Task<PagedResult<TratamientoRealizado>> SearchRealizadosAsync(
        string? searchTerm,
        EstadoTratamiento? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<TratamientoRealizado> query = dbContext.TratamientosRealizados.AsNoTracking();

        if (estado.HasValue)
        {
            query = query.Where(tratamiento => tratamiento.Estado == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(tratamiento =>
                tratamiento.Paciente.Nombres.Contains(term) ||
                tratamiento.Paciente.Apellidos.Contains(term) ||
                tratamiento.Tratamiento.Nombre.Contains(term) ||
                tratamiento.Odontologo.Nombres.Contains(term) ||
                tratamiento.Odontologo.Apellidos.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(tratamiento => tratamiento.Paciente)
            .Include(tratamiento => tratamiento.Tratamiento)
            .Include(tratamiento => tratamiento.Odontologo)
            .Include(tratamiento => tratamiento.PiezaDental)
            .Include(tratamiento => tratamiento.Pagos)
            .OrderByDescending(tratamiento => tratamiento.Fecha)
            .ThenByDescending(tratamiento => tratamiento.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TratamientoRealizado>(items, total, page, pageSize);
    }

    public Task<TratamientoRealizado?> GetRealizadoAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.TratamientosRealizados
            .AsNoTracking()
            .Include(tratamiento => tratamiento.Paciente)
            .Include(tratamiento => tratamiento.Tratamiento)
            .Include(tratamiento => tratamiento.Odontologo)
            .Include(tratamiento => tratamiento.PiezaDental)
            .Include(tratamiento => tratamiento.Pagos)
            .FirstOrDefaultAsync(tratamiento => tratamiento.Id == id, cancellationToken);

    public Task<TratamientoRealizado?> GetRealizadoForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.TratamientosRealizados
            .Include(tratamiento => tratamiento.Paciente)
            .Include(tratamiento => tratamiento.Tratamiento)
            .Include(tratamiento => tratamiento.Pagos)
            .FirstOrDefaultAsync(tratamiento => tratamiento.Id == id, cancellationToken);

    public async Task AddRealizadoAsync(TratamientoRealizado tratamiento, CancellationToken cancellationToken = default)
    {
        await dbContext.TratamientosRealizados.AddAsync(tratamiento, cancellationToken);
    }

    // =====================================================================
    // Catálogos de apoyo
    // =====================================================================

    public async Task<IReadOnlyList<Paciente>> GetPacientesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Pacientes
            .AsNoTracking()
            .OrderBy(paciente => paciente.Apellidos)
            .ThenBy(paciente => paciente.Nombres)
            .Select(paciente => new Paciente
            {
                Id = paciente.Id,
                Nombres = paciente.Nombres,
                Apellidos = paciente.Apellidos,
                Estado = paciente.Estado
            })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Odontologo>> GetOdontologosAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Odontologos
            .AsNoTracking()
            .OrderBy(odontologo => odontologo.Apellidos)
            .ThenBy(odontologo => odontologo.Nombres)
            .Select(odontologo => new Odontologo
            {
                Id = odontologo.Id,
                Nombres = odontologo.Nombres,
                Apellidos = odontologo.Apellidos,
                Estado = odontologo.Estado
            })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PiezaDental>> GetPiezasAsync(CancellationToken cancellationToken = default) =>
        await dbContext.PiezasDentales
            .AsNoTracking()
            .OrderBy(pieza => pieza.NumeroFDI)
            .ToListAsync(cancellationToken);

    public Task<Paciente?> GetPacienteAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(paciente => paciente.Id == id, cancellationToken);

    public Task<Odontologo?> GetOdontologoAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Odontologos
            .AsNoTracking()
            .FirstOrDefaultAsync(odontologo => odontologo.Id == id, cancellationToken);

    public Task<PiezaDental?> GetPiezaAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.PiezasDentales
            .AsNoTracking()
            .FirstOrDefaultAsync(pieza => pieza.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    // =====================================================================
    // Ayudantes
    // =====================================================================

    private static IQueryable<Tratamiento> ProyectarCatalogo(IQueryable<Tratamiento> query) =>
        query.AsNoTracking().Select(tratamiento => new Tratamiento
        {
            Id = tratamiento.Id,
            Nombre = tratamiento.Nombre,
            Descripcion = tratamiento.Descripcion,
            PrecioBase = tratamiento.PrecioBase,
            Estado = tratamiento.Estado
        });
}
