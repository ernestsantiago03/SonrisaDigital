namespace SonrisaDigital.Data.Entities;

public class PiezaDental
{
    public int Id { get; set; }
    public string NumeroFDI { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    public ICollection<RegistroOdontograma> RegistrosOdontograma { get; set; } = [];
    public ICollection<TratamientoRealizado> TratamientosRealizados { get; set; } = [];
}