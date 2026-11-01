using SonrisaDigital.Data.Tenancy;
namespace SonrisaDigital.Data.Entities;

/// <summary>
/// Imagen (JPG o PNG) asociada a un paciente, como una radiografía. Los archivos físicos viven en
/// la carpeta configurada; aquí solo se guardan sus datos y las claves relativas dentro de esa
/// carpeta. Nunca se borra el archivo: "ocultar" solo lo quita de la pantalla.
/// </summary>
public class ArchivoPaciente : IPerteneceAClinica
{
    public int ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public int Id { get; set; }
    public int PacienteId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string ClaveArchivo { get; set; } = string.Empty;
    public string ClaveMiniatura { get; set; } = string.Empty;
    public string TipoContenido { get; set; } = string.Empty;
    public long Tamano { get; set; }
    public DateOnly FechaEstudio { get; set; }
    public string? Descripcion { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
    public bool Oculto { get; set; }
    public DateTime? FechaOcultado { get; set; }
    public string? OcultadoPorUsuarioId { get; set; }

    public Paciente Paciente { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public Usuario? OcultadoPor { get; set; }
}
