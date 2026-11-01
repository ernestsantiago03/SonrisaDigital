using System.ComponentModel.DataAnnotations;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.HistoriasClinicas.DTOs;

/// <summary>
/// Un paciente dentro de la lista de resultados de la búsqueda de Historia Clínica:
/// su id, el nombre completo, si está activo (para mostrar la etiqueta "Inactivo")
/// y su cédula.
/// </summary>
public sealed record HistoriaClinicaPacienteDto(
    int Id,
    string NombreCompleto,
    bool Activo,
    string? Identificacion);

/// <summary>
/// Historia clínica de un paciente. Mezcla datos administrativos que viven en la tabla de
/// pacientes (nombre, cédula, fecha de nacimiento, teléfono, tipo de sangre) con los datos
/// clínicos que tienen su propia tabla (alergias, enfermedades, antecedentes, hábitos) y la
/// lista de consultas atendidas.
/// </summary>
public sealed class HistoriaClinicaDto
{
    public int PacienteId { get; init; }
    public string PacienteNombre { get; init; } = string.Empty;
    public string? Identificacion { get; init; }
    public DateOnly? FechaNacimiento { get; init; }
    public string? Telefono { get; init; }
    public string? TipoSangre { get; init; }
    public bool PacienteActivo { get; init; }

    /// <summary>False mientras el paciente no tenga ningún dato clínico registrado.</summary>
    public bool TieneHistoria { get; init; }

    public DateTime? FechaCreacion { get; init; }
    public DateTime? FechaModificacion { get; init; }

    public IReadOnlyList<ConsultaClinicaItemDto> Consultas { get; init; } = [];

    [StringLength(1000, ErrorMessage = "Las alergias no pueden superar los 1000 caracteres.")]
    public string? Alergias { get; set; }

    [StringLength(1000, ErrorMessage = "Las enfermedades no pueden superar los 1000 caracteres.")]
    public string? Enfermedades { get; set; }

    [StringLength(1000, ErrorMessage = "Los medicamentos no pueden superar los 1000 caracteres.")]
    public string? Medicamentos { get; set; }

    [StringLength(1000, ErrorMessage = "Los antecedentes médicos no pueden superar los 1000 caracteres.")]
    public string? AntecedentesMedicos { get; set; }

    [StringLength(1000, ErrorMessage = "Los antecedentes odontológicos no pueden superar los 1000 caracteres.")]
    public string? AntecedentesOdontologicos { get; set; }

    [StringLength(1000, ErrorMessage = "Los hábitos no pueden superar los 1000 caracteres.")]
    public string? Habitos { get; set; }

    [StringLength(2000, ErrorMessage = "Las observaciones no pueden superar los 2000 caracteres.")]
    public string? Observaciones { get; set; }
}

/// <summary>
/// Una consulta clínica del paciente: cuándo fue, qué odontólogo la atendió y lo que se
/// anotó (motivo, diagnóstico, plan de tratamiento y observaciones).
/// </summary>
public sealed record ConsultaClinicaItemDto(
    int Id,
    DateTime Fecha,
    int OdontologoId,
    string Odontologo,
    string? Motivo,
    string? Diagnostico,
    string? PlanTratamiento,
    string? Observaciones,
    int? CitaId,
    EstadoConsulta Estado);

/// <summary>
/// Datos que el usuario escribe en la ventana "+ Nueva consulta": es lo que viaja de la
/// pantalla al servicio para crear una consulta clínica.
///
/// Las reglas marcadas con [Required] y [StringLength] las revisa el formulario ANTES de
/// enviar nada (mensajes en rojo debajo de cada campo). Además, el servicio vuelve a
/// validar lo esencial: las reglas de negocio deben vivir fuera de la interfaz.
/// </summary>
public sealed class ConsultaClinicaFormDto
{
    /// <summary>
    /// 0 = es una consulta nueva (se registra desde cero). Un número mayor que 0 = se está
    /// completando o corrigiendo la consulta con ese id (solo se permite mientras siga Abierta).
    /// </summary>
    public int Id { get; set; }

    /// <summary>Paciente al que pertenece la consulta (el de la historia que se está viendo).</summary>
    public int PacienteId { get; set; }

    /// <summary>
    /// Odontólogo que atendió al paciente: es obligatorio, porque toda consulta queda firmada por
    /// un profesional. La ventana muestra una lista con los odontólogos activos y la deja
    /// preseleccionada si el usuario de la sesión es odontólogo.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el odontólogo que atendió al paciente.")]
    public int OdontologoId { get; set; }

    /// <summary>Fecha en que se atendió al paciente. Por defecto, hoy.</summary>
    public DateTime Fecha { get; set; } = DateTime.Today;

    /// <summary>Motivo de la consulta (obligatorio): qué trajo al paciente a la clínica.</summary>
    [Required(ErrorMessage = "El motivo de la consulta es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar los 300 caracteres.")]
    public string? Motivo { get; set; }

    /// <summary>Diagnóstico (obligatorio): qué encontró el odontólogo.</summary>
    [Required(ErrorMessage = "El diagnóstico es obligatorio.")]
    [StringLength(1000, ErrorMessage = "El diagnóstico no puede superar los 1000 caracteres.")]
    public string? Diagnostico { get; set; }

    /// <summary>Plan de tratamiento propuesto (opcional).</summary>
    [StringLength(1000, ErrorMessage = "El plan de tratamiento no puede superar los 1000 caracteres.")]
    public string? PlanTratamiento { get; set; }

    /// <summary>Notas adicionales de la consulta (opcional).</summary>
    [StringLength(1000, ErrorMessage = "Las observaciones no pueden superar los 1000 caracteres.")]
    public string? Observaciones { get; set; }
}

/// <summary>
/// Un odontólogo dentro de la lista desplegable de "+ Nueva consulta": solo lo necesario para
/// pintar la opción (su id y su nombre completo).
/// </summary>
public sealed record OdontologoOptionDto(int Id, string Nombre);
