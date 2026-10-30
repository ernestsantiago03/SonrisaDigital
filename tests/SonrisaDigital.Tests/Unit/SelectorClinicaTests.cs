using System.Security.Claims;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Qué clínica ve cada usuario: el de clínica solo la suya; el Dueño, la que eligió en el selector
/// (plan multiclínica, sección 9 y 10.4).
/// </summary>
public sealed class SelectorClinicaTests
{
    [Fact]
    public void UnUsuarioDeClinicaVeSoloSuClinicaEIgnoraElSelector()
    {
        var sesion = new CurrentUserService();
        sesion.Establecer("u1", "Ana", Principal(RolesSonrisaDigital.Administrador, clinicaId: 3));
        sesion.SeleccionarClinica(9);

        var tenant = new TenantDesdeSesion(sesion);

        Assert.Equal(3, tenant.ClinicaId);
        Assert.False(tenant.EsAdministradorGlobal);
    }

    [Fact]
    public void ElDuenoVeLaClinicaQueEligioEnElSelector()
    {
        var sesion = new CurrentUserService();
        sesion.Establecer("d1", "Dueño", Principal(RolesSonrisaDigital.Dueno));
        sesion.SeleccionarClinica(5);

        var tenant = new TenantDesdeSesion(sesion);

        Assert.Equal(5, tenant.ClinicaId);
        Assert.True(tenant.EsAdministradorGlobal);
    }

    [Fact]
    public void ElDuenoSinSeleccionNoVeNingunaClinica()
    {
        var sesion = new CurrentUserService();
        sesion.Establecer("d1", "Dueño", Principal(RolesSonrisaDigital.Dueno));

        var tenant = new TenantDesdeSesion(sesion);

        Assert.Null(tenant.ClinicaId);
        Assert.True(tenant.EsAdministradorGlobal);
    }

    [Fact]
    public void UnUsuarioSinClinicaNoVeNada()
    {
        var sesion = new CurrentUserService();
        sesion.Establecer("u2", "Sin clinica", Principal(RolesSonrisaDigital.Recepcionista));
        sesion.SeleccionarClinica(7);

        var tenant = new TenantDesdeSesion(sesion);

        Assert.Null(tenant.ClinicaId);
        Assert.False(tenant.EsAdministradorGlobal);
    }

    private static ClaimsPrincipal Principal(string rol, int? clinicaId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Name, "Usuario de prueba"),
            new(ClaimTypes.Role, rol)
        };

        if (clinicaId is int id)
        {
            claims.Add(new Claim(ReclamacionesClinica.ClinicaId, id.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba", ClaimTypes.Name, ClaimTypes.Role));
    }
}
