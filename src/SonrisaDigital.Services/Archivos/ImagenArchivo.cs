using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Text;

namespace SonrisaDigital.Services.Archivos;

/// <summary>
/// Validación y preparación de las imágenes que se suben al odontograma: confirma que el
/// contenido sea realmente JPG o PNG, quita los metadatos (fecha, ubicación GPS, modelo del
/// dispositivo) sin recomprimir la imagen y genera una miniatura para la lista.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ImagenArchivo
{
    public const int LadoMiniatura = 400;

    // Una radiografía o fotografía clínica ronda los 10-20 megapíxeles. Por encima de estos límites
    // la decodificación ocupa cientos de MB por imagen, así que se rechaza antes de decodificar.
    private const long PixelesMaximos = 30_000_000;
    private const int LadoMaximo = 12_000;

    // Segmentos JPEG que llevan metadatos: EXIF/XMP (APP1), APP12, IPTC (APP13) y comentarios (COM).
    private static readonly HashSet<byte> MarcadoresJpegDescartados = [0xE1, 0xEC, 0xED, 0xFE];

    // Fragmentos PNG que llevan metadatos: EXIF, texto y fecha de modificación.
    private static readonly HashSet<string> FragmentosPngDescartados = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];

    /// <summary>Devuelve el tipo MIME si el contenido es JPG o PNG; null en cualquier otro caso.</summary>
    public static string? DetectarTipo(ReadOnlySpan<byte> datos)
    {
        if (datos.Length >= 3 && datos[0] == 0xFF && datos[1] == 0xD8 && datos[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (datos.Length >= 8
            && datos[0] == 0x89 && datos[1] == 0x50 && datos[2] == 0x4E && datos[3] == 0x47
            && datos[4] == 0x0D && datos[5] == 0x0A && datos[6] == 0x1A && datos[7] == 0x0A)
        {
            return "image/png";
        }

        return null;
    }

    /// <summary>
    /// Comprueba que la imagen se pueda abrir y no sea excesivamente grande en píxeles, y devuelve
    /// una miniatura JPG nueva (sin metadatos). Lanza InvalidOperationException si no es válida.
    /// </summary>
    public static byte[] GenerarMiniatura(byte[] datos)
    {
        using var flujo = new MemoryStream(datos, writable: false);
        using var original = ValidarYAbrir(flujo);

        var escala = Math.Min(1.0, (double)LadoMiniatura / Math.Max(original.Width, original.Height));
        var ancho = Math.Max(1, (int)(original.Width * escala));
        var alto = Math.Max(1, (int)(original.Height * escala));

        using var miniatura = new Bitmap(ancho, alto, PixelFormat.Format24bppRgb);
        using (var graficos = Graphics.FromImage(miniatura))
        {
            graficos.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graficos.DrawImage(original, 0, 0, ancho, alto);
        }

        using var salida = new MemoryStream();
        miniatura.Save(salida, ImageFormat.Jpeg);
        return salida.ToArray();
    }

    /// <summary>Quita los metadatos sin tocar los píxeles. Devuelve el archivo listo para guardar.</summary>
    public static byte[] QuitarMetadatos(byte[] datos, string tipo)
    {
        var limpio = tipo == "image/jpeg" ? QuitarMetadatosJpeg(datos) : QuitarMetadatosPng(datos);

        // Se vuelve a validar: si al quitar algo se dañó la imagen, no se guarda.
        using var flujo = new MemoryStream(limpio, writable: false);
        using var comprobacion = ValidarYAbrir(flujo);
        return limpio;
    }

    /// <summary>
    /// Lee el ancho y el alto desde la cabecera, sin decodificar los píxeles, y rechaza las
    /// dimensiones excesivas antes de que el procesamiento reserve memoria.
    /// </summary>
    public static void ValidarDimensiones(byte[] datos, string tipo)
    {
        var dimensiones = (tipo == "image/png" ? LeerDimensionesPng(datos) : LeerDimensionesJpeg(datos))
            ?? throw new InvalidOperationException("El archivo no es una imagen válida.");

        if (dimensiones.Ancho > LadoMaximo || dimensiones.Alto > LadoMaximo
            || (long)dimensiones.Ancho * dimensiones.Alto > PixelesMaximos)
        {
            throw new InvalidOperationException("La imagen tiene demasiados píxeles para subirla.");
        }
    }

    private static (int Ancho, int Alto)? LeerDimensionesPng(byte[] datos)
    {
        // Firma de 8 bytes, longitud de 4 y el tipo IHDR; después vienen el ancho y el alto.
        if (datos.Length < 24 || Encoding.ASCII.GetString(datos, 12, 4) != "IHDR")
        {
            return null;
        }

        var ancho = (datos[16] << 24) | (datos[17] << 16) | (datos[18] << 8) | datos[19];
        var alto = (datos[20] << 24) | (datos[21] << 16) | (datos[22] << 8) | datos[23];
        return ancho > 0 && alto > 0 ? (ancho, alto) : null;
    }

    private static (int Ancho, int Alto)? LeerDimensionesJpeg(byte[] datos)
    {
        var pos = 2;
        while (pos + 4 <= datos.Length)
        {
            if (datos[pos] != 0xFF)
            {
                return null;
            }

            var marcador = datos[pos + 1];
            if (marcador == 0xFF)
            {
                pos++;
                continue;
            }

            // Marcadores de inicio de frame (SOF), salvo DHT, JPG y DAC que no son frames.
            var esSof = marcador >= 0xC0 && marcador <= 0xCF && marcador is not (0xC4 or 0xC8 or 0xCC);
            if (esSof)
            {
                if (pos + 9 > datos.Length)
                {
                    return null;
                }

                var alto = (datos[pos + 5] << 8) | datos[pos + 6];
                var ancho = (datos[pos + 7] << 8) | datos[pos + 8];
                return ancho > 0 && alto > 0 ? (ancho, alto) : null;
            }

            if (marcador == 0xD9 || marcador == 0xDA)
            {
                return null;
            }

            var longitud = (datos[pos + 2] << 8) | datos[pos + 3];
            pos += 2 + longitud;
        }

        return null;
    }

    private static Bitmap ValidarYAbrir(Stream flujo)
    {
        Bitmap imagen;
        try
        {
            imagen = new Bitmap(flujo);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("El archivo no es una imagen válida.");
        }

        if ((long)imagen.Width * imagen.Height > PixelesMaximos)
        {
            imagen.Dispose();
            throw new InvalidOperationException("La imagen tiene demasiados píxeles para subirla.");
        }

        return imagen;
    }

    private static byte[] QuitarMetadatosJpeg(byte[] datos)
    {
        using var salida = new MemoryStream(datos.Length);
        salida.Write(datos, 0, 2);
        var pos = 2;

        while (pos < datos.Length)
        {
            if (pos + 1 >= datos.Length || datos[pos] != 0xFF)
            {
                throw new InvalidOperationException("La imagen JPG está dañada.");
            }

            var marcador = datos[pos + 1];

            // Relleno entre marcadores.
            if (marcador == 0xFF)
            {
                pos++;
                continue;
            }

            // Fin de imagen.
            if (marcador == 0xD9)
            {
                salida.Write(datos, pos, 2);
                break;
            }

            // Marcadores sin longitud.
            if (marcador == 0x01 || (marcador >= 0xD0 && marcador <= 0xD7))
            {
                salida.Write(datos, pos, 2);
                pos += 2;
                continue;
            }

            if (pos + 4 > datos.Length)
            {
                throw new InvalidOperationException("La imagen JPG está dañada.");
            }

            var longitud = (datos[pos + 2] << 8) | datos[pos + 3];

            // Inicio de los datos comprimidos: el resto del archivo se copia tal cual.
            if (marcador == 0xDA)
            {
                var fin = pos + 2 + longitud;
                if (fin > datos.Length)
                {
                    throw new InvalidOperationException("La imagen JPG está dañada.");
                }

                salida.Write(datos, pos, fin - pos);
                salida.Write(datos, fin, datos.Length - fin);
                break;
            }

            var total = 2 + longitud;
            if (pos + total > datos.Length)
            {
                throw new InvalidOperationException("La imagen JPG está dañada.");
            }

            if (!MarcadoresJpegDescartados.Contains(marcador))
            {
                salida.Write(datos, pos, total);
            }

            pos += total;
        }

        return salida.ToArray();
    }

    private static byte[] QuitarMetadatosPng(byte[] datos)
    {
        using var salida = new MemoryStream(datos.Length);
        salida.Write(datos, 0, 8);
        var pos = 8;

        while (pos + 12 <= datos.Length)
        {
            var longitud = (datos[pos] << 24) | (datos[pos + 1] << 16) | (datos[pos + 2] << 8) | datos[pos + 3];
            var total = 12L + (uint)longitud;
            if (pos + total > datos.Length)
            {
                throw new InvalidOperationException("La imagen PNG está dañada.");
            }

            var tipo = Encoding.ASCII.GetString(datos, pos + 4, 4);
            if (!FragmentosPngDescartados.Contains(tipo))
            {
                salida.Write(datos, pos, (int)total);
            }

            pos += (int)total;
            if (tipo == "IEND")
            {
                break;
            }
        }

        return salida.ToArray();
    }
}
