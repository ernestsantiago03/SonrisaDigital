using Microsoft.AspNetCore.Identity;

namespace SonrisaDigital.Data.Entities;

public class Usuario : IdentityUser
{
    public string Nombre { get; set; } = string.Empty;
    public EstadoRegistro Estado { get; set; } = EstadoRegistro.Activo;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>Clínica a la que pertenece el usuario. Nulo solo para el Dueño del sistema.</summary>
    public int? ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public Odontologo? Odontologo { get; set; }
    public ICollection<RegistroAuditoria> RegistrosAuditoria { get; set; } = [];
    public ICollection<Pago> Pagos { get; set; } = [];
}