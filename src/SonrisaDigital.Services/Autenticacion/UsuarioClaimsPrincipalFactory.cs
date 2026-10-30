using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Autorizacion;
using System.Security.Claims;

namespace SonrisaDigital.Services.Autenticacion;

public sealed class UsuarioClaimsPrincipalFactory : UserClaimsPrincipalFactory<Usuario, Rol>
{
    private readonly UserManager<Usuario> userManager;
    private readonly ApplicationDbContext dbContext;

    public UsuarioClaimsPrincipalFactory(
        UserManager<Usuario> userManager,
        RoleManager<Rol> roleManager,
        IOptions<IdentityOptions> optionsAccessor,
        ApplicationDbContext dbContext)
        : base(userManager, roleManager, optionsAccessor)
    {
        this.userManager = userManager;
        this.dbContext = dbContext;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Usuario user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var nameClaim = identity.FindFirst(ClaimTypes.Name);

        if (nameClaim is not null)
        {
            identity.RemoveClaim(nameClaim);
        }

        identity.AddClaim(new Claim(ClaimTypes.Name, user.Nombre));
        identity.AddClaim(new Claim(ClaimTypes.GivenName, user.Nombre));

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await dbContext.RolesPermisos
            .Where(rolePermission => roles.Contains(rolePermission.Rol.Name!))
            .Select(rolePermission => rolePermission.Permiso.Nombre)
            .Distinct()
            .ToListAsync();

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim(PermissionCatalog.ClaimType, permission));
        }

        // La clínica va firmada en la sesión. Se regenera en cada petición (ValidationInterval = 0),
        // así que un cambio de clínica del usuario surte efecto de inmediato.
        if (user.ClinicaId is int clinicaId)
        {
            identity.AddClaim(new Claim(ReclamacionesClinica.ClinicaId, clinicaId.ToString()));
        }

        return identity;
    }
}