using System.Security.Claims;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Autorizacion;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Clínica de la sesión actual. Un usuario de clínica la recibe del claim firmado por el servidor.
/// El Dueño no tiene clínica propia: usa la que eligió en el selector de la sesión. Sin clínica
/// no hay acceso a datos de negocio (cierre por defecto). El navegador nunca envía la clínica.
/// </summary>
public sealed class TenantDesdeSesion(ICurrentUserService currentUserService) : ITenantProvider
{
    public int? ClinicaId
    {
        get
        {
            var principal = currentUserService.Principal;
            var valor = principal?.FindFirst(ReclamacionesClinica.ClinicaId)?.Value;
            if (int.TryParse(valor, out var id) && id > 0)
            {
                return id;
            }

            return EsDueno(principal) ? currentUserService.ClinicaSeleccionada : null;
        }
    }

    public bool EsAdministradorGlobal => EsDueno(currentUserService.Principal);

    private static bool EsDueno(ClaimsPrincipal? principal) =>
        principal?.IsInRole(RolesSonrisaDigital.Dueno) == true;
}