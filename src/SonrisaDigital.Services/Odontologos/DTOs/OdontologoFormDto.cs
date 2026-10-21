using SonrisaDigital.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace SonrisaDigital.Services.Odontologos.DTOs;

public sealed class OdontologoFormDto
{
    public int Id { get; set; }
    public EstadoRegistro Estado { get; set; } = EstadoRegistro.Activo;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(100, ErrorMessage = "Los nombres no pueden superar los 100 caracteres.")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(100, ErrorMessage = "Los apellidos no pueden superar los 100 caracteres.")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "El número de licencia es obligatorio.")]
    [StringLength(50, ErrorMessage = "El número de licencia no puede superar los 50 caracteres.")]
    public string NumeroLicencia { get; set; } = string.Empty;

    [Required(ErrorMessage = "La especialidad es obligatoria.")]
    public string? Especialidad { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "Introduzca un correo válido.")]
    [StringLength(255)]
    public string? Correo { get; set; }
}
