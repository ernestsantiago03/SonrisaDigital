# Revisión periódica de accesos

Revisar quién tiene acceso a qué, al menos cada **3 meses** y después de cualquier baja de personal. Esta revisión la hace el Dueño de la plataforma con el apoyo del Administrador de cada clínica.

## 1. Cuentas del Dueño

- Listar las cuentas del rol Dueño. Debe haber al menos dos activas (regla del sistema, `SeguridadDueno.DuenosActivosMinimos`).
- Confirmar que cada una tiene el segundo factor activo. Si una no lo tiene, el sistema la obliga a activarlo al entrar.
- Desactivar las cuentas que ya no correspondan con `dotnet SonrisaDigital.UI.dll desactivar-dueno <correo>`. Solo se permite si después siguen quedando dos Dueños activos.
- Si alguien perdió su segundo factor: `dotnet SonrisaDigital.UI.dll restablecer-mfa-dueno <correo>`, que pide confirmación escribiendo `SI`.

## 2. Usuarios de cada clínica

Para cada clínica (desde **Clínicas** o con el selector de clínica):

- Revisar la pantalla **Usuarios**. Desactivar a quien ya no trabaje en la clínica. Un usuario desactivado pierde el acceso en menos de 30 s.
- Revisar que cada usuario tenga solo el rol que necesita (Administrador, Odontólogo o Recepcionista).
- Confirmar que no hay cuentas compartidas. El uso compartido de cuentas está prohibido por los términos de uso.

## 3. Roles y permisos

- En **Roles**, revisar qué permisos tiene cada rol. Un cambio de permisos se refleja al siguiente ciclo de revalidación de la sesión.
- Confirmar que los permisos de **Archivos.Ocultar**, **Clinicas.Suspender** y **Auditoria.Ver** solo están en roles que los necesitan.

## 4. Auditoría

En **Auditoría** (requiere `Auditoria.Ver`), revisar el último trimestre:

- Eliminaciones de archivos clínicos (`Eliminó`, módulo *Archivos del paciente*).
- Cambios de roles y permisos.
- Desactivaciones y reactivaciones de usuarios y clínicas.
- Pagos o cambios de suscripción fuera de lo habitual.

Documentar las anomalías encontradas. Si alguna indica un incidente, aplicar el [plan de respuesta a incidentes](plan-respuesta-incidentes.md).

## 5. Registro de la revisión

| Fecha | Revisó | Cuentas Dueño | Usuarios desactivados | Anomalías | Acciones |
|---|---|---|---|---|---|
| | | | | | |

Guardar el registro con la fecha de la revisión.
