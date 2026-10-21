using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Pacientes.DTOs;

public sealed record PacienteDetalleDto(
    int Id,
    string Nombres,
    string Apellidos,
    string? Identificacion,
    DateOnly? FechaNacimiento,
    string? Sexo,
    string? TipoSangre,
    string? Telefono,
    string? Correo,
    string? Direccion,
    string? ContactoEmergenciaNombre,
    string? ContactoEmergenciaTelefono,
    string? ContactoEmergenciaRelacion,
    DateTime FechaRegistro,
    EstadoRegistro Estado);