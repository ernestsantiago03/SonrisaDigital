# 07. Arquitectura del Sistema

## 1. Propósito

El sistema utilizará una arquitectura por capas sencilla, orientada a separar las responsabilidades principales de la aplicación.

La solución estará dividida en tres capas:

1. **UI** — Interfaz de usuario.
2. **Services** — Lógica de aplicación y reglas de negocio.
3. **Data** — Acceso y persistencia de datos.

El objetivo es mantener el sistema organizado y fácil de desarrollar y mantener, sin introducir una cantidad de capas o patrones innecesarios para el alcance del proyecto.

La aplicación será un **monolito modular**, desarrollado como una única solución .NET.

---

# 2. Arquitectura general

La arquitectura seguirá el siguiente flujo:

```text
┌───────────────────────────────┐
│              UI               │
│             Blazor            │
│                               │
│  Páginas / Componentes / UI   │
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│           Services            │
│                               │
│ Casos de uso                  │
│ Lógica de negocio             │
│ Validaciones                  │
│ Autorización                  │
│ Auditoría                     │
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│             Data              │
│                               │
│ Entity Framework Core         │
│ Repositorios                  │
│ ASP.NET Core Identity         │
│ Persistencia                  │
└───────────────┬───────────────┘
                │
                ▼
          ┌───────────┐
          │ SQL Server│
          └───────────┘
```

Las dependencias deberán seguir siempre el siguiente sentido:

```text
UI → Services → Data
```

La UI no debe acceder directamente a la base de datos ni utilizar directamente `DbContext`.

---

# 3. Proyectos de la solución

La solución tendrá inicialmente tres proyectos principales:

```text
src/
├── SonrisaDigital.UI/
├── SonrisaDigital.Services/
└── SonrisaDigital.Data/
```

Cada proyecto tendrá una responsabilidad definida.

---

# 4. Capa UI

Proyecto:

```text
SonrisaDigital.UI
```

Esta capa contiene toda la interfaz de usuario desarrollada con Blazor.

## Responsabilidades

La UI será responsable de:

* Mostrar información.
* Recibir acciones del usuario.
* Mostrar formularios.
* Mostrar tablas.
* Mostrar mensajes de validación.
* Mostrar estados de carga.
* Mostrar confirmaciones.
* Gestionar navegación.
* Mostrar u ocultar elementos según permisos.
* Invocar los servicios correspondientes.

La UI **no debe contener reglas de negocio importantes**.

Por ejemplo, un componente de Blazor no debe determinar por sí mismo si un pago puede registrarse o si una cita se solapa con otra.

Esas decisiones pertenecen a la capa de Services.

---

# 5. Organización de la UI

La estructura podrá organizarse por módulos funcionales:

```text
SonrisaDigital.UI/
│
├── Pages/
│   ├── Dashboard/
│   ├── Pacientes/
│   ├── Odontologos/
│   ├── Citas/
│   ├── HistoriaClinica/
│   ├── Odontograma/
│   ├── Tratamientos/
│   ├── Pagos/
│   ├── Usuarios/
│   ├── RolesPermisos/
│   ├── Auditoria/
│   └── Reportes/
│
├── Components/
│   ├── Common/
│   ├── Pacientes/
│   ├── Citas/
│   ├── Tratamientos/
│   └── ...
│
├── Layout/
│
├── Services/
│
└── wwwroot/
```

Los componentes reutilizables deberán colocarse en `Components`.

Ejemplos:

```text
Components/
├── Common/
│   ├── ConfirmDialog.razor
│   ├── LoadingIndicator.razor
│   ├── StatusBadge.razor
│   └── EmptyState.razor
│
├── Pacientes/
│   └── PacienteCard.razor
│
└── Citas/
    └── CitaStatusBadge.razor
```

La estructura exacta podrá modificarse durante el desarrollo si mejora la organización.

---

# 6. Capa Services

Proyecto:

```text
SonrisaDigital.Services
```

Esta capa será responsable de coordinar el comportamiento de la aplicación.

Aquí se implementarán las funcionalidades que el usuario puede ejecutar en el sistema.

## Responsabilidades

* Casos de uso.
* Lógica de negocio.
* Validaciones de negocio.
* Consultas necesarias para las funcionalidades.
* Modificaciones de información.
* Autorización por permisos.
* Coordinación de operaciones relacionadas.
* Generación de registros de auditoría.
* Preparación de información para la UI.

Esta capa representa el punto principal donde se decide **qué puede hacer el sistema**.

---

# 7. Servicios por módulo

Los servicios se organizarán según los módulos funcionales:

```text
SonrisaDigital.Services/
│
├── Pacientes/
├── Odontologos/
├── Citas/
├── HistoriaClinica/
├── Odontograma/
├── Tratamientos/
├── Pagos/
├── Usuarios/
├── RolesPermisos/
├── Auditoria/
└── Reportes/
```

Ejemplos:

```text
PacienteService
OdontologoService
CitaService
HistoriaClinicaService
OdontogramaService
TratamientoService
PagoService
UsuarioService
RolService
AuditoriaService
ReporteService
```

No es obligatorio que cada módulo tenga exactamente un servicio. Si un módulo necesita separar responsabilidades, podrá utilizar varios servicios.

La estructura debe mantenerse sencilla.

---


La página de Blazor no debe realizar directamente las validaciones de negocio ni consultar la base de datos.

---

# 9. Entidades

Las entidades principales del sistema podrán mantenerse dentro de la capa de Services, ya que no se utilizará una capa Domain independiente.

Ejemplo:

```text
SonrisaDigital.Services/
├── Entities/
│   ├── Paciente.cs
│   ├── Odontologo.cs
│   ├── Cita.cs
│   ├── HistoriaClinica.cs
│   ├── ConsultaClinica.cs
│   ├── PiezaDental.cs
│   ├── RegistroOdontograma.cs
│   ├── Tratamiento.cs
│   ├── TratamientoRealizado.cs
│   ├── Pago.cs
│   ├── Usuario.cs
│   ├── Rol.cs
│   ├── Permiso.cs
│   └── RegistroAuditoria.cs
```

Estas entidades representan los conceptos definidos en `06-modelo-de-dominio.md`.

No se deberá crear una entidad únicamente porque exista una pantalla o una consulta.

---




# 12. Capa Data

Proyecto:

```text
SonrisaDigital.Data
```

Esta capa contiene todo lo relacionado con la persistencia y acceso a datos.

## Responsabilidades

* Entity Framework Core.
* `DbContext`.
* Configuración de entidades.
* Repositorios.
* Consultas a SQL Server.
* ASP.NET Core Identity.
* Persistencia de usuarios, roles y permisos.
* Implementación de mecanismos de almacenamiento.

La capa Data no debe contener lógica propia de la interfaz.

---


# 14. Entity Framework Core

Entity Framework Core será utilizado como ORM.

El `ApplicationDbContext` será el punto principal de acceso a las entidades persistidas.

Ejemplo conceptual:

```text
Services
   ↓
Repository
   ↓
ApplicationDbContext
   ↓
Entity Framework Core
   ↓
SQL Server
```

La UI no debe utilizar directamente `ApplicationDbContext`.

---

# 15. Repositorios

Los repositorios estarán ubicados en Data y serán utilizados por Services para realizar operaciones de persistencia.

Ejemplo:

```text
IPacienteRepository
PacienteRepository

ICitaRepository
CitaRepository

IPagoRepository
PagoRepository
```

Cuando una operación sea sencilla y no justifique un repositorio específico, podrá utilizarse directamente un servicio de acceso a datos apropiado.

El objetivo no es crear una abstracción por cada tabla de forma automática, sino mantener una separación clara entre lógica y persistencia.

---

# 16. Autenticación

La autenticación será implementada utilizando **ASP.NET Core Identity**.

Identity será responsable de aspectos como:

* Usuarios.
* Credenciales.
* Contraseñas.
* Estados de cuenta.
* Roles relacionados con autenticación.

Los detalles de persistencia de Identity pertenecerán a Data.

La UI únicamente interactuará con los mecanismos de autenticación proporcionados por la aplicación.

---

# 17. Autorización

La autorización se basará en los roles y permisos definidos en:

`02-actores-y-roles.md`

Los permisos seguirán el formato:

```text
<Modulo>.<Accion>
```

Ejemplos:

```text
Pacientes.Ver
Pacientes.Crear
Pacientes.Editar
Citas.Crear
Citas.Cancelar
HistoriaClinica.Ver
HistoriaClinica.Editar
Odontograma.Editar
Pagos.Crear
Usuarios.Editar
Auditoria.Ver
Reportes.Ver
```

La autorización deberá comprobarse en la capa Services.

La UI podrá ocultar opciones que el usuario no pueda utilizar, pero esto solamente constituye una mejora de experiencia de usuario.

La seguridad real deberá mantenerse en Services.

---

# 18. Auditoría

La auditoría será manejada desde Services.

Cuando se realice una operación relevante, el servicio correspondiente podrá registrar una acción en la auditoría.

Ejemplo:

```text
PacienteService
      │
      ├── Actualizar paciente
      │
      └── AuditoriaService.Registrar(...)
                         │
                         ▼
                  AuditRepository
                         │
                         ▼
                    SQL Server
```

Las operaciones relevantes incluyen:

* Crear paciente.
* Editar paciente.
* Desactivar paciente.
* Crear o modificar citas.
* Modificar historia clínica.
* Modificar odontograma.
* Registrar tratamientos.
* Registrar pagos.
* Modificar usuarios.
* Modificar roles y permisos.

Los registros de auditoría serán de solo lectura desde la aplicación.

---

# 19. Reportes

Los reportes serán implementados principalmente mediante consultas de lectura.

El flujo será:

```text
ReportePage
     ↓
ReporteService
     ↓
Repositorio / consulta
     ↓
SQL Server
     ↓
Resultado
     ↓
UI
```

Los reportes iniciales serán:

* Citas.
* Pacientes.
* Tratamientos.
* Pagos.

Los reportes no serán tratados como entidades persistentes.

---

# 20. Manejo de errores

Los errores de negocio deberán ser controlados desde Services.

Ejemplo:

```text
PagoService
    ↓
¿Monto > saldo pendiente?
    │
    ├── Sí → Registrar pago
    │
    └── No → Retornar error de negocio
```

La UI deberá presentar el error de forma comprensible.

Ejemplo:

```text
No se puede registrar el pago porque
el monto supera el saldo pendiente.
```

No se deberán mostrar directamente al usuario:

* Stack traces.
* Excepciones completas.
* Consultas SQL.
* Información interna de Entity Framework.
* Información sensible de configuración.

Los errores inesperados deberán registrarse mediante logging técnico.

---

# 21. Transacciones

Las operaciones que involucren varias modificaciones que deban completarse juntas deberán ejecutarse dentro de una transacción.

Por ejemplo:

```text
Registrar tratamiento
       +
Registrar auditoría
```

deberá evitar que una de las operaciones se complete mientras la otra falla cuando ambas sean necesarias para mantener la consistencia.

Las transacciones deberán gestionarse desde la capa Services o mediante mecanismos proporcionados por Data.

No todas las operaciones de lectura requieren una transacción explícita.

---

# 22. Validaciones

Las validaciones se dividirán entre UI y Services.

## UI

Validaciones relacionadas con la entrada:

* Campos obligatorios.
* Formato de correo.
* Longitud.
* Formato de fecha.
* Formato numérico.

## Services

Validaciones relacionadas con las reglas del sistema:

* Existencia de entidades.
* Estados.
* Permisos.
* Relaciones entre entidades.
* Solapamiento de citas.
* Saldos de tratamientos.
* Restricciones de operaciones.

Una regla de negocio importante nunca deberá depender únicamente de una validación en Blazor.

---



# 24. Seguridad

El sistema deberá implementar como mínimo:

* Autenticación mediante ASP.NET Core Identity.
* Autorización mediante roles y permisos.
* Validación de permisos en Services.
* Protección de páginas y operaciones administrativas.
* Auditoría de operaciones importantes.
* No almacenar secretos dentro del código fuente.
* Utilizar configuración segura para la conexión a SQL Server.
* No exponer información técnica innecesaria al usuario.

El acceso a información clínica y administrativa deberá respetar los permisos definidos para cada rol.

---




# 30. Tecnologías

La implementación utilizará inicialmente:

| Componente           | Tecnología                           |
| -------------------- | ------------------------------------ |
| Lenguaje             | C#                                   |
| Framework            | .NET / ASP.NET Core                  |
| Interfaz             | Blazor                               |
| ORM                  | Entity Framework Core                |
| Base de datos        | SQL Server                           |
| Autenticación        | ASP.NET Core Identity                |
| Estilos              | CSS / librería compatible con Blazor |
| Control de versiones | Git                                  |
| Arquitectura         | Monolito por capas                   |

No se utilizará Bootstrap como requisito arquitectónico. La interfaz podrá utilizar CSS propio o una librería de componentes compatible con Blazor, siempre que mantenga el diseño definido para el sistema.

---
