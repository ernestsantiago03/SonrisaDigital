using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Text;
using SonrisaDigital.Services.Archivos;

namespace SonrisaDigital.Tests.Unit;

/// <summary>Validación, limpieza de metadatos y miniaturas de las imágenes del odontograma.</summary>
[SupportedOSPlatform("windows")]
public class ImagenArchivoTests
{
    [Fact]
    public void DetectaJpgYPngPorSuContenido()
    {
        if (!RequerirWindows()) return;
        var jpg = CrearJpg();
        var png = CrearPng();

        Assert.Equal("image/jpeg", ImagenArchivo.DetectarTipo(jpg));
        Assert.Equal("image/png", ImagenArchivo.DetectarTipo(png));
        Assert.Null(ImagenArchivo.DetectarTipo(Encoding.ASCII.GetBytes("esto es texto, no una imagen")));
    }

    [Fact]
    public void QuitaLosMetadatosExifDelJpgSinAlterarLaImagen()
    {
        if (!RequerirWindows()) return;
        var original = CrearJpg();
        var conExif = InsertarSegmentoJpeg(original, marcador: 0xE1, contenido: Encoding.ASCII.GetBytes("Exif\0\0UbicacionSecreta")).ToArray();
        Assert.True(Contiene(conExif, "UbicacionSecreta"));

        var limpio = ImagenArchivo.QuitarMetadatos(conExif, "image/jpeg");

        Assert.False(Contiene(limpio, "UbicacionSecreta"));
        Assert.Equal(original, limpio);
    }

    [Fact]
    public void QuitaLosTextosDelPng()
    {
        if (!RequerirWindows()) return;
        var conTexto = InsertarFragmentoPng(CrearPng(), "tEXt", Encoding.ASCII.GetBytes("Autor\0AnaSecreta"));
        Assert.True(Contiene(conTexto, "AnaSecreta"));

        var limpio = ImagenArchivo.QuitarMetadatos(conTexto, "image/png");

        Assert.False(Contiene(limpio, "AnaSecreta"));
        Assert.Equal("image/png", ImagenArchivo.DetectarTipo(limpio));
    }

    [Fact]
    public void LaMiniaturaEsUnJpgNoMayorQueElLimite()
    {
        if (!RequerirWindows()) return;
        var grande = CrearImagen(1200, 800, ImageFormat.Png);

        var miniatura = ImagenArchivo.GenerarMiniatura(grande);

        Assert.Equal("image/jpeg", ImagenArchivo.DetectarTipo(miniatura));
        using var flujo = new MemoryStream(miniatura);
        using var imagen = new Bitmap(flujo);
        Assert.True(Math.Max(imagen.Width, imagen.Height) <= ImagenArchivo.LadoMiniatura);
    }

    [Fact]
    public void RechazaUnContenidoQueNoEsUnaImagenValida()
    {
        if (!RequerirWindows()) return;
        var falso = new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x01, 0x02, 0x03 };

        Assert.Throws<InvalidOperationException>(() => ImagenArchivo.GenerarMiniatura(falso));
    }

    /// <summary>System.Drawing solo funciona en Windows: fuera de Windows la prueba no se ejecuta.</summary>
    internal static bool RequerirWindows() => OperatingSystem.IsWindows();

    internal static bool Contiene(byte[] datos, string texto) =>
        datos.AsSpan().IndexOf(Encoding.ASCII.GetBytes(texto)) >= 0;

    internal static byte[] CrearJpg() => CrearImagen(64, 48, ImageFormat.Jpeg);

    internal static byte[] CrearPng() => CrearImagen(64, 48, ImageFormat.Png);

    private static byte[] CrearImagen(int ancho, int alto, ImageFormat formato)
    {
        using var bitmap = new Bitmap(ancho, alto, PixelFormat.Format24bppRgb);
        using (var graficos = Graphics.FromImage(bitmap))
        {
            graficos.Clear(Color.SteelBlue);
        }

        using var salida = new MemoryStream();
        bitmap.Save(salida, formato);
        return salida.ToArray();
    }

    private static List<byte> InsertarSegmentoJpeg(byte[] jpg, byte marcador, byte[] contenido)
    {
        var longitud = contenido.Length + 2;
        var resultado = new List<byte>(jpg.Length + contenido.Length + 4);
        resultado.AddRange(jpg.Take(2));
        resultado.Add(0xFF);
        resultado.Add(marcador);
        resultado.Add((byte)(longitud >> 8));
        resultado.Add((byte)(longitud & 0xFF));
        resultado.AddRange(contenido);
        resultado.AddRange(jpg.Skip(2));
        return resultado;
    }

    private static byte[] InsertarFragmentoPng(byte[] png, string tipo, byte[] datos)
    {
        var posicionIend = png.Length - 12;
        var longitud = datos.Length;
        var fragmento = new List<byte>
        {
            (byte)(longitud >> 24), (byte)(longitud >> 16), (byte)(longitud >> 8), (byte)longitud
        };
        fragmento.AddRange(Encoding.ASCII.GetBytes(tipo));
        fragmento.AddRange(datos);
        fragmento.AddRange(new byte[4]);

        var resultado = new List<byte>(png.Take(posicionIend));
        resultado.AddRange(fragmento);
        resultado.AddRange(png.Skip(posicionIend));
        return resultado.ToArray();
    }
}
