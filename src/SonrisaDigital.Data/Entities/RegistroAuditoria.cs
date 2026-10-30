using SonrisaDigital.Data.Tenancy;
namespace SonrisaDigital.Data.Entities;

public class RegistroAuditoria : IPerteneceAClinica
{
    public int ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public long Id { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
    public string Accion { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public int? EntidadId { get; set; }
    public string? Descripcion { get; set; }

    public Usuario Usuario { get; set; } = null!;
}