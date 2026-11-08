using Microsoft.AspNetCore.Identity;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Bloqueo persistente de cuentas. Los fallos se guardan en la tabla de usuarios, así que sobreviven
/// a un reinicio de la aplicación. Se usa la misma configuración en la aplicación y en las pruebas.
/// </summary>
public static class PoliticaBloqueoCuentas
{
    public const int IntentosFallidosMaximos = 5;

    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(15);

    public static void Aplicar(LockoutOptions opciones)
    {
        // Explícito: no depender del valor por defecto de Identity para cuentas nuevas.
        opciones.AllowedForNewUsers = true;
        opciones.MaxFailedAccessAttempts = IntentosFallidosMaximos;
        opciones.DefaultLockoutTimeSpan = Duracion;
    }
}
