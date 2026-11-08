using System.Security.Claims;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Marca la sesión que superó el segundo factor. Solo la agrega el segundo paso del login
/// (/account/login-2fa); una sesión abierta únicamente con contraseña no la tiene y debe
/// volver a autenticarse antes de usar el sistema.
/// </summary>
public static class ReclamacionesSegundoFactor
{
    public const string Tipo = "sd.segundo-factor";
    public const string ValorVerificado = "verificado";

    public static Claim Crear() => new(Tipo, ValorVerificado);

    public static bool Verificada(ClaimsPrincipal principal) =>
        principal.HasClaim(Tipo, ValorVerificado);
}
