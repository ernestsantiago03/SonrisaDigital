using System.Globalization;
using Microsoft.AspNetCore.Authentication;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Reglas reforzadas para la cuenta del rol Dueño: contraseña larga, sesión corta y un mínimo de
/// Dueños activos para que nunca quede el sistema sin quien lo administre.
/// </summary>
public static class SeguridadDueno
{
    public const int LongitudMinimaClave = 16;

    /// <summary>Duración máxima de una sesión del Dueño, desde que inició sesión.</summary>
    public static readonly TimeSpan DuracionSesion = TimeSpan.FromHours(2);

    /// <summary>Número de Dueños activos que siempre deben existir.</summary>
    public const int DuenosActivosMinimos = 2;

    private const string ClaveInicioSesion = "sd.inicio-sesion";

    /// <summary>Devuelve el motivo si la contraseña no cumple la regla del Dueño; null si la cumple.</summary>
    public static string? ValidarClave(string? clave) =>
        string.IsNullOrEmpty(clave) || clave.Length < LongitudMinimaClave
            ? $"La contraseña del Dueño debe tener al menos {LongitudMinimaClave} caracteres."
            : null;

    /// <summary>
    /// Guarda el momento del inicio de sesión dentro de la cookie. Se usa una propiedad propia en
    /// lugar de IssuedUtc porque la validación de seguridad renueva la cookie en cada petición.
    /// </summary>
    public static void MarcarInicioSesion(AuthenticationProperties propiedades, DateTimeOffset ahora) =>
        propiedades.SetString(ClaveInicioSesion, ahora.ToString("O", CultureInfo.InvariantCulture));

    /// <summary>True si la sesión superó la duración permitida. Sin marca de inicio, se considera vencida.</summary>
    public static bool SesionVencida(AuthenticationProperties? propiedades, DateTimeOffset ahora)
    {
        var texto = propiedades?.GetString(ClaveInicioSesion);
        if (!DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var inicio))
        {
            return true;
        }

        return ahora - inicio > DuracionSesion;
    }

    /// <summary>Un Dueño activo solo puede desactivarse si, después, siguen quedando los mínimos.</summary>
    public static bool PuedeDesactivarse(int duenosActivos) => duenosActivos - 1 >= DuenosActivosMinimos;
}
