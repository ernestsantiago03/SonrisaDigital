namespace SonrisaDigital.Data.Entities;

public class Odontologo
{
    public int Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string? NumeroLicencia { get; set; }
    public string? Especialidad { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public EstadoRegistro Estado { get; set; } = EstadoRegistro.Activo;
    public DateTime FechaRegistro { get; set; }
    public string? UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }
    public ICollection<Cita> Citas { get; set; } = [];
    public ICollection<ConsultaClinica> ConsultasClinicas { get; set; } = [];
    public ICollection<RegistroOdontograma> RegistrosOdontograma { get; set; } = [];
    public ICollection<TratamientoRealizado> TratamientosRealizados { get; set; } = [];
}