using SonrisaDigital.Data.Tenancy;

namespace SonrisaDigital.Data.Entities;

/// <summary>
/// Tratamiento propuesto dentro de una consulta clínica. Todavía no es un tratamiento
/// realizado: no genera cobros hasta que se asigna desde el módulo de Tratamientos.
/// </summary>
public class PlanTratamientoItem : IPerteneceAClinica
{
    public int ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public int Id { get; set; }
    public int ConsultaId { get; set; }
    public int TratamientoId { get; set; }
    public int? PiezaDentalId { get; set; }
    public decimal Precio { get; set; }
    public string? Observaciones { get; set; }
    public EstadoPlanTratamiento Estado { get; set; } = EstadoPlanTratamiento.Pendiente;
    public int? TratamientoRealizadoId { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public ConsultaClinica ConsultaClinica { get; set; } = null!;
    public Tratamiento Tratamiento { get; set; } = null!;
    public PiezaDental? PiezaDental { get; set; }
    public TratamientoRealizado? TratamientoRealizado { get; set; }
}

public enum EstadoPlanTratamiento
{
    Pendiente,
    Asignado,
    Cancelado
}
