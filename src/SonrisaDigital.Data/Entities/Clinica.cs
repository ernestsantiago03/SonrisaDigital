namespace SonrisaDigital.Data.Entities;

/// <summary>
/// Clínica que usa el sistema. Cada registro de negocio pertenece a una clínica (ClinicaId).
/// Los campos de identidad alimentan la página "Acerca De" de esa clínica.
/// </summary>
public class Clinica
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Rnc { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Direccion { get; set; }
    public string? Horario { get; set; }
    public string? Eslogan { get; set; }
    public string? LogoUrl { get; set; }
    public EstadoClinica Estado { get; set; } = EstadoClinica.Activa;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
    public string? Plan { get; set; }

    /// <summary>Fecha en que la clínica empieza a operar. La prueba de 30 días corre desde aquí.</summary>
    public DateTime? FechaPrimerIngreso { get; set; }

    public DateTime? FechaVencimiento { get; set; }
    public bool EnPrueba { get; set; }
    public int LimiteUsuarios { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = [];
    public ICollection<PagoSuscripcion> PagosSuscripcion { get; set; } = [];
}

public enum EstadoClinica
{
    Activa,
    Suspendida,
    Vencida
}
