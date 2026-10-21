# Sistema de Gestión para Clínica Dental

## 1. Descripción general

El proyecto consiste en el desarrollo de un sistema web para la gestión de las operaciones principales de una clínica dental.

El sistema permitirá centralizar la información relacionada con pacientes, odontólogos, citas, historia clínica, odontogramas, tratamientos y pagos, además de proporcionar mecanismos de administración de usuarios, roles y permisos, auditoría y generación de reportes.

El objetivo del proyecto es desarrollar una solución funcional que permita demostrar la aplicación de principios de ingeniería de software, arquitectura por capas, persistencia de datos, control de acceso y gestión de información dentro de un contexto de clínica dental.

El sistema estará orientado principalmente al personal interno de la clínica. No se contempla inicialmente un portal externo para pacientes.

---

## 2. Objetivos

### 2.1. Objetivo general

Desarrollar un sistema web para gestionar de manera centralizada la información clínica y administrativa de una clínica dental, aplicando una arquitectura por capas y buenas prácticas de desarrollo de software.

### 2.2. Objetivos específicos

* Gestionar el registro y la información de los pacientes.
* Gestionar los odontólogos pertenecientes a la clínica.
* Administrar las citas entre pacientes y odontólogos.
* Mantener la historia clínica de los pacientes.
* Registrar y consultar el odontograma de cada paciente.
* Gestionar el catálogo y registro de tratamientos.
* Registrar los pagos asociados a los tratamientos.
* Administrar usuarios, roles y permisos de acceso.
* Registrar las operaciones relevantes realizadas por los usuarios mediante un mecanismo de auditoría.
* Generar reportes básicos sobre la información administrativa y clínica.
* Implementar el sistema utilizando una arquitectura por capas que permita separar responsabilidades.

---

## 3. Alcance funcional

El sistema incluirá los siguientes módulos:

### 3.1. Gestión de pacientes

Permitirá registrar, consultar, modificar y administrar la información básica de los pacientes.

La información podrá incluir:

* Datos personales.
* Información de contacto.
* Fecha de nacimiento.
* Dirección.
* Contacto de emergencia.
* Estado del paciente.
* Fecha de registro.

Los pacientes que posean información clínica o transaccional no serán eliminados físicamente del sistema. En estos casos se utilizará un estado que permita mantenerlos como inactivos.

---

### 3.2. Gestión de odontólogos

Permitirá administrar la información de los odontólogos que trabajan en la clínica.

La información podrá incluir:

* Nombre y apellidos.
* Número de licencia o colegiatura.
* Especialidad.
* Información de contacto.
* Estado.

Cada odontólogo podrá estar asociado a un usuario del sistema para permitirle acceder a las funcionalidades correspondientes a su rol.

---

### 3.3. Gestión de citas

Permitirá crear y administrar las citas entre pacientes y odontólogos.

Una cita deberá contener como mínimo:

* Paciente.
* Odontólogo.
* Fecha.
* Hora.
* Motivo.
* Estado.
* Observaciones.

Los estados contemplados inicialmente serán:

* Programada.
* Confirmada.
* Atendida.
* Cancelada.
* No asistió.

El sistema deberá evitar que un mismo odontólogo tenga dos citas activas en el mismo horario.

---

### 3.4. Historia clínica

Permitirá mantener la información clínica básica de cada paciente.

La historia clínica podrá incluir:

* Alergias.
* Enfermedades relevantes.
* Medicamentos.
* Antecedentes médicos.
* Antecedentes odontológicos.
* Hábitos.
* Observaciones.

Además, se mantendrá un historial de las consultas realizadas al paciente, incluyendo información como:

* Fecha.
* Odontólogo.
* Motivo de consulta.
* Observaciones.
* Diagnóstico.
* Plan de tratamiento.

El acceso y modificación de esta información estará restringido de acuerdo con los permisos del usuario.

---

### 3.5. Odontograma

El sistema contará con un odontograma asociado a cada paciente para representar el estado de sus piezas dentales.

Inicialmente se utilizará la numeración dental FDI.

Cada pieza dental podrá tener un estado clínico simplificado, por ejemplo:

* Sano.
* Caries.
* Restauración.
* Ausente.
* Fracturado.
* Tratamiento realizado.

El usuario autorizado podrá seleccionar una pieza y registrar información adicional relacionada con ella.

El odontograma tendrá como objetivo representar de manera sencilla el estado dental del paciente, sin intentar reproducir todas las funcionalidades de un software odontológico profesional.

---

### 3.6. Gestión de tratamientos

El sistema permitirá administrar un catálogo de tratamientos disponibles en la clínica.

Cada tratamiento del catálogo podrá contener:

* Nombre.
* Descripción.
* Precio base.
* Estado.

Los tratamientos del catálogo se diferenciarán de los tratamientos realizados a los pacientes.

Un tratamiento realizado podrá registrar:

* Paciente.
* Tratamiento.
* Odontólogo.
* Fecha.
* Pieza dental relacionada, cuando corresponda.
* Precio.
* Estado.
* Observaciones.

Los estados podrán incluir:

* Pendiente.
* En proceso.
* Completado.
* Cancelado.

---

### 3.7. Gestión de pagos

El sistema permitirá registrar los pagos asociados a tratamientos.

Cada pago podrá contener:

* Paciente.
* Tratamiento realizado.
* Fecha.
* Monto.
* Método de pago.
* Estado.

Los métodos de pago contemplados inicialmente serán:

* Efectivo.
* Tarjeta.
* Transferencia.

El sistema permitirá registrar pagos parciales y determinar el monto pendiente de un tratamiento.

No se contempla inicialmente la integración directa con bancos, procesadores de pago o sistemas contables externos.

---

### 3.8. Usuarios, roles y permisos

El sistema contará con autenticación para los usuarios internos de la clínica.

Se contemplarán inicialmente los siguientes roles:

#### Administrador

Responsable de la administración general del sistema.

Tendrá acceso a:

* Usuarios.
* Roles y permisos.
* Pacientes.
* Odontólogos.
* Citas.
* Tratamientos.
* Pagos.
* Auditoría.
* Reportes.

#### Recepcionista

Responsable principalmente de las operaciones administrativas.

Tendrá acceso a:

* Pacientes.
* Citas.
* Odontólogos.
* Tratamientos.
* Pagos.

No tendrá permisos para modificar información clínica.

#### Odontólogo

Responsable de las operaciones clínicas.

Tendrá acceso a:

* Sus citas.
* Pacientes.
* Historia clínica.
* Odontograma.
* Tratamientos.

No tendrá acceso a la administración general de usuarios y permisos.

Los permisos se gestionarán de manera independiente de los roles, permitiendo que un rol tenga múltiples permisos y que los permisos puedan ser utilizados para controlar el acceso a funcionalidades específicas.

---

### 3.9. Auditoría

El sistema registrará las operaciones relevantes realizadas por los usuarios.

Los registros de auditoría podrán incluir:

* Fecha y hora.
* Usuario.
* Acción realizada.
* Entidad afectada.
* Identificador de la entidad.
* Descripción de la operación.

Entre las operaciones que podrán auditarse se encuentran:

* Inicio de sesión.
* Creación de registros.
* Modificación de registros.
* Desactivación de registros.
* Registro de información clínica.
* Registro de pagos.
* Cambios relacionados con usuarios, roles o permisos.

No se pretende registrar cada interacción realizada en la interfaz, sino las operaciones relevantes para la trazabilidad del sistema.

---

### 3.10. Reportes

El sistema proporcionará reportes básicos para facilitar la consulta de información.

Inicialmente se contemplan:

* Citas por período.
* Citas por odontólogo.
* Citas por estado.
* Pacientes registrados.
* Tratamientos realizados.
* Tratamientos más realizados.
* Pagos por período.
* Ingresos por período.
* Pagos pendientes.

Los reportes estarán orientados a la consulta de información y no constituirán un sistema contable.

---

## 4. Usuarios del sistema

El sistema estará orientado a tres tipos principales de usuarios internos:

| Usuario       | Responsabilidad principal                      |
| ------------- | ---------------------------------------------- |
| Administrador | Administración general del sistema             |
| Recepcionista | Gestión administrativa y atención de pacientes |
| Odontólogo    | Gestión de información clínica y tratamientos  |

La autorización se realizará utilizando roles y permisos.

---

## 5. Alcance técnico inicial

El sistema será desarrollado como una aplicación web utilizando tecnologías del ecosistema .NET.

La arquitectura inicial contemplará:

* **Blazor** para la interfaz de usuario.
* **ASP.NET Core / .NET** como plataforma de desarrollo.
* **Entity Framework Core** para el acceso a datos.
* **SQL Server** como sistema gestor de base de datos.
* **ASP.NET Core Identity** para la gestión de autenticación y usuarios.
* **Bootstrap o una librería de componentes compatible con Blazor** para facilitar el desarrollo de la interfaz.
* **Git** para el control de versiones.

La aplicación utilizará una arquitectura por capas para separar las responsabilidades de presentación, aplicación, dominio e infraestructura.

---

## 6. Arquitectura conceptual

La arquitectura estará organizada inicialmente de la siguiente manera:

```text
┌─────────────────────────────┐
│       Presentation          │
│          Blazor             │
├─────────────────────────────┤
│        Application          │
│   Casos de uso / Servicios  │
├─────────────────────────────┤
│           Domain            │
│ Entidades / Reglas de negocio│
├─────────────────────────────┤
│       Infrastructure        │
│ EF Core / SQL Server / Auth │
└─────────────────────────────┘
```

Las dependencias entre capas se definirán durante el diseño detallado de la arquitectura.


## 9. Criterio general de alcance

El sistema debe priorizar la **completitud funcional de los requisitos solicitados** sobre la complejidad.

Cada módulo deberá implementar las funcionalidades necesarias para demostrar su propósito dentro del sistema, evitando características avanzadas que no sean necesarias para cumplir los objetivos académicos del proyecto.

La solución debe ser suficientemente realista para representar el funcionamiento básico de una clínica dental, pero mantenerse dentro de un nivel de complejidad razonable para un equipo de tres desarrolladores.
