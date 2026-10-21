using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Citas.DTOs;

public sealed record CitaDetalleDto(
    int Id,
    string PacienteNombre,
    int PacienteId,
    string OdontologoNombre,
    int OdontologoId,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    string? Motivo,
    EstadoCita Estado,
    DateTime FechaCreacion,
    int? ConsultaId);
