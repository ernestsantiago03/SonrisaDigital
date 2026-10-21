using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Pacientes.DTOs;

namespace SonrisaDigital.Services.Pacientes;

public interface IPacienteService
{
    Task<IReadOnlyList<PacienteListItemDto>> SearchAsync(string? searchTerm, EstadoRegistro? estado, CancellationToken cancellationToken = default);
    Task<PacienteDetalleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PacienteFormDto?> GetFormAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(PacienteFormDto model, CancellationToken cancellationToken = default);
    Task UpdateAsync(PacienteFormDto model, CancellationToken cancellationToken = default);
    Task SetStatusAsync(int id, EstadoRegistro estado, CancellationToken cancellationToken = default);
}