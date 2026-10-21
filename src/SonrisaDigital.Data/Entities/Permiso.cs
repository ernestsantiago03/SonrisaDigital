namespace SonrisaDigital.Data.Entities;

public class Permiso
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Modulo { get; set; } = string.Empty;

    public ICollection<RolPermiso> RolesPermisos { get; set; } = [];
}