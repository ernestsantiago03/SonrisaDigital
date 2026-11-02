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

    /// <summary>
    /// Motivo de la suspensión manual. Es <c>null</c> mientras la clínica está activa.
    /// Se conserva para distinguir una suspensión por impago, que un pago puede
    /// resolver, de una decisión administrativa, que exige reactivación explícita.
    /// </summary>
    public MotivoSuspension? MotivoSuspension { get; set; }

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

/// <summary>
/// Razón por la que el Dueño suspendió una clínica. Un pago reactiva solo
/// <see cref="FaltaDePago"/>; una suspensión administrativa requiere que el
/// Dueño la levante de forma explícita.
/// </summary>
public enum MotivoSuspension
{
    FaltaDePago,
    DecisionAdministrativa
}
