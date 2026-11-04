using System.Net;
using Microsoft.Extensions.Configuration;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.Services.Configuracion;

/// <summary>
/// Revisa que la configuración mínima de un despliegue real esté presente antes
/// de que la aplicación empiece a atender usuarios.
///
/// La validación se ejecuta solo fuera de Development. En Development se usan
/// valores locales y el runner E2E; en producción se exige dominio, cadena de
/// conexión y una ruta persistente para las claves de Data Protection.
/// </summary>
public static class ValidadorConfiguracionProduccion
{
    /// <summary>
    /// Devuelve los errores que impiden arrancar y las advertencias que deben
    /// revisarse, pero que no bloquean el inicio.
    /// </summary>
    /// <param name="configuration">Configuración combinada de appsettings, variables de entorno y secretos.</param>
    /// <param name="esDesarrollo">Indica si el entorno actual es Development.</param>
    public static ResultadoValidacionConfiguracion Validar(
        IConfiguration configuration,
        bool esDesarrollo)
    {
        // En desarrollo no se exige la configuración de producción: el runner E2E
        // crea su propia base y usa una contraseña temporal en memoria.
        if (esDesarrollo)
        {
            return new ResultadoValidacionConfiguracion(
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var errores = new List<string>();
        var advertencias = new List<string>();

        ValidarAllowedHosts(configuration, errores, advertencias);
        ValidarCadenaConexion(configuration, errores, advertencias);
        ValidarProxy(configuration, errores, advertencias);
        ValidarDataProtection(configuration, errores, advertencias);
        ValidarArchivos(configuration, errores, advertencias);
        ValidarSeguridad(configuration, errores, advertencias);
        ValidarSeeding(configuration, errores, advertencias);

        return new ResultadoValidacionConfiguracion(errores, advertencias);
    }

    /// <summary>
    /// AllowedHosts evita que la aplicación responda a nombres de host que no le
    /// pertenecen. Un comodín o el texto de ejemplo indican que la configuración
    /// de producción todavía no fue completada.
    /// </summary>
    private static void ValidarAllowedHosts(
        IConfiguration configuration,
        List<string> errores,
        List<string> advertencias)
    {
        var allowedHosts = configuration["AllowedHosts"]?.Trim();

        if (string.IsNullOrWhiteSpace(allowedHosts))
        {
            errores.Add("AllowedHosts no está configurado.");
            return;
        }

        if (allowedHosts == "*"
            || allowedHosts.Contains("REEMPLAZAR", StringComparison.OrdinalIgnoreCase))
        {
            errores.Add("AllowedHosts contiene un comodín o un valor de ejemplo. Configura el dominio real.");
            return;
        }

        var hosts = allowedHosts.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (hosts.Length == 0)
        {
            errores.Add("AllowedHosts no contiene ningún nombre de host válido.");
            return;
        }

        // localhost puede ser útil en una prueba local, pero no es el dominio que
        // debe atender a la clínica. Se avisa sin bloquear para no impedir un
        // smoke test controlado en el servidor.
        if (hosts.Any(host => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            advertencias.Add("AllowedHosts incluye localhost. Confirma que también esté el dominio real.");
        }
    }

    /// <summary>
    /// La cadena de conexión no debe quedar vacía ni con parámetros que
    /// desactiven la validación del certificado en producción.
    /// </summary>
    private static void ValidarCadenaConexion(
        IConfiguration configuration,
        List<string> errores,
        List<string> advertencias)
    {
        var cadenaConexion = configuration.GetConnectionString("DefaultConnection")?.Trim();

        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            errores.Add("ConnectionStrings:DefaultConnection no está configurada.");
            return;
        }

        // En producción se espera cifrado y un certificado válido. Si la cadena
        // los desactiva, se permite arrancar pero queda una advertencia visible
        // para que el responsable de infraestructura la corrija.
        if (cadenaConexion.Contains("TrustServerCertificate=True", StringComparison.OrdinalIgnoreCase)
            || cadenaConexion.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
        {
            advertencias.Add(
                "La cadena de conexión permite certificados no confiables o desactiva el cifrado. " +
                "En producción usa Encrypt=True y TrustServerCertificate=False.");
        }
    }

    /// <summary>
    /// Los proxies de confianza determinan qué IP puede enviar X-Forwarded-For y
    /// X-Forwarded-Proto. Una IP mal escrita se ignora silenciosamente en el
    /// pipeline, por eso aquí se convierte en un error de configuración.
    /// </summary>
    private static void ValidarProxy(
        IConfiguration configuration,
        List<string> errores,
        List<string> advertencias)
    {
        var forwardLimit = configuration.GetValue<int?>("ReverseProxy:ForwardLimit") ?? 1;
        if (forwardLimit < 1)
        {
            errores.Add("ReverseProxy:ForwardLimit debe ser mayor o igual a 1.");
        }

        var knownProxies = configuration
            .GetSection("ReverseProxy:KnownProxies")
            .Get<string[]>() ?? [];

        if (knownProxies.Length == 0)
        {
            // La lista vacía conserva el comportamiento de loopback, válido para
            // IIS en la misma máquina. Si hay un proxy externo, debe declararse.
            advertencias.Add(
                "ReverseProxy:KnownProxies está vacío. Solo se confiará en loopback; " +
                "si hay nginx o un balanceador, agrega su IP.");
            return;
        }

        foreach (var proxy in knownProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                errores.Add($"ReverseProxy:KnownProxies contiene una IP inválida: '{proxy}'.");
            }
        }
    }

    /// <summary>
    /// Data Protection firma las cookies y protege el estado de autenticación.
    /// En producción las claves deben vivir en una ruta persistente y respaldada,
    /// fuera de la carpeta que se reemplaza en cada publicación.
    /// </summary>
    private static void ValidarDataProtection(
        IConfiguration configuration,
        List<string> errores,
        List<string> advertencias)
    {
        var rutaClaves = configuration["DataProtection:KeysPath"]?.Trim();

        if (string.IsNullOrWhiteSpace(rutaClaves))
        {
            errores.Add(
                "DataProtection:KeysPath no está configurado. " +
                "Usa una carpeta persistente y respaldada para las claves.");
            return;
        }

        if (!Path.IsPathRooted(rutaClaves))
        {
            advertencias.Add(
                "DataProtection:KeysPath es una ruta relativa. " +
                "En producción se recomienda una ruta absoluta fuera de la publicación.");
        }
    }

    /// <summary>El segundo factor del Dueño no puede apagarse en un despliegue real.</summary>
    private static void ValidarSeguridad(
        IConfiguration configuration,
        List<string> errores,
        List<string> advertencias)
    {
        if (configuration.GetValue<bool?>("Seguridad:MfaDuenoObligatorio") == false)
        {
            errores.Add(
                "Seguridad:MfaDuenoObligatorio no puede ser false en producción. " +
                "El Dueño debe usar segundo factor.");
        }
    }

    /// <summary>
    /// Las radiografías y fotos del odontograma viven en una carpeta del servidor. Sin esa carpeta
    /// no se pueden subir archivos, así que debe estar configurada y ser una ruta absoluta.
    /// </summary>
    private static void ValidarArchivos(
        IConfiguration configuration,
        List<string> errores,
        List<string> advertencias)
    {
        var directorio = configuration["Archivos:Directorio"]?.Trim();

        if (string.IsNullOrWhiteSpace(directorio))
        {
            errores.Add(
                "Archivos:Directorio no está configurado. " +
                "Indica una carpeta absoluta, fuera de wwwroot, para las imágenes del odontograma.");
            return;
        }

        if (!Path.IsPathRooted(directorio))
        {
            advertencias.Add(
                "Archivos:Directorio es una ruta relativa. " +
                "En producción se recomienda una ruta absoluta.");
        }
    }

    /// <summary>
    /// En producción la aplicación arranca solo con la cuenta del Dueño. Esa cuenta
    /// crea la primera clínica y su administrador desde la interfaz. El
    /// administrador automático y la clínica demo quedan reservados a Development.
    /// </summary>
    private static void ValidarSeeding(
        IConfiguration configuration,
        List<string> errores,
        List<string> advertencias)
    {
        var duenoEmail = configuration["Dueno:Email"]?.Trim();
        var duenoPassword = configuration["Dueno:Password"];

        if (string.IsNullOrWhiteSpace(duenoEmail))
        {
            errores.Add(
                "Dueno:Email no está configurado. Es la cuenta que crea la primera " +
                "clínica y su administrador desde la interfaz.");
        }

        if (string.IsNullOrWhiteSpace(duenoPassword))
        {
            errores.Add(
                "Dueno:Password no está configurado. Es la cuenta que crea la primera " +
                "clínica y su administrador desde la interfaz.");
        }
        else if (SeguridadDueno.ValidarClave(duenoPassword) is { } errorClave)
        {
            errores.Add($"Dueno:Password no es válida. {errorClave}");
        }

        if (!string.IsNullOrWhiteSpace(configuration["Authentication:AdminEmail"])
            || !string.IsNullOrWhiteSpace(configuration["Authentication:AdminPassword"]))
        {
            advertencias.Add(
                "Authentication:AdminEmail/AdminPassword se ignoran en producción: " +
                "el administrador inicial se crea desde la interfaz del Dueño.");
        }
    }
}

/// <summary>
/// Resultado de la revisión de configuración. Los errores impiden el arranque;
/// las advertencias se registran para revisión del operador.
/// </summary>
public sealed record ResultadoValidacionConfiguracion(
    IReadOnlyList<string> Errores,
    IReadOnlyList<string> Advertencias);
