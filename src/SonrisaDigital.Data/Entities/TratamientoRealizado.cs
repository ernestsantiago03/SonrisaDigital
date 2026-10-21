namespace SonrisaDigital.Data.Entities;

public class TratamientoRealizado
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public int TratamientoId { get; set; }
    public int OdontologoId { get; set; }
    public int? PiezaDentalId { get; set; }
    public int? ConsultaId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public decimal Precio { get; set; }
    public EstadoTratamiento Estado { get; set; } = EstadoTratamiento.EnProceso;
    public string? Observaciones { get; set; }

    public Paciente Paciente { get; set; } = null!;
    public Tratamiento Tratamiento { get; set; } = null!;
    public Odontologo Odontologo { get; set; } = null!;
    public PiezaDental? PiezaDental { get; set; }
    public ConsultaClinica? ConsultaClinica { get; set; }
    public ICollection<Pago> Pagos { get; set; } = [];
}