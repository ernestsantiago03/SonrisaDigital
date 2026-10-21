# Actores, Usuarios, Roles y Permisos

## 1. Objetivo

Este documento define los tipos de usuarios que utilizarán el sistema, sus responsabilidades, los roles disponibles y los permisos asociados a cada rol.

El objetivo es establecer desde el inicio qué funcionalidades puede utilizar cada tipo de usuario, evitando que las reglas de autorización queden definidas de manera informal durante el desarrollo.

---

# 2. Actores del sistema

El sistema tendrá tres actores principales:

* Administrador.
* Recepcionista.
* Odontólogo.

Cada actor utilizará una cuenta de usuario para autenticarse en el sistema.

La cuenta de usuario determinará el rol o roles que posee y, a partir de estos, los permisos disponibles.

---

# 3. Usuario

Un **usuario** representa una persona autorizada para acceder al sistema.

La cuenta de usuario estará destinada exclusivamente al acceso y control de seguridad. La información específica de la persona podrá estar asociada a una entidad del sistema cuando corresponda.

Por ejemplo, un usuario con rol de odontólogo podrá estar asociado a un registro de odontólogo.

Conceptualmente:

```text
Usuario
   │
   ├── Roles
   │     └── Permisos
   │
   └── Odontólogo (cuando corresponda)
```

No todos los usuarios tienen que estar asociados a un odontólogo. Por ejemplo, un administrador o recepcionista no requiere dicha asociación.

---

# 4. Roles

Un rol representa un conjunto de responsabilidades dentro del sistema.

El sistema tendrá inicialmente tres roles:

```text
Administrador
Recepcionista
Odontólogo
```

Los roles podrán estar asociados a múltiples permisos.

---

# 5. Administrador

## 5.1. Responsabilidad

El Administrador es responsable de la configuración y administración general del sistema.

## 5.2. Acceso

El Administrador tendrá acceso a los módulos administrativos y podrá consultar la información general del sistema.

### Usuarios

* Consultar usuarios.
* Crear usuarios.
* Editar usuarios.
* Activar o desactivar usuarios.
* Asignar roles.

### Roles y permisos

* Consultar roles.
* Crear roles.
* Editar roles.
* Consultar permisos.
* Asignar permisos a roles.

### Pacientes

* Consultar pacientes.
* Registrar pacientes.
* Editar pacientes.
* Activar o desactivar pacientes.

### Odontólogos

* Consultar odontólogos.
* Registrar odontólogos.
* Editar odontólogos.
* Activar o desactivar odontólogos.

### Citas

* Consultar citas.
* Crear citas.
* Editar citas.
* Cancelar citas.
* Cambiar el estado de una cita.

### Tratamientos

* Consultar tratamientos.
* Crear tratamientos del catálogo.
* Editar tratamientos.
* Activar o desactivar tratamientos.
* Consultar tratamientos realizados.

### Pagos

* Consultar pagos.
* Registrar pagos.
* Consultar pagos pendientes.

### Historia clínica

* Consultar información clínica.
* No se utilizará al Administrador como responsable habitual de modificar información clínica.

### Odontograma

* Consultar odontogramas.
* No se utilizará al Administrador como responsable habitual de modificar información clínica.

### Auditoría

* Consultar registros de auditoría.

### Reportes

* Consultar todos los reportes disponibles.

---

# 6. Recepcionista

## 6.1. Responsabilidad

El Recepcionista será responsable principalmente de las operaciones administrativas relacionadas con pacientes, citas y pagos.

## 6.2. Acceso

### Pacientes

* Consultar pacientes.
* Registrar pacientes.
* Editar información administrativa del paciente.
* Activar o desactivar pacientes cuando corresponda.

El Recepcionista no tendrá permisos para modificar información clínica.

### Odontólogos

* Consultar odontólogos.
* Consultar su disponibilidad mediante las citas existentes.

No podrá modificar la información administrativa de los odontólogos.

### Citas

* Consultar citas.
* Crear citas.
* Editar citas.
* Cancelar citas.
* Confirmar citas.
* Registrar estados administrativos de las citas.

### Tratamientos

* Consultar el catálogo de tratamientos.
* Consultar tratamientos realizados.

No podrá modificar información clínica relacionada con los tratamientos.

### Pagos

* Consultar pagos.
* Registrar pagos.
* Consultar pagos pendientes.

### Historia clínica

* No podrá modificar información clínica.
* Podrá consultar únicamente la información necesaria para las operaciones administrativas cuando los permisos específicos del sistema lo permitan.

### Odontograma

* No podrá modificar el odontograma.

El acceso de consulta al odontograma no será necesario para las operaciones habituales del Recepcionista.

### Reportes

Podrá consultar reportes administrativos relacionados con:

* Citas.
* Pacientes.
* Pagos.

No tendrá acceso a reportes clínicos que no sean necesarios para sus funciones.

### Administración

No podrá:

* Gestionar usuarios.
* Gestionar roles.
* Gestionar permisos.
* Consultar auditoría administrativa completa.

---

# 7. Odontólogo

## 7.1. Responsabilidad

El Odontólogo será responsable de gestionar la información clínica de los pacientes y los tratamientos que realiza.

## 7.2. Acceso

### Pacientes

* Consultar pacientes.
* Consultar información necesaria para la atención.
* Registrar información clínica relacionada con sus pacientes cuando corresponda.

No tendrá permisos para eliminar pacientes.

### Citas

* Consultar sus citas.
* Consultar información del paciente asociada a sus citas.
* Marcar una cita como atendida.
* Registrar observaciones relacionadas con la atención.

No podrá administrar las citas de otros odontólogos salvo que se le otorgue explícitamente el permiso correspondiente.

### Historia clínica

* Consultar historia clínica.
* Registrar información clínica.
* Modificar información clínica permitida.
* Registrar consultas.
* Registrar diagnósticos.
* Registrar observaciones.
* Registrar planes de tratamiento.

### Odontograma

* Consultar odontogramas.
* Modificar el estado de las piezas dentales.
* Registrar observaciones relacionadas con piezas dentales.

### Tratamientos

* Consultar el catálogo de tratamientos.
* Registrar tratamientos realizados.
* Modificar información de tratamientos que se encuentren bajo su responsabilidad.
* Actualizar el estado de un tratamiento.

### Pagos

El Odontólogo no será responsable de las operaciones financieras.

No podrá:

* Registrar pagos.
* Modificar pagos.
* Eliminar pagos.

Podrá consultar información económica del tratamiento cuando resulte necesaria para la atención, siempre que exista un permiso específico para ello.

### Reportes

Podrá consultar reportes relacionados con su actividad, como:

* Sus citas.
* Tratamientos realizados.
* Pacientes atendidos.

No tendrá acceso a reportes financieros generales de la clínica.

### Administración

No podrá:

* Gestionar usuarios.
* Gestionar roles.
* Gestionar permisos.
* Gestionar odontólogos.
* Consultar la auditoría administrativa completa.

---

# 8. Permisos

Los permisos representan acciones específicas que un usuario puede realizar sobre una funcionalidad.

Se utilizará una nomenclatura consistente:

```text
<Modulo>.<Accion>
```

Ejemplos:

```text
Pacientes.Ver
Pacientes.Crear
Pacientes.Editar

Citas.Ver
Citas.Crear
Citas.Editar
Citas.Cancelar

Pagos.Ver
Pagos.Crear
```

Para las operaciones clínicas:

```text
HistoriaClinica.Ver
HistoriaClinica.Crear
HistoriaClinica.Editar

Odontograma.Ver
Odontograma.Editar

Tratamientos.Ver
Tratamientos.Crear
Tratamientos.Editar
```

Para administración:

```text
Usuarios.Ver
Usuarios.Crear
Usuarios.Editar
Usuarios.Desactivar

Roles.Ver
Roles.Crear
Roles.Editar

Permisos.Ver
Permisos.Asignar
```

Y para funcionalidades transversales:

```text
Auditoria.Ver
Reportes.Ver
```

---

# 9. Matriz inicial de permisos

La siguiente matriz representa la configuración inicial propuesta.

| Módulo / Acción                    | Administrador | Recepcionista | Odontólogo |
| ---------------------------------- | :-----------: | :-----------: | :--------: |
| Pacientes - Ver                    |       ✓       |       ✓       |      ✓     |
| Pacientes - Crear                  |       ✓       |       ✓       |      —     |
| Pacientes - Editar                 |       ✓       |       ✓       |      —     |
| Pacientes - Desactivar             |       ✓       |       ✓       |      —     |
| Odontólogos - Ver                  |       ✓       |       ✓       |      ✓     |
| Odontólogos - Crear                |       ✓       |       —       |      —     |
| Odontólogos - Editar               |       ✓       |       —       |      —     |
| Odontólogos - Desactivar           |       ✓       |       —       |      —     |
| Citas - Ver                        |       ✓       |       ✓       |     ✓*     |
| Citas - Crear                      |       ✓       |       ✓       |      —     |
| Citas - Editar                     |       ✓       |       ✓       |      —     |
| Citas - Cancelar                   |       ✓       |       ✓       |      —     |
| Citas - Atender                    |       ✓       |       —       |      ✓     |
| Historia Clínica - Ver             |       ✓       |       ✓       |      ✓     |
| Historia Clínica - Crear           |       —       |       —       |      ✓     |
| Historia Clínica - Editar          |       —       |       —       |      ✓     |
| Odontograma - Ver                  |       ✓       |       ✓       |      ✓     |
| Odontograma - Editar               |       —       |       —       |      ✓     |
| Tratamientos - Ver                 |       ✓       |       ✓       |      ✓     |
| Tratamientos - Crear catálogo      |       ✓       |       —       |      —     |
| Tratamientos - Editar catálogo     |       ✓       |       —       |      —     |
| Tratamientos - Registrar realizado |       —       |       —       |      ✓     |
| Tratamientos - Editar realizado    |       —       |       —       |      ✓     |
| Pagos - Ver                        |       ✓       |       ✓       |      —     |
| Pagos - Crear                      |       ✓       |       ✓       |      —     |
| Pagos - Editar                     |       ✓       |       —       |      —     |
| Usuarios - Ver                     |       ✓       |       —       |      —     |
| Usuarios - Crear                   |       ✓       |       —       |      —     |
| Usuarios - Editar                  |       ✓       |       —       |      —     |
| Usuarios - Desactivar              |       ✓       |       —       |      —     |
| Roles - Ver                        |       ✓       |       —       |      —     |
| Roles - Crear                      |       ✓       |       —       |      —     |
| Roles - Editar                     |       ✓       |       —       |      —     |
| Permisos - Ver                     |       ✓       |       —       |      —     |
| Permisos - Asignar                 |       ✓       |       —       |      —     |
| Auditoría - Ver                    |       ✓       |       —       |      —     |
| Reportes - Administrativos         |       ✓       |       ✓       |      —     |
| Reportes - Clínicos                |       ✓       |       —       |      ✓     |

`*` El Odontólogo verá principalmente sus propias citas.

**Historia Clínica y Odontograma (solo lectura para Recepción).** El Recepcionista puede
**ver** la historia clínica y el odontograma, pero siempre en modo lectura: no tiene los
permisos *Crear* ni *Editar*. Así puede consultar en mostrador datos útiles (por ejemplo si
el paciente tiene alergias registradas) sin poder modificar información clínica, que queda
reservada al Odontólogo.

---
