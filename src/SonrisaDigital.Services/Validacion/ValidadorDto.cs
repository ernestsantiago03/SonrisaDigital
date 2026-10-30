using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SonrisaDigital.Services.Validacion;

/// <summary>
/// Aplica al servicio las mismas anotaciones que el formulario usa ([Required],
/// [StringLength], [Range], [EmailAddress]). Así una llamada que no viene de la
/// pantalla no puede guardar datos que el formulario habría rechazado.
/// </summary>
public static class ValidadorDto
{
    // Las contraseñas no se recortan: un espacio al inicio o al final forma parte
    // de la contraseña que el usuario escribió.
    private static readonly HashSet<string> PropiedadesSinRecorte = new(StringComparer.Ordinal)
    {
        "Password"
    };

    /// <summary>
    /// Recorta los textos del modelo y lo valida. Lanza InvalidOperationException con
    /// el primer mensaje de error, que ya está redactado para el usuario.
    /// </summary>
    public static void PrepararYValidar(object modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        RecortarCadenas(modelo);

        var errores = new List<ValidationResult>();
        var contexto = new ValidationContext(modelo);
        if (!Validator.TryValidateObject(modelo, contexto, errores, validateAllProperties: true))
        {
            throw new InvalidOperationException(
                errores[0].ErrorMessage ?? "Hay datos no válidos. Revisa el formulario.");
        }
    }

    private static void RecortarCadenas(object modelo)
    {
        foreach (var propiedad in modelo.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (propiedad.PropertyType != typeof(string)
                || !propiedad.CanRead
                || !propiedad.CanWrite
                || PropiedadesSinRecorte.Contains(propiedad.Name))
            {
                continue;
            }

            if (propiedad.GetValue(modelo) is string valor)
            {
                propiedad.SetValue(modelo, valor.Trim());
            }
        }
    }
}
