namespace SonrisaDigital.Data.Entities;

public class Paciente
{
    public int Id { get; set; }
    public string? Identificacion { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public DateOnly? FechaNacimiento { get; set; }
    public string? Sexo { get; set; }
    public string? TipoSangre { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Direccion { get; set; }
    public string? ContactoEmergenciaNombre { get; set; }
    public string? ContactoEmergenciaTelefono { get; set; }
    public string? ContactoEmergenciaRelacion { get; set; }
    public EstadoRegistro Estado { get; set; } = EstadoRegistro.Activo;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public HistoriaClinica? HistoriaClinica { get; set; }
    public ICollection<Cita> Citas { get; set; } = [];
    public ICollection<ConsultaClinica> ConsultasClinicas { get; set; } = [];
    public ICollection<RegistroOdontograma> RegistrosOdontograma { get; set; } = [];
    public ICollection<TratamientoRealizado> TratamientosRealizados { get; set; } = [];
}