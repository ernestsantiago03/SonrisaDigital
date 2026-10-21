namespace SonrisaDigital.Data.Entities;

public class Pago
{
    public int Id { get; set; }
    public int TratamientoRealizadoId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public decimal Monto { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public string? Observaciones { get; set; }
    public string UsuarioId { get; set; } = string.Empty;

    public TratamientoRealizado TratamientoRealizado { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}