using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

/// <summary>
/// Implementación de las consultas de Reportes. Todas las consultas son de solo
/// lectura (AsNoTracking) y reutilizan las entidades existentes; el módulo no
/// tiene tablas propias.
/// </summary>
public sealed class ReporteRepository(ApplicationDbContext dbContext) : IReporteRepository
{
    public async Task<IReadOnlyList<Cita>> GetCitasAsync(
        DateOnly desde,
        DateOnly hasta,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Citas
            .AsNoTracking()
            .Include(cita => cita.Paciente)
            .Include(cita => cita.Odontologo)
            .Where(cita => cita.Fecha >= desde && cita.Fecha <= hasta);

        if (odontologoId.HasValue)
        {
            query = query.Where(cita => cita.OdontologoId == odontologoId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(cita => cita.Estado == estado.Value);
        }

        return await query
            .OrderBy(cita => cita.Fecha)
            .ThenBy(cita => cita.HoraInicio)
            .ThenBy(cita => cita.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Cita>> GetCitasAtendidasAsync(
        DateOnly desde,
        DateOnly hasta,
        int? odontologoId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Citas
            .AsNoTracking()
            .Include(cita => cita.Paciente)
            .Where(cita => cita.Estado == EstadoCita.Atendida
                && cita.Fecha >= desde
                && cita.Fecha <= hasta);

        if (odontologoId.HasValue)
        {
            query = query.Where(cita => cita.OdontologoId == odontologoId.Value);
        }

        return await query
            .OrderBy(cita => cita.Fecha)
            .ThenBy(cita => cita.HoraInicio)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountPacientesAsync(
        EstadoRegistro? estado = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Pacientes.AsNoTracking();

        if (estado.HasValue)
        {
            query = query.Where(paciente => paciente.Estado == estado.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public Task<int> CountPacientesRegistradosAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        CancellationToken cancellationToken = default) =>
        dbContext.Pacientes
            .AsNoTracking()
            .CountAsync(
                paciente => paciente.FechaRegistro >= desdeUtc
                    && paciente.FechaRegistro < hastaUtcExclusivo,
                cancellationToken);

    public async Task<IReadOnlyList<TratamientoRealizado>> GetTratamientosAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? odontologoId = null,
        EstadoTratamiento? estado = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.TratamientosRealizados
            .AsNoTracking()
            .Include(tratamiento => tratamiento.Paciente)
            .Include(tratamiento => tratamiento.Tratamiento)
            .Include(tratamiento => tratamiento.Odontologo)
            .Include(tratamiento => tratamiento.Pagos)
            .Where(tratamiento => tratamiento.Fecha >= desdeUtc
                && tratamiento.Fecha < hastaUtcExclusivo);

        if (odontologoId.HasValue)
        {
            query = query.Where(tratamiento => tratamiento.OdontologoId == odontologoId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(tratamiento => tratamiento.Estado == estado.Value);
        }

        return await query
            .OrderByDescending(tratamiento => tratamiento.Fecha)
            .ThenByDescending(tratamiento => tratamiento.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TratamientoRealizado>> GetTratamientosConSaldoAsync(
        int? odontologoId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.TratamientosRealizados
            .AsNoTracking()
            // Solo los tratamientos con precio pueden tener saldo pendiente.
            .Where(tratamiento => tratamiento.Precio > 0);

        if (odontologoId.HasValue)
        {
            query = query.Where(tratamiento => tratamiento.OdontologoId == odontologoId.Value);
        }

        return await query
            .Include(tratamiento => tratamiento.Pagos)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Pago>> GetPagosAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? odontologoId = null,
        MetodoPago? metodoPago = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Pagos
            .AsNoTracking()
            .Include(pago => pago.TratamientoRealizado)
                .ThenInclude(tratamiento => tratamiento.Paciente)
            .Include(pago => pago.TratamientoRealizado)
                .ThenInclude(tratamiento => tratamiento.Tratamiento)
            .Include(pago => pago.Usuario)
                .ThenInclude(usuario => usuario!.Odontologo)
            .Where(pago => pago.Fecha >= desdeUtc && pago.Fecha < hastaUtcExclusivo);

        // El pago no guarda odontólogo: se filtra por el tratamiento cobrado.
        if (odontologoId.HasValue)
        {
            query = query.Where(pago =>
                pago.TratamientoRealizado.OdontologoId == odontologoId.Value);
        }

        if (metodoPago.HasValue)
        {
            query = query.Where(pago => pago.MetodoPago == metodoPago.Value);
        }

        return await query
            .OrderByDescending(pago => pago.Fecha)
            .ThenByDescending(pago => pago.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Odontologo>> GetOdontologosAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Odontologos
            .AsNoTracking()
            .OrderBy(odontologo => odontologo.Apellidos)
            .ThenBy(odontologo => odontologo.Nombres)
            .ToListAsync(cancellationToken);

    public Task<int?> GetOdontologoIdByUserAsync(
        string usuarioId,
        CancellationToken cancellationToken = default) =>
        dbContext.Odontologos
            .AsNoTracking()
            .Where(odontologo => odontologo.UsuarioId == usuarioId)
            .Select(odontologo => (int?)odontologo.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
