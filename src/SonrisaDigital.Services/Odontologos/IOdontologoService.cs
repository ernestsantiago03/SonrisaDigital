using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Odontologos.DTOs;

namespace SonrisaDigital.Services.Odontologos;

public interface IOdontologoService
{
    Task<IReadOnlyList<OdontologoListItemDto>> SearchAsync(string? searchTerm, EstadoRegistro? estado, CancellationToken cancellationToken = default);
    Task<OdontologoDetalleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<OdontologoFormDto?> GetFormAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(OdontologoFormDto model, CancellationToken cancellationToken = default);
    Task UpdateAsync(OdontologoFormDto model, CancellationToken cancellationToken = default);
    Task SetStatusAsync(int id, EstadoRegistro estado, CancellationToken cancellationToken = default);
}
