using SonrisaDigital.Data.Tenancy;
namespace SonrisaDigital.Data.Entities;

/// <summary>
/// Un guardado de una pieza dental: el estado antes y después, las observaciones de ese
/// momento y quién firmó. Solo se agregan filas; nunca se editan ni se borran.
/// </summary>
public class HistorialOdontograma : IPerteneceAClinica
{
    public int ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public int Id { get; set; }
    public int PacienteId { get; set; }
    public int PiezaDentalId { get; set; }

    /// <summary>Nulo cuando es el primer registro de la pieza.</summary>
    public EstadoOdontograma? EstadoAnterior { get; set; }
    public EstadoOdontograma EstadoNuevo { get; set; }
    public string? Observaciones { get; set; }
    public int OdontologoId { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    public Paciente Paciente { get; set; } = null!;
    public PiezaDental PiezaDental { get; set; } = null!;
    public Odontologo Odontologo { get; set; } = null!;
}
