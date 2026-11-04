namespace SonrisaDigital.Services.Archivos;

public interface IArchivoPacienteService
{
    /// <summary>
    /// Archivos del paciente, del más reciente estudio al más antiguo. Requiere Odontograma.Ver.
    /// Los ocultos solo aparecen si la sesión tiene Archivos.Ocultar y se pide incluirOcultos.
    /// </summary>
    Task<ArchivosPacienteListaDto> ListarAsync(int pacienteId, bool incluirOcultos = false, CancellationToken cancellationToken = default);

    /// <summary>Sube una imagen nueva. Requiere Odontograma.Editar. Nunca reemplaza archivos anteriores.</summary>
    Task<ArchivoPacienteDto> SubirAsync(
        int pacienteId,
        string nombreOriginal,
        byte[] contenido,
        DateOnly fechaEstudio,
        string? descripcion,
        CancellationToken cancellationToken = default);

    /// <summary>Contenido del archivo o de su miniatura. Devuelve null si no existe o está oculto para la sesión.</summary>
    Task<ArchivoPacienteContenido?> ObtenerContenidoAsync(int archivoId, bool miniatura = false, CancellationToken cancellationToken = default);

    /// <summary>Oculta el archivo de la pantalla. Solo Archivos.Ocultar (Dueño). El archivo se conserva.</summary>
    Task OcultarAsync(int archivoId, CancellationToken cancellationToken = default);

    /// <summary>Vuelve a mostrar un archivo oculto. Solo Archivos.Ocultar (Dueño).</summary>
    Task RestaurarAsync(int archivoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra el archivo y su miniatura de forma definitiva, por ejemplo una imagen subida por error.
    /// Requiere Odontograma.Editar. No se puede deshacer.
    /// </summary>
    Task EliminarAsync(int archivoId, CancellationToken cancellationToken = default);
}
