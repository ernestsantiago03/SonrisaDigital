using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public sealed class CitaRepository(ApplicationDbContext dbContext) : ICitaRepository
{
    public async Task<IReadOnlyList<Cita>> SearchAsync(
        DateOnly? fecha = null,
        int? pacienteId = null,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default)
    {
        // Se mantiene sin paginar para el Dashboard y la ficha del paciente, que
        // necesitan el conjunto completo de citas que cumple el filtro.
        return await BuildQuery(fecha, pacienteId, odontologoId, estado)
            .Include(c => c.Paciente)
            .Include(c => c.Odontologo)
            .OrderBy(c => c.Fecha)
            .ThenBy(c => c.HoraInicio)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Página de citas para la tabla principal. Primero cuenta en SQL y después trae
    /// solamente las filas de la página solicitada, con paciente y odontólogo incluidos.
    /// </summary>
    public async Task<PagedResult<Cita>> SearchPagedAsync(
        DateOnly? fecha,
        int? pacienteId,
        int? odontologoId,
        EstadoCita? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = BuildQuery(fecha, pacienteId, odontologoId, estado);

        // El conteo no necesita incluir paciente ni odontólogo.
        var total = await query.CountAsync(cancellationToken);

        // Si la página quedó fuera de rango después de un filtro, se baja a la última válida.
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query
            .Include(c => c.Paciente)
            .Include(c => c.Odontologo)
            .OrderBy(c => c.Fecha)
            .ThenBy(c => c.HoraInicio)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Cita>(items, total, page, pageSize);
    }

    /// <summary>
    /// Arma la consulta base de citas para que la búsqueda normal y la paginada
    /// compartan los mismos filtros.
    /// </summary>
    private IQueryable<Cita> BuildQuery(
        DateOnly? fecha,
        int? pacienteId,
        int? odontologoId,
        EstadoCita? estado)
    {
        IQueryable<Cita> query = dbContext.Citas.AsNoTracking();

        if (fecha.HasValue)
            query = query.Where(c => c.Fecha == fecha.Value);

        if (pacienteId.HasValue)
            query = query.Where(c => c.PacienteId == pacienteId.Value);

        if (odontologoId.HasValue)
            query = query.Where(c => c.OdontologoId == odontologoId.Value);

        if (estado.HasValue)
            query = query.Where(c => c.Estado == estado.Value);

        return query;
    }

    public async Task<Cita?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Odontologo)
            // La consulta clínica que se generó al atender la cita (si ya se atendió): sirve para
            // que la pantalla pueda enlazar a la Historia Clínica.
            .Include(c => c.ConsultaClinica)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<CitaConflicto> BuscarConflictoAsync(
        int odontologoId,
        int pacienteId,
        DateOnly fecha,
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int? excluirCitaId = null,
        CancellationToken cancellationToken = default)
    {
        // Dos citas se cruzan cuando una empieza antes de que termine la otra y termina
        // después de que la otra empiece. Las canceladas y las inasistencias liberan el hueco.
        var query = dbContext.Citas
            .AsNoTracking()
            .Where(cita =>
                cita.Fecha == fecha
                && cita.Estado != EstadoCita.Cancelada
                && cita.Estado != EstadoCita.NoAsistio
                && cita.HoraInicio < horaFin
                && cita.HoraFin > horaInicio
                && (cita.OdontologoId == odontologoId || cita.PacienteId == pacienteId));

        if (excluirCitaId.HasValue)
        {
            query = query.Where(cita => cita.Id != excluirCitaId.Value);
        }

        var citas = await query
            .Select(cita => new { cita.OdontologoId, cita.PacienteId })
            .ToListAsync(cancellationToken);

        return new CitaConflicto(
            citas.Any(cita => cita.OdontologoId == odontologoId),
            citas.Any(cita => cita.PacienteId == pacienteId));
    }

    public async Task<Cita> AddAsync(Cita cita, CancellationToken cancellationToken = default)
    {
        dbContext.Citas.Add(cita);
        await SaveChangesAsync(cancellationToken);
        return cita;
    }

    public async Task<Cita> UpdateAsync(Cita cita, CancellationToken cancellationToken = default)
    {
        cita.FechaModificacion = DateTime.UtcNow;
        dbContext.Citas.Update(cita);
        await SaveChangesAsync(cancellationToken);
        return cita;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
