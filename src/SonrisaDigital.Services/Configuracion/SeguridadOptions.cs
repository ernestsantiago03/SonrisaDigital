namespace SonrisaDigital.Services.Configuracion;

public sealed class SeguridadOptions
{
    public const string SectionName = "Seguridad";

    /// <summary>
    /// El Dueño debe activar el segundo factor antes de usar el sistema. Solo puede apagarse en
    /// Development (por ejemplo, para las pruebas E2E); la validación de producción lo rechaza.
    /// </summary>
    public bool MfaDuenoObligatorio { get; set; } = true;
}
