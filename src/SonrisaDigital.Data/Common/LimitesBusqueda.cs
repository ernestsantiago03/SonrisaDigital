namespace SonrisaDigital.Data.Common;

/// <summary>
/// Máximo de pacientes que devuelve cualquier buscador o selector. Evita cargar toda la
/// base en un desplegable o en un panel de búsqueda; el usuario afina escribiendo más letras.
/// </summary>
public static class LimitesBusqueda
{
    public const int PacientesResultados = 50;
}
