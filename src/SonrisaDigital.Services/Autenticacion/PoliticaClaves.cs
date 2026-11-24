namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Reglas de contraseña comunes a las cuentas de clínica. La cuenta global del
/// Dueño usa además las reglas reforzadas de <see cref="SeguridadDueno"/>.
/// </summary>
public static class PoliticaClaves
{
    /// <summary>Longitud mínima de la contraseña de cualquier cuenta que no sea Dueño.</summary>
    public const int LongitudMinimaGeneral = 12;
}
