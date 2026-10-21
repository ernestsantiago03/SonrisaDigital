using Microsoft.AspNetCore.Identity;

namespace SonrisaDigital.Data.Entities;

public class Rol : IdentityRole
{
    public string? Descripcion { get; set; }
    public EstadoRegistro Estado { get; set; } = EstadoRegistro.Activo;

    public ICollection<RolPermiso> RolesPermisos { get; set; } = [];
}