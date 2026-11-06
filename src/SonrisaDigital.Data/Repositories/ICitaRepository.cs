using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface ICitaRepository
{
    Task<IReadOnlyList<Cita>> SearchAsync(
        DateOnly? fecha = null,
        int? pacienteId = null,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Citas dentro de un rango de fechas local [desde, hasta], con paciente y
    /// odontólogo incluidos. La usa el panel de próximas citas del Inicio.
    /// </summary>
    Task<IReadOnlyList<Cita>> SearchRangoAsync(
        DateOnly desde,
        DateOnly hasta,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve una página de citas y el total que cumple los filtros. El conteo y el
    /// recorte se hacen en SQL para no cargar todas las citas en memoria.
    /// </summary>
    Task<PagedResult<Cita>> SearchPagedAsync(
        DateOnly? fecha,
        int? pacienteId,
        int? odontologoId,
        EstadoCita? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Cita?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revisa si el odontólogo o el paciente ya tienen una cita activa que se cruce
    /// con el horario indicado. Al editar, se puede excluir la cita actual.
    /// </summary>
    Task<CitaConflicto> BuscarConflictoAsync(
        int odontologoId,
        int pacienteId,
        DateOnly fecha,
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int? excluirCitaId = null,
        CancellationToken cancellationToken = default);

    Task<Cita> AddAsync(Cita cita, CancellationToken cancellationToken = default);
    Task<Cita> UpdateAsync(Cita cita, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
