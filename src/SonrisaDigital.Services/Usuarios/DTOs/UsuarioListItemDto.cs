using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Usuarios.DTOs;

public sealed record UsuarioListItemDto(
    string Id,
    string Nombre,
    string Email,
    EstadoRegistro Estado,
    DateTime FechaCreacion,
    IReadOnlyList<string> Roles);