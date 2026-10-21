namespace SonrisaDigital.Data.Entities;

public class Tratamiento
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal PrecioBase { get; set; }
    public EstadoRegistro Estado { get; set; } = EstadoRegistro.Activo;

    public ICollection<TratamientoRealizado> TratamientosRealizados { get; set; } = [];
}