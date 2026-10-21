using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Odontograma.DTOs;

public sealed record OdontogramaPacienteDto(int Id, string NombreCompleto);

public sealed class OdontogramaPiezaDto
{
    public int Id { get; init; }
    public string NumeroFDI { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public EstadoOdontograma Estado { get; set; } = EstadoOdontograma.Sano;
    public string? Observaciones { get; set; }
    public DateTime? FechaModificacion { get; init; }
    public string? Odontologo { get; init; }
}