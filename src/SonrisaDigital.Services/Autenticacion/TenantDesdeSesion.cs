using SonrisaDigital.Data.Tenancy;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Clínica de la sesión actual. Se lee del claim firmado que genera el servidor; el navegador
/// nunca envía la clínica. Sin sesión o sin claim no hay clínica (cierre por defecto).
/// </summary>
public sealed class TenantDesdeSesion(ICurrentUserService currentUserService) : ITenantProvider
{
    public int? ClinicaId
    {
        get
        {
            var valor = currentUserService.Principal?.FindFirst(ReclamacionesClinica.ClinicaId)?.Value;
            return int.TryParse(valor, out var id) && id > 0 ? id : null;
        }
    }
}
