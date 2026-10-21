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
        new("Reportes.Ver", "Reportes", "Consultar reportes")
    ];

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> DefaultRolePermissions =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [RolesSonrisaDigital.Administrador] = All.Select(permission => permission.Nombre).ToHashSet(),
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