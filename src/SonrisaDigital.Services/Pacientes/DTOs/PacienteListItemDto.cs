using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Pacientes.DTOs;

public sealed record PacienteListItemDto(
    int Id,
    string NombreCompleto,
    string? Identificacion,
    string? TipoSangre,
    string? Telefono,
    string? Correo,
    DateTime FechaRegistro,
    EstadoRegistro Estado);