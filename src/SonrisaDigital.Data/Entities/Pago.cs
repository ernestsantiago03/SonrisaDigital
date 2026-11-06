using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Data.Tiempo;
namespace SonrisaDigital.Data.Entities;

public class Pago : IPerteneceAClinica
{
    public int ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public int Id { get; set; }
    public int TratamientoRealizadoId { get; set; }
    public DateOnly Fecha { get; set; } = RelojNegocio.HoyDateOnly();
    public decimal Monto { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public string? Observaciones { get; set; }
    public string UsuarioId { get; set; } = string.Empty;

    public TratamientoRealizado TratamientoRealizado { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}