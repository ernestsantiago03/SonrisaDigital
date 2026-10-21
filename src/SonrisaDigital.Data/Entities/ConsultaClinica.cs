namespace SonrisaDigital.Data.Entities;

public class ConsultaClinica
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public int OdontologoId { get; set; }
    public int? CitaId { get; set; }
    public int HistoriaClinicaId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
    public string? Diagnostico { get; set; }
    public string? PlanTratamiento { get; set; }
    public EstadoConsulta Estado { get; set; } = EstadoConsulta.Abierta;

    public Paciente Paciente { get; set; } = null!;
    public Odontologo Odontologo { get; set; } = null!;
    public Cita? Cita { get; set; }
    public HistoriaClinica HistoriaClinica { get; set; } = null!;
    public ICollection<TratamientoRealizado> TratamientosRealizados { get; set; } = [];
}