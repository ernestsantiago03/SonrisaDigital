using SonrisaDigital.Services.Autorizacion;

namespace SonrisaDigital.Tests.Unit;

/// <summary>Reglas del panel de clínicas: solo el Dueño lo tiene (plan multiclínica, sección 10.4).</summary>
public sealed class PermisosDuenoTests
{
    [Fact]
    public void ElCatalogoTieneLosCuatroPermisosDeClinicas()
    {
        var clinicas = PermissionCatalog.All.Where(permission => PermissionCatalog.EsPermisoDeClinicas(permission.Nombre))
            .Select(permission => permission.Nombre)
            .ToHashSet();

        Assert.Equal(new[] { "Clinicas.Ver", "Clinicas.Crear", "Clinicas.Editar", "Clinicas.Suspender" }.ToHashSet(), clinicas);
    }

    [Fact]
    public void ElAdministradorNoRecibePermisosDeClinicas()
    {
        var permisos = PermissionCatalog.DefaultRolePermissions[RolesSonrisaDigital.Administrador];

        Assert.DoesNotContain(permisos, PermissionCatalog.EsPermisoDeClinicas);
        Assert.Contains("Usuarios.Editar", permisos);
    }

    [Fact]
    public void ElDuenoTieneTodosLosPermisos()
    {
        var permisos = PermissionCatalog.DefaultRolePermissions[RolesSonrisaDigital.Dueno];

        Assert.Equal(PermissionCatalog.All.Count, permisos.Count);
        Assert.Contains("Clinicas.Suspender", permisos);
    }
}
