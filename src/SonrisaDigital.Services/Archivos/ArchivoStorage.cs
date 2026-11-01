using Microsoft.Extensions.Configuration;

namespace SonrisaDigital.Services.Archivos;

/// <summary>
/// Guarda los archivos físicos en la carpeta configurada en "Archivos:Directorio". La base solo
/// guarda claves relativas (por ejemplo "1/25/abc.jpg"), así que cambiar la carpeta de lugar
/// solo requiere cambiar la configuración.
/// </summary>
public interface IArchivoStorage
{
    Task GuardarAsync(string clave, byte[] contenido, CancellationToken cancellationToken = default);
    Task<byte[]> LeerAsync(string clave, CancellationToken cancellationToken = default);

    /// <summary>Solo para revertir una subida que no llegó a guardarse en la base. Nunca borra archivos ya registrados.</summary>
    void EliminarSubidaFallida(string clave);
}

public sealed class ArchivoStorage(IConfiguration configuration) : IArchivoStorage
{
    private string Raiz
    {
        get
        {
            var directorio = configuration["Archivos:Directorio"]?.Trim();
            if (string.IsNullOrWhiteSpace(directorio))
            {
                throw new InvalidOperationException("Falta configurar Archivos:Directorio para guardar archivos.");
            }

            return Path.GetFullPath(directorio).TrimEnd(Path.DirectorySeparatorChar);
        }
    }

    public async Task GuardarAsync(string clave, byte[] contenido, CancellationToken cancellationToken = default)
    {
        var ruta = RutaSegura(clave);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        await File.WriteAllBytesAsync(ruta, contenido, cancellationToken);
    }

    public Task<byte[]> LeerAsync(string clave, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(RutaSegura(clave), cancellationToken);

    public void EliminarSubidaFallida(string clave)
    {
        var ruta = RutaSegura(clave);
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
    }

    private string RutaSegura(string clave)
    {
        var raiz = Raiz;
        var ruta = Path.GetFullPath(Path.Combine(raiz, clave));

        if (!ruta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La ruta del archivo no es válida.");
        }

        return ruta;
    }
}
