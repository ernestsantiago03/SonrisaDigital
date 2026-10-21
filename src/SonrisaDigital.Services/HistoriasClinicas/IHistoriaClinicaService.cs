using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.HistoriasClinicas.DTOs;

namespace SonrisaDigital.Services.HistoriasClinicas;

public interface IHistoriaClinicaService
{
    /// <summary>
    /// Pacientes para el buscador, ordenados alfabéticamente por nombre completo.
    /// Si se indica <paramref name="searchTerm"/> se filtran por nombres o apellidos.
    /// </summary>
    Task<IReadOnlyList<HistoriaClinicaPacienteDto>> GetPatientsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Historia clínica completa de un paciente: sus datos generales, los datos clínicos
    /// (alergias, enfermedades, antecedentes...) y sus consultas. Devuelve null si el
    /// paciente no existe.
    /// </summary>
    Task<HistoriaClinicaDto?> GetByPatientAsync(int pacienteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda los datos clínicos de la historia clínica del paciente.
    /// </summary>
    Task SaveAsync(HistoriaClinicaDto model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Odontólogos activos para elegir quién atendió al paciente (para la lista desplegable de
    /// "+ Nueva consulta"), ordenados por nombre.
    /// </summary>
    Task<IReadOnlyList<OdontologoOptionDto>> GetOdontologosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// El odontólogo ligado al usuario de la sesión, o null si su usuario no es odontólogo.
    /// Sirve para dejar preseleccionado al profesional en "+ Nueva consulta".
    /// </summary>
    Task<int?> GetOdontologoVinculadoAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra una consulta clínica del paciente. Si el paciente todavía no tenía historia
    /// clínica, la crea (la consulta es un dato clínico que la necesita) y deja el movimiento
    /// anotado en la auditoría. El acceso lo decide el permiso del rol (HistoriaClinica.Crear);
    /// el odontólogo que atendió se elige en la ventana.
    /// </summary>
    Task CreateConsultaAsync(ConsultaClinicaFormDto model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completa o corrige una consulta que todavía está Abierta (por ejemplo la que se creó
    /// automáticamente al atender una cita, a la que le falta el diagnóstico). Una consulta
    /// cerrada es de solo lectura y no se puede modificar. Deja el cambio en la auditoría.
    /// </summary>
    Task UpdateConsultaAsync(ConsultaClinicaFormDto model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puente entre Citas e Historia Clínica: registra la consulta clínica de una cita que se
    /// acaba de atender. La consulta hereda el paciente, el odontólogo, la fecha y el motivo de
    /// la cita, nace Abierta y queda ligada a esa cita (una sola consulta por cita). Si el
    /// paciente no tenía historia clínica, se crea. Devuelve el id de la consulta.
    /// </summary>
    Task<int> RegistrarConsultaDeCitaAsync(Cita cita, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cierra una consulta clínica (pasa de Abierta a Cerrada). Una vez cerrada queda de solo
    /// lectura. Deja el cambio anotado en la auditoría.
    /// </summary>
    Task CerrarConsultaAsync(int consultaId, CancellationToken cancellationToken = default);
}
