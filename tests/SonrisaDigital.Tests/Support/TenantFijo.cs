using SonrisaDigital.Data.Tenancy;

namespace SonrisaDigital.Tests.Support;

/// <summary>Clínica fija para las pruebas de integración.</summary>
internal sealed class TenantFijo(int? clinicaId) : ITenantProvider
{
    public int? ClinicaId => clinicaId;

    public bool EsAdministradorGlobal => false;
}
