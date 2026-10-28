using System.ComponentModel.DataAnnotations;

namespace SonrisaDigital.Services.Pacientes.DTOs;

/// <summary>
/// Datos administrativos del paciente. Los datos clínicos y el estado se gestionan
/// con DTOs y permisos separados para evitar que se modifiquen por esta vía.
/// </summary>
public sealed class PacienteFormDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(100, ErrorMessage = "Los nombres no pueden superar los 100 caracteres.")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(100, ErrorMessage = "Los apellidos no pueden superar los 100 caracteres.")]
    public string Apellidos { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "La identificación no puede superar los 50 caracteres.")]
    public string? Identificacion { get; set; }

    public DateOnly? FechaNacimiento { get; set; }

    [StringLength(20)]
    public string? Sexo { get; set; }

    [StringLength(10)]
    public string? TipoSangre { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "Introduzca un correo válido.")]
    [StringLength(256)]
    public string? Correo { get; set; }

    [StringLength(300)]
    public string? Direccion { get; set; }

    [StringLength(200)]
    public string? ContactoEmergenciaNombre { get; set; }

    [StringLength(30)]
    public string? ContactoEmergenciaTelefono { get; set; }

    [StringLength(100)]
    public string? ContactoEmergenciaRelacion { get; set; }
}