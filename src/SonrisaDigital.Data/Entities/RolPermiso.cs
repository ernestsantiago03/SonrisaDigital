namespace SonrisaDigital.Data.Entities;

public class RolPermiso
{
    public string RolId { get; set; } = string.Empty;
    public int PermisoId { get; set; }

    public Rol Rol { get; set; } = null!;
    public Permiso Permiso { get; set; } = null!;
}