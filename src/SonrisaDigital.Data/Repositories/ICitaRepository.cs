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
