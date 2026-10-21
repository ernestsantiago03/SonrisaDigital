namespace SonrisaDigital.Services.Autorizacion;

/// <summary>
/// Nombres canónicos de los roles del sistema (USU-03). Deben usarse siempre
/// estas constantes en lugar de escribir el nombre del rol como texto literal,
/// para que un cambio de nombre se haga en un solo lugar y el compilador avise
/// si algo queda desalineado.
/// </summary>
public static class RolesSonrisaDigital
{
    public const string Administrador = "Administrador";
    public const string Recepcionista = "Recepcionista";
    public const string Odontologo = "Odontólogo";
}
