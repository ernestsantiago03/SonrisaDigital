using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IHistoriaClinicaRepository
{
    /// <summary>
    /// Pacientes para el buscador de la historia clínica, ordenados alfabéticamente
    /// por nombre completo. Si se indica <paramref name="searchTerm"/> se filtran por
    /// nombres o apellidos.
    /// </summary>
    Task<IReadOnlyList<Paciente>> GetPatientsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
    /// <summary>Datos generales de un paciente por su id (consulta de solo lectura).</summary>
    Task<Paciente?> GetPatientAsync(int pacienteId, CancellationToken cancellationToken = default);

    /// <summary>Historia clínica del paciente, o null si todavía no tiene ninguna.</summary>
    Task<HistoriaClinica?> GetByPatientAsync(int pacienteId, CancellationToken cancellationToken = default);

    /// <summary>Consultas del paciente, de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<ConsultaClinica>> GetConsultasAsync(int pacienteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Una consulta clínica por su id. Se usa para cerrarla (cambiarle el estado), por eso
    /// trae la fila "rastreada" (sin AsNoTracking) para poder modificarla.
    /// </summary>
    Task<ConsultaClinica?> GetConsultaAsync(int consultaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// La consulta clínica que ya está ligada a una cita, o null si esa cita todavía no tiene
    /// consulta. Sirve para no crear dos consultas para la misma cita (una cita atendida
    /// genera una sola consulta).
    /// </summary>
    Task<ConsultaClinica?> GetConsultaPorCitaAsync(int citaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// El id del odontólogo que corresponde a un usuario del sistema. Sirve para preseleccionar
    /// al profesional en "+ Nueva consulta". Devuelve null si el usuario no es odontólogo.
    /// </summary>
    Task<int?> GetOdontologoIdByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Odontólogos activos para elegir quién atendió al paciente, ordenados por apellidos y
    /// después por nombres.
    /// </summary>
    Task<IReadOnlyList<Odontologo>> GetOdontologosAsync(CancellationToken cancellationToken = default);

    /// <summary>Un odontólogo por su id (para comprobar que el elegido existe).</summary>
    Task<Odontologo?> GetOdontologoAsync(int odontologoId, CancellationToken cancellationToken = default);

    /// <summary>Prepara una historia clínica nueva para insertarla en la base de datos.</summary>
    Task AddAsync(HistoriaClinica historia, CancellationToken cancellationToken = default);

    /// <summary>Prepara una consulta clínica nueva para insertarla en la base de datos.</summary>
    Task AddConsultaAsync(ConsultaClinica consulta, CancellationToken cancellationToken = default);

    /// <summary>Aplica en la base de datos todos los cambios pendientes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
