using System.Security.Claims;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Usuario que está operando en la sesión actual.
/// Se alimenta desde la interfaz (MainLayout) porque en Blazor Server
/// el contexto HTTP no está disponible durante las interacciones por SignalR.
/// </summary>
public interface ICurrentUserService
{
    string? UsuarioId { get; }
    string? Nombre { get; }

    /// <summary>
    /// Identidad completa del usuario autenticado. Los servicios la usan para
    /// evaluar políticas de autorización sin depender del HttpContext de SignalR.
    /// </summary>
    ClaimsPrincipal? Principal { get; }

    /// <summary>
    /// Guarda los datos de la sesión actual. El principal es opcional para no
    /// romper llamadas que solo necesitan el identificador o el nombre.
    /// </summary>
    void Establecer(string? usuarioId, string? nombre, ClaimsPrincipal? principal = null);
}

public sealed class CurrentUserService : ICurrentUserService
{
    public string? UsuarioId { get; private set; }
    public string? Nombre { get; private set; }

    /// <inheritdoc />
    public ClaimsPrincipal? Principal { get; private set; }

    public void Establecer(string? usuarioId, string? nombre, ClaimsPrincipal? principal = null)
    {
        UsuarioId = string.IsNullOrWhiteSpace(usuarioId) ? null : usuarioId;
        Nombre = string.IsNullOrWhiteSpace(nombre) ? null : nombre;

        // Se conserva el principal completo para que IAuthorizationService pueda
        // comprobar los mismos permisos que usa la interfaz.
        Principal = principal;
    }
}
