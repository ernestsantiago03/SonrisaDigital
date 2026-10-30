using System.ComponentModel.DataAnnotations;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Clinicas.DTOs;

public sealed record ClinicaListItemDto(
    int Id,
    string Nombre,
    string Codigo,
    EstadoClinica Estado,
    DateTime FechaAlta);

public sealed class ClinicaFormDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre de la clínica es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El código de la clínica es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar los 30 caracteres.")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "El código solo puede tener letras minúsculas, números y guiones.")]
    public string Codigo { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "El RNC no puede superar los 30 caracteres.")]
    public string? Rnc { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "Introduzca un correo válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
    public string? Correo { get; set; }

    [StringLength(300, ErrorMessage = "La dirección no puede superar los 300 caracteres.")]
    public string? Direccion { get; set; }

    [StringLength(150, ErrorMessage = "El horario no puede superar los 150 caracteres.")]
    public string? Horario { get; set; }

    [StringLength(300, ErrorMessage = "El eslogan no puede superar los 300 caracteres.")]
    public string? Eslogan { get; set; }
}

/// <summary>Primer administrador de una clínica nueva, creado por el Dueño.</summary>
public sealed class AdministradorClinicaFormDto
{
    [Required(ErrorMessage = "El nombre del administrador es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo del administrador es obligatorio.")]
    [EmailAddress(ErrorMessage = "Introduzca un correo válido.")]
    [StringLength(256, ErrorMessage = "El correo no puede superar los 256 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña inicial es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres.")]
    public string Password { get; set; } = string.Empty;
}
