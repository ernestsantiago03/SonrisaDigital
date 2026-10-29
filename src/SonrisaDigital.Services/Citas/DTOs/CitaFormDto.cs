using SonrisaDigital.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace SonrisaDigital.Services.Citas.DTOs;

public sealed class CitaFormDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El paciente es obligatorio.")]
    public int PacienteId { get; set; }

    [Required(ErrorMessage = "El odontólogo es obligatorio.")]
    public int OdontologoId { get; set; }

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    public DateOnly Fecha { get; set; }

    [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
    public TimeOnly HoraInicio { get; set; }

    [Required(ErrorMessage = "La hora de fin es obligatoria.")]
    public TimeOnly HoraFin { get; set; }

    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    public string? Motivo { get; set; }

    /// <summary>
    /// Estado guardado de la cita. Es solo informativo para que la pantalla decida
    /// si puede abrir el formulario; los cambios de estado se hacen únicamente
    /// con CitaService.SetEstadoAsync.
    /// </summary>
    public EstadoCita EstadoActual { get; init; } = EstadoCita.Programada;
}
