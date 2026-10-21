# Modelo de Dominio

## 1. Objetivo

Este documento define el modelo de dominio del Sistema de Gestión para Clínica Dental.

El modelo de dominio identifica las entidades principales del sistema, sus atributos relevantes, relaciones, responsabilidades y conceptos utilizados por las reglas de negocio.

Este documento servirá como referencia para:

* Diseño de la base de datos.
* Definición de entidades de dominio.
* Implementación con Entity Framework Core.
* Definición de relaciones entre entidades.
* Implementación de reglas de negocio.
* Diseño de casos de uso.
* Desarrollo asistido mediante herramientas de inteligencia artificial.

El modelo se mantendrá deliberadamente dentro de un nivel de complejidad apropiado para el proyecto académico.

---

# 2. Principios del modelo

El modelo deberá seguir los siguientes principios:

1. Las entidades deberán representar conceptos reales del dominio de una clínica dental.
2. Las relaciones deberán corresponder a necesidades funcionales reales del sistema.
3. No se crearán entidades únicamente por motivos técnicos si no representan un concepto del dominio.
4. Se evitará modelar funcionalidades que se encuentran fuera del alcance.
5. La información histórica deberá conservarse cuando tenga valor clínico, administrativo o de auditoría.
6. Las reglas importantes del negocio deberán poder expresarse a partir del modelo.
7. Las entidades deberán mantener responsabilidades claras.
8. La estructura física de la base de datos podrá diferir del modelo conceptual cuando existan razones técnicas para ello.

---

# 3. Vista general del dominio

El dominio puede dividirse en cinco áreas principales:

```text id="0plk7n"
                         ┌─────────────────┐
                         │   Seguridad     │
                         │                 │
                         │ Usuario         │
                         │ Rol             │
                         │ Permiso         │
                         └────────┬────────┘
                                  │
                                  │
┌─────────────────┐       ┌───────▼────────┐
│ Administración  │       │     Clínica     │
│                 │       │                 │
│ Paciente        │──────►│ Cita            │
│ Odontólogo      │       │ Consulta        │
└─────────────────┘       │ Historia Clínica│
                          │ Odontograma     │
                          │ Tratamiento     │
                          └────────┬────────┘
                                   │
                                   ▼
                            ┌──────────────┐
                            │   Pagos      │
                            └──────────────┘

                         ┌───────────────┐
                         │   Auditoría   │
                         │   Reportes    │
                         └───────────────┘
```

El **Paciente** será una de las entidades centrales del dominio y estará relacionado directa o indirectamente con la mayoría de las entidades clínicas.

---

# 4. Entidades principales

Las entidades principales del dominio serán:

```text id="6blgqs"
Paciente
Odontólogo
Cita
Historia Clínica
Consulta Clínica
Pieza Dental
Registro Odontograma
Tratamiento
Tratamiento Realizado
Pago

Usuario
Rol
Permiso

Registro de Auditoría
```

Algunas entidades, como `Rol`, `Permiso` y `Registro de Auditoría`, pertenecen a funcionalidades transversales del sistema.

---

# 5. Paciente

## 5.1. Propósito

Representa a una persona que recibe o ha recibido servicios de la clínica.

Es una entidad central del dominio.

## 5.2. Información

```text id="xj87ap"
Paciente
-------------------------
Id
Identificación
Nombres
Apellidos
FechaNacimiento
Sexo
Teléfono
Correo
Dirección

ContactoEmergenciaNombre
ContactoEmergenciaTelefono
ContactoEmergenciaRelacion

Estado
FechaRegistro
```

## 5.3. Relaciones

Un paciente puede tener:

```text id="9xyxzt"
Paciente
   ├── N Citas
   ├── 1 Historia Clínica
   ├── 1 Odontograma
   ├── N Consultas
   ├── N Tratamientos Realizados
   └── N Pagos
```

## 5.4. Reglas principales

* La identificación deberá ser única cuando sea utilizada.
* Un paciente inactivo no deberá utilizarse para nuevas citas.
* La desactivación no eliminará su información histórica.
* La información clínica del paciente no deberá ser modificada por usuarios que no tengan permisos clínicos.

---

# 6. Odontólogo

## 6.1. Propósito

Representa a un profesional que presta servicios odontológicos dentro de la clínica.

## 6.2. Información

```text id="6r84v0"
Odontólogo
-------------------------
Id
Nombres
Apellidos
NumeroLicencia
Especialidad
Teléfono
Correo
Estado
```

## 6.3. Relaciones

```text id="sgkmcn"
Odontólogo
   ├── N Citas
   ├── N Consultas
   └── N Tratamientos Realizados
```

También podrá estar asociado con un `Usuario`.

## 6.4. Reglas principales

* El número de licencia deberá ser único cuando se utilice.
* Un odontólogo inactivo no podrá recibir nuevas citas.
* Un odontólogo podrá tener múltiples citas, pero no dos citas activas en el mismo horario.
* Los registros históricos asociados al odontólogo deberán conservarse aunque sea desactivado.

---

# 7. Usuario

## 7.1. Propósito

Representa una cuenta con capacidad para autenticarse y utilizar el sistema.

El usuario es un concepto de seguridad y no necesariamente representa directamente a un paciente u odontólogo.

## 7.2. Información conceptual

La información de autenticación será gestionada mediante el mecanismo de identidad utilizado por la aplicación.

A nivel conceptual:

```text id="cqy7x0"
Usuario
-------------------------
Id
Nombre
Correo
Estado
FechaCreacion
```

La contraseña no será considerada un atributo de negocio gestionado directamente por las entidades de dominio.

## 7.3. Relaciones

```text id="kmb6i4"
Usuario
   └── N Roles

Usuario
   └── 0..1 Odontólogo
```

Un usuario puede tener uno o varios roles.

Un usuario que representa a un odontólogo podrá asociarse a un único registro de odontólogo.

---

# 8. Rol

## 8.1. Propósito

Representa un conjunto de responsabilidades dentro del sistema.

## 8.2. Información

```text id="y29w0e"
Rol
-------------------------
Id
Nombre
Descripción
Estado
```

Roles iniciales:

```text id="9igc8k"
Administrador
Recepcionista
Odontólogo
```

## 8.3. Relaciones

```text id="ev1k22"
Rol
   ├── N Usuarios
   └── N Permisos
```

La relación entre usuarios y roles y entre roles y permisos será de muchos a muchos.

---

# 9. Permiso

## 9.1. Propósito

Representa una acción específica que un usuario autorizado puede ejecutar.

## 9.2. Información

```text id="a5k94g"
Permiso
-------------------------
Id
Nombre
Descripción
Módulo
```

Ejemplos:

```text id="4q7cbk"
Pacientes.Ver
Pacientes.Crear
Pacientes.Editar

Citas.Ver
Citas.Crear
Citas.Editar
Citas.Cancelar

HistoriaClinica.Ver
HistoriaClinica.Editar

Odontograma.Ver
Odontograma.Editar

Pagos.Ver
Pagos.Crear
```

## 9.3. Relaciones

```text id="5cmq2p"
Rol
  N
  │
  │
  N
Permiso
```

Un permiso puede pertenecer a múltiples roles.

---

# 10. Cita

## 10.1. Propósito

Representa una reserva de tiempo entre un paciente y un odontólogo para recibir atención.

## 10.2. Información

```text id="m4v8kx"
Cita
-------------------------
Id
PacienteId
OdontólogoId

Fecha
HoraInicio
HoraFin

Motivo
Observaciones
Estado

FechaCreacion
FechaModificacion
```

La utilización de `HoraInicio` y `HoraFin` permitirá controlar conflictos de horarios de manera más flexible que almacenar únicamente una hora.

## 10.3. Relaciones

```text id="h0snf2"
Paciente 1 ───── N Cita N ───── 1 Odontólogo
```

Una cita pertenece a un paciente y a un odontólogo.

Una cita podrá generar una consulta clínica cuando sea atendida.

## 10.4. Estados

```text id="7s2fxa"
Programada
Confirmada
Atendida
Cancelada
NoAsistio
```

## 10.5. Reglas principales

* El paciente debe estar activo.
* El odontólogo debe estar activo.
* Una cita cancelada no podrá volver a atenderse directamente.
* Una cita atendida no podrá modificarse como una cita pendiente.
* No se permitirá solapamiento de citas activas para el mismo odontólogo.
* Una cita atendida podrá asociarse a una consulta clínica.

---

# 11. Historia Clínica

## 11.1. Propósito

Representa el conjunto de información clínica general asociada a un paciente.

Cada paciente tendrá una única historia clínica principal.

## 11.2. Información

```text id="n6z1w5"
HistoriaClinica
-------------------------
Id
PacienteId

Alergias
Enfermedades
Medicamentos
AntecedentesMedicos
AntecedentesOdontologicos
Habitos
Observaciones

FechaCreacion
FechaModificacion
```

## 11.3. Relaciones

```text id="2v1k7f"
Paciente 1 ───── 1 Historia Clínica
                         │
                         └──── N Consultas
```

La historia clínica contiene información general y se complementa con las consultas clínicas y el odontograma.

---

# 12. Consulta Clínica

## 12.1. Propósito

Representa una atención clínica concreta realizada a un paciente.

Se diferencia de la `Cita` porque una cita representa la programación y la consulta representa la atención clínica efectivamente realizada.

## 12.2. Información

```text id="u4f98b"
ConsultaClinica
-------------------------
Id
PacienteId
OdontólogoId
CitaId

Fecha
Motivo
Observaciones
Diagnostico
PlanTratamiento

Estado
```

## 12.3. Relaciones

```text id="h6p6wy"
Paciente 1 ───── N Consulta Clínica N ───── 1 Odontólogo

Cita 1 ───── 0..1 Consulta Clínica
```

Una cita atendida podrá generar una consulta clínica.

No todas las consultas clínicas tienen que ser creadas obligatoriamente desde una cita si posteriormente el alcance permite otros flujos, pero el flujo principal será:

```text id="l8kv8u"
Cita
 ↓
Atendida
 ↓
Consulta Clínica
```

## 12.4. Reglas principales

* Una consulta deberá estar asociada a un paciente.
* Una consulta deberá registrar el odontólogo responsable.
* Una consulta asociada a una cita deberá corresponder al paciente y odontólogo de dicha cita.
* La creación o modificación de una consulta deberá estar restringida a usuarios con permisos clínicos.

---

# 13. Pieza Dental

## 13.1. Propósito

Representa una pieza dental identificada mediante la nomenclatura utilizada por el odontograma.

## 13.2. Información

```text id="vlq9it"
PiezaDental
-------------------------
Id
NumeroFDI
Nombre
```

Ejemplos:

```text id="9lnqve"
11
12
13
...
48
```

Las piezas dentales constituyen un catálogo relativamente estático del sistema.

## 13.3. Consideración

La pieza dental no representa por sí misma el estado clínico de un paciente.

El estado de una pieza para un paciente será representado mediante un `RegistroOdontograma`.

---

# 14. Registro del Odontograma

## 14.1. Propósito

Representa el estado de una pieza dental específica para un paciente.

Esta separación es importante porque la pieza dental `36`, por ejemplo, es un concepto permanente, mientras que su estado clínico puede variar entre pacientes y a través del tiempo.

## 14.2. Información

```text id="qv5z2n"
RegistroOdontograma
-------------------------
Id
PacienteId
PiezaDentalId

Estado
Observaciones

FechaModificacion
OdontólogoId
```

## 14.3. Relaciones

```text id="m0u0lz"
Paciente 1 ───── N RegistroOdontograma N ───── 1 PiezaDental

Odontólogo 1 ───── N RegistroOdontograma
```

Conceptualmente:

```text id="1l8m0r"
Paciente
   │
   └── RegistroOdontograma
          │
          ├── PiezaDental 36
          ├── Estado: Caries
          └── Odontólogo: Dr. Pérez
```

## 14.4. Estados

Inicialmente:

```text id="8aj4fu"
Sano
Caries
Restauracion
Ausente
Fracturado
TratamientoRealizado
```

## 14.5. Reglas principales

* Una pieza deberá pertenecer al catálogo de piezas dentales.
* Un registro deberá pertenecer a un paciente.
* Solo usuarios con permisos clínicos podrán modificarlo.
* Las modificaciones deberán quedar auditadas.
* El sistema deberá mantener el estado actual de cada pieza.

El historial detallado de cambios del odontograma podrá quedar registrado mediante auditoría, sin necesidad de crear inicialmente una entidad clínica adicional para cada cambio.

---

# 15. Tratamiento

## 15.1. Propósito

Representa un tratamiento disponible en el catálogo de la clínica.

## 15.2. Información

```text id="p7c98x"
Tratamiento
-------------------------
Id
Nombre
Descripcion
PrecioBase
Estado
```

Ejemplos:

```text id="0h6vwl"
Limpieza Dental
Restauración
Extracción
Endodoncia
Blanqueamiento
```

## 15.3. Relaciones

Un tratamiento puede utilizarse en múltiples tratamientos realizados.

```text id="l5gq5n"
Tratamiento
    │
    └── N TratamientosRealizados
```

## 15.4. Reglas principales

* Un tratamiento inactivo no podrá seleccionarse para nuevos tratamientos realizados.
* El precio base podrá modificarse en el catálogo.
* Los registros históricos deberán conservar el precio utilizado cuando fueron realizados.

---

# 16. Tratamiento Realizado

## 16.1. Propósito

Representa la aplicación concreta de un tratamiento a un paciente.

Esta entidad es diferente del catálogo `Tratamiento`.

## 16.2. Información

```text id="0l9l5b"
TratamientoRealizado
-------------------------
Id

PacienteId
TratamientoId
OdontólogoId
PiezaDentalId (opcional)
ConsultaId (opcional)

Fecha
Precio
Estado
Observaciones
```

## 16.3. Relaciones

```text id="j5k6t2"
Paciente 1 ───── N TratamientoRealizado N ───── 1 Tratamiento

Odontólogo 1 ───── N TratamientoRealizado

PiezaDental 1 ───── 0..N TratamientoRealizado

Consulta 1 ───── 0..N TratamientoRealizado
```

La pieza dental será opcional porque existen tratamientos que pueden aplicarse a la boca completa o que no requieren identificar una pieza específica.

## 16.4. Estados

```text id="1h1q8e"
Pendiente
EnProceso
Completado
Cancelado
```

## 16.5. Precio

El `Precio` del tratamiento realizado deberá almacenarse independientemente del `PrecioBase` del catálogo.

Ejemplo:

```text id="7s0p1a"
Catálogo:
Restauración → RD$ 3,000

Tratamiento realizado:
Restauración → RD$ 3,500
```

Esto permite conservar el valor real utilizado en la transacción histórica aunque posteriormente cambie el precio del catálogo.

---

# 17. Pago

## 17.1. Propósito

Representa un pago realizado por un paciente asociado a un tratamiento.

## 17.2. Información

```text id="5g4q8f"
Pago
-------------------------
Id
TratamientoRealizadoId

Fecha
Monto
MetodoPago
Observaciones
```

## 17.3. Relaciones

```text id="x6b6hl"
TratamientoRealizado 1 ───── N Pago
```

Un tratamiento puede tener múltiples pagos.

El paciente se obtiene mediante el tratamiento realizado al que pertenece el pago.

## 17.4. Métodos de pago

```text id="l5z1jc"
Efectivo
Tarjeta
Transferencia
```

## 17.5. Estado financiero

El sistema no necesita almacenar obligatoriamente un estado de pago independiente.

El estado financiero podrá determinarse mediante:

```text id="a4u7lw"
TotalPagado = SUM(Pagos.Monto)

SaldoPendiente =
TratamientoRealizado.Precio - TotalPagado
```

A partir de estos valores podrá determinarse si el tratamiento está:

```text id="j9f2vo"
Pendiente
Pago parcial
Pagado
```

## 17.6. Reglas principales

* El monto debe ser mayor que cero.
* El total acumulado de pagos no podrá superar el precio del tratamiento.
* Los pagos históricos deberán conservarse.
* Un pago registrado no deberá eliminarse físicamente.

---

# 18. Registro de Auditoría

## 18.1. Propósito

Representa un evento relevante realizado dentro del sistema.

La auditoría es transversal al dominio y permite conocer quién realizó una operación y cuándo.

## 18.2. Información

```text id="w1gq8q"
RegistroAuditoria
-------------------------
Id
UsuarioId

FechaHora
Accion
Modulo
Entidad
EntidadId
Descripcion
```

## 18.3. Acciones

Ejemplos:

```text id="6ep0b3"
LOGIN
CREAR
EDITAR
DESACTIVAR
CANCELAR
REGISTRAR_PAGO
CAMBIAR_PERMISOS
```

## 18.4. Relaciones

```text id="p6bj3k"
Usuario 1 ───── N RegistroAuditoria
```

Los registros de auditoría deberán ser de solo lectura desde la aplicación.

---

# 19. Reportes

Los reportes no se considerarán entidades persistentes del dominio.

Representan consultas construidas a partir de las entidades existentes.

Por ejemplo:

```text id="f6rj1z"
Reporte de Citas
    ↓
Cita
    ├── Paciente
    └── Odontólogo
```

```text id="d7z3cj"
Reporte de Ingresos
    ↓
Pago
    └── TratamientoRealizado
          ├── Paciente
          └── Tratamiento
```

```text id="m5w8y4"
Reporte de Tratamientos
    ↓
TratamientoRealizado
    ├── Tratamiento
    ├── Paciente
    └── Odontólogo
```

Los reportes no requieren una tabla propia salvo que posteriormente se determine una necesidad específica.

---

# 20. Relaciones principales del dominio

La estructura principal puede resumirse de la siguiente manera:

```text id="6w8e5r"
                         ┌──────────────┐
                         │    Usuario   │
                         └──────┬───────┘
                                │
                         ┌──────▼───────┐
                         │     Rol      │
                         └──────┬───────┘
                                │
                         ┌──────▼───────┐
                         │   Permiso    │
                         └──────────────┘


┌─────────────┐
│   Paciente  │
└──────┬──────┘
       │
       ├────────────── 1 Historia Clínica
       │                       │
       │                       └── N Consultas
       │
       ├────────────── N Citas ───────────────┐
       │                                      │
       │                               ┌──────▼──────┐
       │                               │  Odontólogo │
       │                               └─────────────┘
       │
       ├────────────── N Registros Odontograma
       │                         │
       │                         └── Pieza Dental
       │
       ├────────────── N Tratamientos Realizados
       │                         │
       │                         ├── Tratamiento
       │                         ├── Odontólogo
       │                         ├── Pieza Dental
       │                         └── N Pagos
       │
       └────────────── N Consultas


Usuario ─────────── N RegistroAuditoria
```

---

# 21. Cardinalidades principales

| Relación                             | Cardinalidad |
| ------------------------------------ | ------------ |
| Paciente → Cita                      | 1:N          |
| Odontólogo → Cita                    | 1:N          |
| Paciente → Historia Clínica          | 1:1          |
| Historia Clínica → Consulta Clínica  | 1:N          |
| Paciente → Consulta Clínica          | 1:N          |
| Odontólogo → Consulta Clínica        | 1:N          |
| Cita → Consulta Clínica              | 1:0..1       |
| Paciente → Registro Odontograma      | 1:N          |
| Pieza Dental → Registro Odontograma  | 1:N          |
| Odontólogo → Registro Odontograma    | 1:N          |
| Tratamiento → Tratamiento Realizado  | 1:N          |
| Paciente → Tratamiento Realizado     | 1:N          |
| Odontólogo → Tratamiento Realizado   | 1:N          |
| Pieza Dental → Tratamiento Realizado | 1:N          |
| Tratamiento Realizado → Pago         | 1:N          |
| Usuario → Rol                        | N:M          |
| Rol → Permiso                        | N:M          |
| Usuario → Registro Auditoría         | 1:N          |
| Usuario → Odontólogo                 | 0..1:1       |

---

# 22. Relaciones de seguridad

Las relaciones de seguridad se pueden representar conceptualmente como:

```text id="j7qf6b"
Usuario
   │
   │ N:M
   ▼
Rol
   │
   │ N:M
   ▼
Permiso
```

Esto permite que:

* Un usuario tenga uno o varios roles.
* Un rol tenga múltiples permisos.
* Un permiso pueda pertenecer a múltiples roles.
* La autorización se determine mediante los permisos efectivos del usuario.

Ejemplo:

```text id="6c9t8k"
Usuario: carlos.perez

Roles:
    Odontólogo

Permisos:
    Citas.Ver
    Citas.Atender
    Pacientes.Ver
    HistoriaClinica.Ver
    HistoriaClinica.Editar
    Odontograma.Ver
    Odontograma.Editar
    Tratamientos.Ver
    Tratamientos.Crear
```

---

# 23. Estados y enumeraciones del dominio

Para evitar valores arbitrarios dentro del sistema, varios conceptos utilizarán valores controlados.

## Estado de paciente

```text id="6z5kqh"
Activo
Inactivo
```

## Estado de odontólogo

```text id="5zzj9n"
Activo
Inactivo
```

## Estado de usuario

```text id="2v1t4p"
Activo
Inactivo
```

## Estado de cita

```text id="z8g5xj"
Programada
Confirmada
Atendida
Cancelada
NoAsistio
```

## Estado de tratamiento realizado

```text id="j2r6py"
Pendiente
EnProceso
Completado
Cancelado
```

## Estado de pieza dental

```text id="9v6g2x"
Sano
Caries
Restauracion
Ausente
Fracturado
TratamientoRealizado
```

## Método de pago

```text id="v2w5x8"
Efectivo
Tarjeta
Transferencia
```

---

# 24. Agregados conceptuales

Aunque la implementación no tiene que utilizar obligatoriamente un patrón de Domain-Driven Design completo, el dominio puede organizarse conceptualmente alrededor de algunos agregados.

## Agregado Paciente

```text id="5n7f6k"
Paciente
 ├── Historia Clínica
 ├── Citas
 ├── Consultas
 ├── Registros de Odontograma
 └── Tratamientos Realizados
```

El paciente constituye el principal punto de referencia de la información clínica.

## Agregado Tratamiento Realizado

```text id="8r0x4b"
TratamientoRealizado
 └── Pagos
```

El tratamiento realizado controla su precio, estado y los pagos asociados.

## Agregado Seguridad

```text id="4c5y9x"
Usuario
 └── Roles
       └── Permisos
```

## Agregado Auditoría

```text id="7m2f8q"
RegistroAuditoria
```

Los registros de auditoría son históricos y no deben modificarse después de creados.

---

# 25. Reglas de integridad del dominio

Las principales reglas que deberán respetarse son:

### Pacientes

* La identificación de un paciente no deberá duplicarse cuando sea obligatoria.
* Un paciente inactivo no podrá recibir nuevas citas.
* Sus datos históricos deberán conservarse.

### Odontólogos

* Un odontólogo inactivo no podrá recibir nuevas citas.
* No podrá existir más de una cita activa para el mismo odontólogo en un horario incompatible.

### Citas

* Una cita debe tener paciente y odontólogo.
* Una cita cancelada no podrá ser atendida.
* Una cita atendida deberá poder relacionarse con una consulta clínica.

### Historia clínica

* Cada paciente tendrá una historia clínica principal.
* La información clínica solo podrá ser modificada por usuarios autorizados.

### Odontograma

* Cada registro corresponde a una pieza dental de un paciente.
* Solo usuarios autorizados podrán modificar el estado de las piezas.
* Las modificaciones deberán ser auditables.

### Tratamientos

* Un tratamiento del catálogo inactivo no podrá utilizarse en nuevos tratamientos.
* El tratamiento realizado deberá conservar el precio aplicado independientemente de cambios posteriores en el catálogo.

### Pagos

* El monto debe ser positivo.
* Los pagos acumulados no pueden superar el precio del tratamiento.
* Los pagos no deben eliminarse físicamente.

### Seguridad

* Un usuario inactivo no podrá autenticarse.
* Un usuario solo podrá realizar operaciones para las cuales tenga permisos.

### Auditoría

* Los registros de auditoría son históricos.
* No podrán modificarse desde la aplicación.
* Las operaciones sensibles deberán generar eventos de auditoría.

---

# 26. Decisiones de modelado

Se establecen las siguientes decisiones para mantener el dominio simple y consistente:

### Historia Clínica y Consulta Clínica son conceptos diferentes

La `HistoriaClinica` contiene información general y antecedentes del paciente.

La `ConsultaClinica` representa una atención específica.

```text
Historia Clínica
       │
       ├── Antecedentes
       │
       └── Consultas
              ├── Consulta 1
              ├── Consulta 2
              └── Consulta 3
```

### Tratamiento y Tratamiento Realizado son conceptos diferentes

`Tratamiento` representa el catálogo.

`TratamientoRealizado` representa una aplicación concreta del tratamiento a un paciente.

Esto permite conservar correctamente el historial aunque cambie el catálogo.

### Pieza Dental y Registro Odontograma son conceptos diferentes

`PiezaDental` representa el catálogo de piezas.

`RegistroOdontograma` representa el estado de una pieza para un paciente.

Esto evita duplicar información sobre las piezas dentales.

### Reportes no son entidades

Los reportes se obtendrán mediante consultas sobre las entidades existentes.

### Auditoría no modifica las entidades auditadas

La auditoría registra eventos sobre las entidades, pero no forma parte de su ciclo de vida funcional.

---

# 27. Modelo conceptual simplificado

El dominio completo puede resumirse en el siguiente modelo:

```text id="2g7r6c"
                         ┌────────────┐
                         │   Usuario  │
                         └─────┬──────┘
                               │
                          N:M  │
                               ▼
                         ┌────────────┐
                         │    Rol     │
                         └─────┬──────┘
                               │
                          N:M  │
                               ▼
                         ┌────────────┐
                         │  Permiso   │
                         └────────────┘


┌──────────────┐
│   Paciente   │
└──────┬───────┘
       │
       ├── 1:1 ── Historia Clínica ── 1:N ── Consulta Clínica ── N:1 ── Odontólogo
       │
       ├── 1:N ── Cita ─────────────── N:1 ── Odontólogo
       │
       ├── 1:N ── Registro Odontograma ── N:1 ── Pieza Dental
       │
       └── 1:N ── Tratamiento Realizado
                          │
                          ├── N:1 ── Tratamiento
                          ├── N:1 ── Odontólogo
                          ├── N:1 ── Pieza Dental (opcional)
                          │
                          └── 1:N ── Pago


Usuario ── 1:N ── Registro Auditoría
```

Este modelo representa el núcleo funcional del sistema y será la base para el diseño posterior de la persistencia y de los casos de uso.
