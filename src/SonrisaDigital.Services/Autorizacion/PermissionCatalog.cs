namespace SonrisaDigital.Services.Autorizacion;

public static class PermissionCatalog
{
    public const string ClaimType = "sonrisadigital.permission";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new("Pacientes.Ver", "Pacientes", "Consultar pacientes"),
        new("Pacientes.Crear", "Pacientes", "Registrar pacientes"),
        new("Pacientes.Editar", "Pacientes", "Editar pacientes"),
        new("Pacientes.Desactivar", "Pacientes", "Activar o desactivar pacientes"),
        new("Odontologos.Ver", "Odontólogos", "Consultar odontólogos"),
        new("Odontologos.Crear", "Odontólogos", "Registrar odontólogos"),
        new("Odontologos.Editar", "Odontólogos", "Editar odontólogos"),
        new("Odontologos.Desactivar", "Odontólogos", "Activar o desactivar odontólogos"),
        new("Citas.Ver", "Citas", "Consultar citas"),
        new("Citas.Crear", "Citas", "Crear citas"),
        new("Citas.Editar", "Citas", "Editar citas"),
        new("Citas.Cancelar", "Citas", "Cancelar citas"),
        new("Citas.Atender", "Citas", "Atender citas"),
        new("HistoriaClinica.Ver", "Historia Clínica", "Consultar historias clínicas"),
        new("HistoriaClinica.Crear", "Historia Clínica", "Crear información clínica"),
        new("HistoriaClinica.Editar", "Historia Clínica", "Editar información clínica"),
        new("Odontograma.Ver", "Odontograma", "Consultar odontogramas"),
        new("Odontograma.Editar", "Odontograma", "Editar odontogramas"),
        new("Tratamientos.Ver", "Tratamientos", "Consultar tratamientos"),
        new("Tratamientos.Crear", "Tratamientos", "Registrar tratamientos"),
        new("Tratamientos.Editar", "Tratamientos", "Editar tratamientos"),
        new("Pagos.Ver", "Pagos", "Consultar pagos"),
        new("Pagos.Crear", "Pagos", "Registrar pagos"),
        new("Pagos.Editar", "Pagos", "Editar pagos"),
        new("Usuarios.Ver", "Usuarios", "Consultar usuarios"),
        new("Usuarios.Crear", "Usuarios", "Crear usuarios"),
        new("Usuarios.Editar", "Usuarios", "Editar usuarios y roles"),
        new("Usuarios.Desactivar", "Usuarios", "Activar o desactivar usuarios"),
        new("Roles.Ver", "Roles y Permisos", "Consultar roles y permisos"),
        new("Roles.Editar", "Roles y Permisos", "Asignar permisos a roles"),
        new("Auditoria.Ver", "Auditoría", "Consultar auditoría"),
        new("Reportes.Ver", "Reportes", "Consultar reportes"),
        new("Clinicas.Ver", "Clínicas", "Consultar clínicas (solo Dueño)"),
        new("Clinicas.Crear", "Clínicas", "Crear clínicas (solo Dueño)"),
        new("Clinicas.Editar", "Clínicas", "Editar clínicas (solo Dueño)"),
        new("Clinicas.Suspender", "Clínicas", "Suspender o reactivar clínicas (solo Dueño)"),
        new("AcercaDe.Editar", "Acerca De", "Editar la información de la clínica (Administrador y Dueño)"),
        new("Archivos.Ocultar", "Archivos del paciente", "Ocultar y restaurar archivos del paciente (solo Dueño)")
    ];

    /// <summary>Permisos del panel de clínicas. Solo los tiene el rol Dueño.</summary>
    public static bool EsPermisoDeClinicas(string nombre) => nombre.StartsWith("Clinicas.", StringComparison.Ordinal);

    /// <summary>
    /// Permisos que solo puede tener el Dueño. No es una configuración por defecto:
    /// el sistema rechaza cualquier intento de asignarlos a otro rol.
    /// </summary>
    public static bool EsPermisoExclusivoDueno(string nombre) =>
        EsPermisoDeClinicas(nombre)
        || nombre.StartsWith("Usuarios.", StringComparison.Ordinal)
        || nombre.StartsWith("Roles.", StringComparison.Ordinal)
        || string.Equals(nombre, "Auditoria.Ver", StringComparison.Ordinal)
        || string.Equals(nombre, "Archivos.Ocultar", StringComparison.Ordinal);

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> DefaultRolePermissions =
        new Dictionary<string, IReadOnlySet<string>>
        {
            // El Administrador de clínica gestiona su clínica, nunca el panel de clínicas,
            // ni usuarios, roles y permisos o auditoría: esas tres áreas son solo del Dueño.
            [RolesSonrisaDigital.Dueno] = All.Select(permission => permission.Nombre).ToHashSet(),
            [RolesSonrisaDigital.Administrador] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Pacientes.Ver", "Pacientes.Crear", "Pacientes.Editar", "Pacientes.Desactivar",
                "Odontologos.Ver", "Odontologos.Crear", "Odontologos.Editar", "Odontologos.Desactivar",
                "Citas.Ver", "Citas.Crear", "Citas.Editar", "Citas.Cancelar", "Citas.Atender",
                "HistoriaClinica.Ver", "HistoriaClinica.Crear", "HistoriaClinica.Editar",
                "Odontograma.Ver", "Odontograma.Editar",
                "Tratamientos.Ver", "Tratamientos.Crear", "Tratamientos.Editar",
                "Pagos.Ver", "Pagos.Crear", "Pagos.Editar",
                "Reportes.Ver",
                "AcercaDe.Editar"
            },
            [RolesSonrisaDigital.Recepcionista] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Pacientes.Ver", "Pacientes.Crear", "Pacientes.Editar", "Pacientes.Desactivar",
                "Odontologos.Ver", "Citas.Ver", "Citas.Crear", "Citas.Editar", "Citas.Cancelar",
                "HistoriaClinica.Ver", "Odontograma.Ver", "Tratamientos.Ver",
                "Pagos.Ver", "Pagos.Crear", "Reportes.Ver"
            },
            [RolesSonrisaDigital.Odontologo] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Pacientes.Ver", "Odontologos.Ver", "Citas.Ver", "Citas.Atender", "HistoriaClinica.Ver", "HistoriaClinica.Crear",
                "HistoriaClinica.Editar", "Odontograma.Ver", "Odontograma.Editar", "Tratamientos.Ver",
                "Tratamientos.Crear", "Tratamientos.Editar", "Reportes.Ver"
            }
        };
}

public sealed record PermissionDefinition(string Nombre, string Modulo, string Descripcion);