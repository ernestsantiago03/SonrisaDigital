using SonrisaDigital.Services.Auditoria.DTOs;

namespace SonrisaDigital.Services.Auditoria;

public interface IAuditoriaService
{
    Task RegistrarAsync(
        string accion,
        string modulo,
        string entidad,
        int? entidadId,
        string descripcion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActividadRecienteDto>> GetRecientesAsync(
        int cantidad = 20,
        CancellationToken cancellationToken = default);
}
