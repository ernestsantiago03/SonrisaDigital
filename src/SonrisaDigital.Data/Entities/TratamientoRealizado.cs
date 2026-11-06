using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Data.Tiempo;
namespace SonrisaDigital.Data.Entities;

public class TratamientoRealizado : IPerteneceAClinica
{
    public int ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public int Id { get; set; }
    public int PacienteId { get; set; }
    public int TratamientoId { get; set; }
    public int OdontologoId { get; set; }
    public int? PiezaDentalId { get; set; }
    public int? ConsultaId { get; set; }
    public DateOnly Fecha { get; set; } = RelojNegocio.HoyDateOnly();
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