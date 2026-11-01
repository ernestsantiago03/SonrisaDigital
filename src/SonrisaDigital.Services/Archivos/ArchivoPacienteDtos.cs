namespace SonrisaDigital.Services.Archivos;

/// <summary>Límites que comparten el servicio y la pantalla.</summary>
public static class LimitesArchivo
{
    public const int TamanoMaximo = 20 * 1024 * 1024;
}

public sealed record ArchivoPacienteDto(
    int Id,
    string NombreOriginal,
    string? Descripcion,
    DateOnly FechaEstudio,
    DateTime FechaSubida,
    string SubidoPor,
    string TipoContenido,
    bool Oculto);

/// <summary>Lista de archivos de un paciente. PuedeOcultar indica si la sesión puede ocultar y restaurar.</summary>
public sealed record ArchivosPacienteListaDto(IReadOnlyList<ArchivoPacienteDto> Archivos, bool PuedeOcultar);

/// <summary>Contenido de un archivo listo para enviarse al navegador.</summary>
public sealed record ArchivoPacienteContenido(byte[] Datos, string TipoContenido, string NombreOriginal);
