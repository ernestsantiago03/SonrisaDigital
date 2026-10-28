using System.ComponentModel.DataAnnotations;

namespace SonrisaDigital.Services.Pacientes.DTOs;

/// <summary>
/// Datos clínicos que se editan desde la ficha del paciente. Se mantienen separados
/// del formulario administrativo para que sus permisos se validen de forma independiente.
/// </summary>
public sealed class PacienteClinicoDto
{
    public int PacienteId { get; set; }

    [StringLength(1000, ErrorMessage = "El detalle de alergias no puede superar los 1000 caracteres.")]
    public string? Alergias { get; set; }

    public bool NoRefiereAlergias { get; set; }
}
