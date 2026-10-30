namespace SonrisaDigital.Data.Tenancy;

/// <summary>Clínica activa de la petición o del circuito. Nulo cuando no hay clínica (cierre por defecto).</summary>
public interface ITenantProvider
{
    int? ClinicaId { get; }

    /// <summary>Dueño del sistema: puede registrar datos con una clínica explícita (p. ej. la auditoría de una clínica).</summary>
    bool EsAdministradorGlobal { get; }
}

/// <summary>Proveedor sin clínica: ninguna consulta de negocio devuelve datos.</summary>
public sealed class SinClinica : ITenantProvider
{
    public static readonly SinClinica Instancia = new();

    public int? ClinicaId => null;

    public bool EsAdministradorGlobal => false;
}

/// <summary>Marca las entidades de negocio que pertenecen a una clínica.</summary>
public interface IPerteneceAClinica
{
    int ClinicaId { get; set; }
}
