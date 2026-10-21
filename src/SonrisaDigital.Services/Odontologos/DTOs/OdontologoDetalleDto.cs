using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Odontologos.DTOs;

public sealed record OdontologoDetalleDto(
    int Id,
    string Nombres,
    string Apellidos,
    string? NumeroLicencia,
    string? Especialidad,
    string? Telefono,
    string? Correo,
    EstadoRegistro Estado);
