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

    [StringLength(20, ErrorMessage = "El sexo no puede superar los 20 caracteres.")]
    public string? Sexo { get; set; }

    [StringLength(10, ErrorMessage = "El tipo de sangre no puede superar los 10 caracteres.")]
    public string? TipoSangre { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "Introduzca un correo válido.")]
    [StringLength(256, ErrorMessage = "El correo no puede superar los 256 caracteres.")]
    public string? Correo { get; set; }

    [StringLength(300, ErrorMessage = "La dirección no puede superar los 300 caracteres.")]
    public string? Direccion { get; set; }

    [StringLength(200, ErrorMessage = "El nombre del contacto de emergencia no puede superar los 200 caracteres.")]
    public string? ContactoEmergenciaNombre { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono del contacto de emergencia no puede superar los 30 caracteres.")]
    public string? ContactoEmergenciaTelefono { get; set; }

    [StringLength(100, ErrorMessage = "La relación con el contacto de emergencia no puede superar los 100 caracteres.")]
    public string? ContactoEmergenciaRelacion { get; set; }
}
