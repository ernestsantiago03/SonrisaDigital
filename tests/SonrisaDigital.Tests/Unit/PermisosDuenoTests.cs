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
    }

    [Fact]
    public void SoloElDuenoGestionaUsuariosRolesYAuditoria()
    {
        var permisosAdministrador = PermissionCatalog.DefaultRolePermissions[RolesSonrisaDigital.Administrador];
        var exclusivos = PermissionCatalog.All
            .Select(permission => permission.Nombre)
            .Where(nombre => nombre.StartsWith("Usuarios.", StringComparison.Ordinal)
                || nombre.StartsWith("Roles.", StringComparison.Ordinal)
                || nombre == "Auditoria.Ver");

        foreach (var permiso in exclusivos)
        {
            Assert.DoesNotContain(permiso, permisosAdministrador);
            Assert.Contains(permiso, PermissionCatalog.DefaultRolePermissions[RolesSonrisaDigital.Dueno]);
        }
    }

    [Fact]
    public void SoloAdministradorYDuenoPuedenEditarAcercaDe()
    {
        Assert.Contains("AcercaDe.Editar", PermissionCatalog.DefaultRolePermissions[RolesSonrisaDigital.Administrador]);
        Assert.Contains("AcercaDe.Editar", PermissionCatalog.DefaultRolePermissions[RolesSonrisaDigital.Dueno]);
        Assert.DoesNotContain("AcercaDe.Editar", PermissionCatalog.DefaultRolePermissions[RolesSonrisaDigital.Recepcionista]);
        Assert.DoesNotContain("AcercaDe.Editar", PermissionCatalog.DefaultRolePermissions[RolesSonrisaDigital.Odontologo]);
    }

    [Fact]
    public void ElDuenoTieneTodosLosPermisos()
    {
        var permisos = PermissionCatalog.DefaultRolePermissions[RolesSonrisaDigital.Dueno];

        Assert.Equal(PermissionCatalog.All.Count, permisos.Count);
        Assert.Contains("Clinicas.Suspender", permisos);
    }
}
