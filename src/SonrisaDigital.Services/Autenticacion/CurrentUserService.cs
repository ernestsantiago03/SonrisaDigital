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

    void Establecer(string? usuarioId, string? nombre);
}

public sealed class CurrentUserService : ICurrentUserService
{
    public string? UsuarioId { get; private set; }
    public string? Nombre { get; private set; }

    public void Establecer(string? usuarioId, string? nombre)
    {
        UsuarioId = string.IsNullOrWhiteSpace(usuarioId) ? null : usuarioId;
        Nombre = string.IsNullOrWhiteSpace(nombre) ? null : nombre;
    }
}
