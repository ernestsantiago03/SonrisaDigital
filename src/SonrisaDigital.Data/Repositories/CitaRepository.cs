using Microsoft.EntityFrameworkCore;
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
        var query = dbContext.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Odontologo)
            .AsNoTracking();

        if (fecha.HasValue)
            query = query.Where(c => c.Fecha == fecha.Value);

        if (pacienteId.HasValue)
            query = query.Where(c => c.PacienteId == pacienteId.Value);

        if (odontologoId.HasValue)
            query = query.Where(c => c.OdontologoId == odontologoId.Value);

        if (estado.HasValue)
            query = query.Where(c => c.Estado == estado.Value);

        return await query
            .OrderBy(c => c.Fecha)
            .ThenBy(c => c.HoraInicio)
            .ToListAsync(cancellationToken);
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
