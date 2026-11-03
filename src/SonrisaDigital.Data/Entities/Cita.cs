using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Data.Tiempo;
namespace SonrisaDigital.Data.Entities;

public class Cita : IPerteneceAClinica
{
    public int ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public int Id { get; set; }
    public int PacienteId { get; set; }
    public int OdontologoId { get; set; }
    public DateOnly Fecha { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
    public EstadoCita Estado { get; set; } = EstadoCita.Programada;

    // Se inicializa automáticamente con la hora exacta de República Dominicana
    public DateTime FechaCreacion { get; set; } = RelojNegocio.Ahora();
    public DateTime? FechaModificacion { get; set; }

    public Paciente Paciente { get; set; } = null!;
    public Odontologo Odontologo { get; set; } = null!;
    public ConsultaClinica? ConsultaClinica { get; set; }
}