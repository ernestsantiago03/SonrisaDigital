namespace SonrisaDigital.Data.Entities;

/// <summary>
/// Intento de acceso al sistema. Es una bitácora de seguridad independiente de la
/// auditoría clínica: puede existir sin usuario (correo inexistente) y sin clínica
/// (el Dueño es global), por eso no implementa <c>IPerteneceAClinica</c>.
/// </summary>
public class EventoAcceso
{
    public long Id { get; set; }

    /// <summary>Usuario encontrado, o <c>null</c> cuando el correo no existe o no se pudo identificar.</summary>
    public string? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Correo tal como se intentó usar. Se guarda aunque la cuenta no exista.</summary>
    public string? Correo { get; set; }

    public ResultadoAcceso Resultado { get; set; }

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    /// <summary>IP del cliente (ya resuelta por ForwardedHeaders cuando hay proxy).</summary>
    public string? DireccionIp { get; set; }

    /// <summary>Navegador y sistema operativo declarados por el cliente.</summary>
    public string? Agente { get; set; }

    /// <summary>Clínica del usuario al momento del intento. Nula para el Dueño o correos inexistentes.</summary>
    public int? ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    /// <summary>Contexto adicional del intento (motivo de la denegación, paso del acceso, etc.).</summary>
    public string? Detalle { get; set; }
}

/// <summary>Resultado de un intento de acceso. Se guarda como texto para que la lectura sea legible.</summary>
public enum ResultadoAcceso
{
    Exitoso,
    CredencialesInvalidas,
    ClaveIncorrecta,
    CuentaInactiva,
    CuentaBloqueada,
    LimiteExcedido,
    SegundoFactorPendiente,
    SegundoFactorFallido,
    AccesoClinicaDenegado,
    CierreSesion
}
