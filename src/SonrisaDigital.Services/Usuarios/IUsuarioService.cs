using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Usuarios.DTOs;

namespace SonrisaDigital.Services.Usuarios;

public interface IUsuarioService
{
    /// <summary>
    /// Página de usuarios para la tabla principal. Devuelve también el total que
    /// cumple el filtro para poder mostrar la paginación.
    /// </summary>
    Task<PagedResult<UsuarioListItemDto>> SearchPagedAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<UsuarioFormDto?> GetFormAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clínica de la sesión actual, o <c>null</c> si el Dueño todavía no eligió una.
    /// Es la única fuente para asignar usuarios: no se acepta una clínica del formulario.
    /// </summary>
    Task<ClinicaActivaDto?> GetClinicaActualAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(UsuarioFormDto model, CancellationToken cancellationToken = default);
    Task UpdateAsync(UsuarioFormDto model, CancellationToken cancellationToken = default);
    Task SetStatusAsync(string id, EstadoRegistro estado, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(string id, string password, CancellationToken cancellationToken = default);
}