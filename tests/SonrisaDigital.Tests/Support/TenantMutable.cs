using SonrisaDigital.Data.Tenancy;

namespace SonrisaDigital.Tests.Support;

/// <summary>Clínica que la prueba puede cambiar entre operaciones, para simular dos sesiones.</summary>
internal sealed class TenantMutable : ITenantProvider
{
    public int? ClinicaId { get; set; }
}
