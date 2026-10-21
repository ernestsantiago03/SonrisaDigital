namespace SonrisaDigital.Data.Entities;

public class RegistroOdontograma
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public int PiezaDentalId { get; set; }
    public EstadoOdontograma Estado { get; set; } = EstadoOdontograma.Sano;
    public string? Observaciones { get; set; }
    public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;
    public int OdontologoId { get; set; }

    public Paciente Paciente { get; set; } = null!;
    public PiezaDental PiezaDental { get; set; } = null!;
    public Odontologo Odontologo { get; set; } = null!;
}