namespace SonrisaDigital.Services.Auditoria.DTOs;

public sealed record ActividadRecienteDto(
    long Id,
    string Usuario,
    string Accion,
    string Modulo,
    string Entidad,
    int? EntidadId,
    string? Descripcion,
    DateTime FechaHora);
