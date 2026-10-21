using System.ComponentModel.DataAnnotations;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Odontologos.DTOs;

namespace SonrisaDigital.Services.Usuarios.DTOs;

public sealed class UsuarioFormDto
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Introduzca un correo válido.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres.")]
    public string? Password { get; set; }

    public EstadoRegistro Estado { get; set; } = EstadoRegistro.Activo;
    public List<string> Roles { get; set; } = [];
    public OdontologoFormDto? PerfilOdontologo { get; set; }
}