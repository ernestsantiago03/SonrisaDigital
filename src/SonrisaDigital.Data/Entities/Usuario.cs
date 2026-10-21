using Microsoft.AspNetCore.Identity;

namespace SonrisaDigital.Data.Entities;

public class Usuario : IdentityUser
{
    public string Nombre { get; set; } = string.Empty;
    public EstadoRegistro Estado { get; set; } = EstadoRegistro.Activo;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Odontologo? Odontologo { get; set; }
    public ICollection<RegistroAuditoria> RegistrosAuditoria { get; set; } = [];
    public ICollection<Pago> Pagos { get; set; } = [];
}