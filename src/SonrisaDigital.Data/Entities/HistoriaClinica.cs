namespace SonrisaDigital.Data.Entities;

public class HistoriaClinica
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public string? Alergias { get; set; }
    public string? Enfermedades { get; set; }
    public string? Medicamentos { get; set; }
    public string? AntecedentesMedicos { get; set; }
    public string? AntecedentesOdontologicos { get; set; }
    public string? Habitos { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaModificacion { get; set; }

    public Paciente Paciente { get; set; } = null!;
    public ICollection<ConsultaClinica> ConsultasClinicas { get; set; } = [];
}