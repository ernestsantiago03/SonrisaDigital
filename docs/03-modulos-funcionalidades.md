# Módulos y Funcionalidades

## 1. Objetivo

Este documento define los módulos funcionales que componen el Sistema de Gestión para Clínica Dental y las funcionalidades que estarán disponibles dentro de cada uno.

La finalidad es establecer el comportamiento funcional esperado del sistema antes de iniciar la implementación.

El documento servirá como referencia para:

* Diseño de la base de datos.
* Diseño de las interfaces.
* Definición de casos de uso.
* Implementación de la lógica de negocio.
* Distribución del trabajo entre los integrantes del equipo.
* Generación de código mediante herramientas de inteligencia artificial.
* Elaboración de pruebas.

---

# 2. Estructura general de módulos

El sistema estará compuesto inicialmente por los siguientes módulos:

```text
Sistema de Gestión Dental
│
├── Dashboard
│
├── Pacientes
├── Odontólogos
├── Citas
├── Historia Clínica
├── Odontograma
├── Tratamientos
├── Pagos
│
├── Usuarios
├── Roles y Permisos
├── Auditoría
│
└── Reportes
```

El Dashboard funcionará como punto de entrada para los usuarios autenticados y mostrará información acorde con sus permisos.

---

# 3. Dashboard

## 3.1. Descripción

El Dashboard será la pantalla principal después del inicio de sesión.

La información mostrada dependerá del rol y los permisos del usuario.

No se pretende desarrollar un sistema avanzado de analítica. El Dashboard tendrá como objetivo proporcionar una vista rápida del estado actual de la clínica.

## 3.2. Información general

Para usuarios administrativos podrá mostrar:

* Cantidad de pacientes activos.
* Cantidad de odontólogos activos.
* Citas programadas para el día.
* Citas pendientes.
* Tratamientos en proceso.
* Pagos pendientes.

También podrá incluir accesos rápidos a operaciones frecuentes.

Ejemplo:

```text
┌─────────────────────────────────────────────────────────┐
│ Dashboard                                               │
├───────────────┬───────────────┬─────────────────────────┤
│ Pacientes     │ Citas hoy     │ Pagos pendientes        │
│     524       │      18       │      RD$ 42,500         │
├───────────────┴───────────────┴─────────────────────────┤
│                                                         │
│ Próximas citas                                         │
│                                                         │
│ 09:00  Juan Pérez       Dr. Gómez       Confirmada     │
│ 10:00  Ana López        Dra. Díaz        Programada     │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

El Dashboard del odontólogo mostrará principalmente:

* Sus citas del día.
* Pacientes atendidos recientemente.
* Tratamientos en proceso.
* Accesos rápidos a sus pacientes.

---

# 4. Módulo de Pacientes

## 4.1. Descripción

Permitirá administrar la información general de los pacientes de la clínica.

## 4.2. Listado de pacientes

La pantalla principal mostrará una tabla con información resumida.

Columnas iniciales:

* Nombre completo.
* Identificación.
* Teléfono.
* Correo.
* Fecha de registro.
* Estado.

Funcionalidades:

* Buscar pacientes.
* Filtrar por estado.
* Consultar información.
* Registrar paciente.
* Editar paciente.
* Desactivar paciente.

La búsqueda podrá realizarse por:

* Nombre.
* Apellido.
* Identificación.
* Teléfono.

## 4.3. Registro de paciente

El formulario permitirá registrar:

### Información personal

* Nombres.
* Apellidos.
* Identificación.
* Fecha de nacimiento.
* Sexo.

### Información de contacto

* Teléfono.
* Correo electrónico.
* Dirección.

### Contacto de emergencia

* Nombre.
* Teléfono.
* Relación con el paciente.

El sistema deberá validar los campos obligatorios y formatos correspondientes.

## 4.4. Consulta del paciente

La información del paciente podrá organizarse mediante pestañas:

```text
Información | Historia Clínica | Odontograma | Tratamientos | Pagos | Citas
```

El contenido disponible dependerá de los permisos del usuario.

La pantalla de información general mostrará los datos administrativos del paciente.

## 4.5. Edición

Los usuarios con permiso podrán modificar la información administrativa del paciente.

La modificación de información clínica se realizará desde los módulos correspondientes.

## 4.6. Desactivación

Un paciente podrá pasar a estado inactivo.

La desactivación no eliminará físicamente sus registros.

Los pacientes inactivos podrán permanecer disponibles para consulta histórica, pero no deberían utilizarse para crear nuevas citas sin ser reactivados.

---

# 5. Módulo de Odontólogos

## 5.1. Descripción

Permitirá administrar la información de los odontólogos que forman parte de la clínica.

## 5.2. Listado

La pantalla mostrará:

* Nombre.
* Especialidad.
* Número de licencia/colegiatura.
* Teléfono.
* Correo.
* Estado.

Funcionalidades:

* Buscar.
* Filtrar.
* Consultar.
* Registrar.
* Editar.
* Desactivar.

## 5.3. Registro

El formulario permitirá introducir:

* Nombres.
* Apellidos.
* Número de licencia/colegiatura.
* Especialidad.
* Teléfono.
* Correo.
* Estado.

Opcionalmente podrá asociarse un usuario existente al odontólogo.

## 5.4. Consulta

La vista detallada podrá mostrar:

* Información del odontólogo.
* Próximas citas.
* Historial de citas.
* Tratamientos realizados.

---

# 6. Módulo de Citas

## 6.1. Descripción

Permitirá gestionar las citas de los pacientes con los odontólogos.

## 6.2. Listado

La pantalla podrá mostrar las citas en formato de tabla.

Columnas:

* Fecha.
* Hora.
* Paciente.
* Odontólogo.
* Motivo.
* Estado.

Se podrán aplicar filtros por:

* Fecha.
* Rango de fechas.
* Paciente.
* Odontólogo.
* Estado.

## 6.3. Registro de cita

El formulario permitirá seleccionar:

* Paciente.
* Odontólogo.
* Fecha.
* Hora.
* Motivo.
* Observaciones.

El sistema validará que:

* El paciente esté activo.
* El odontólogo esté activo.
* La fecha sea válida.
* El odontólogo no tenga otra cita activa en el mismo horario.

## 6.4. Estados

Las citas podrán encontrarse en:

```text
Programada
Confirmada
Atendida
Cancelada
No asistió
```

## 6.5. Modificación

Una cita programada podrá modificarse mientras no haya sido atendida o cancelada.

Podrán modificarse:

* Fecha.
* Hora.
* Odontólogo.
* Motivo.
* Observaciones.

## 6.6. Cancelación

Las citas podrán cancelarse.

La cancelación no eliminará físicamente la cita, sino que cambiará su estado.

## 6.7. Atención

Cuando el paciente sea atendido, la cita podrá marcarse como `Atendida`.

A partir de esta acción, el odontólogo podrá acceder al flujo clínico correspondiente.

Al marcar la cita como `Atendida`, el sistema registra automáticamente su consulta clínica en la
historia del paciente (una sola por cita) y la deja `Abierta` para que el odontólogo la complete. El
detalle de la cita muestra entonces un enlace **"Ver historia clínica del paciente"**. Este puente se
describe en el apartado 7.4.2.

El estado `Atendida` es final: una vez marcada, la cita ya no se edita (solo se consulta o se
reprograma).

---

# 7. Módulo de Historia Clínica

## 7.1. Descripción

Permitirá almacenar la información clínica relevante de cada paciente.

## 7.2. Antecedentes

La historia clínica tendrá inicialmente los siguientes campos:

* Alergias.
* Enfermedades relevantes.
* Medicamentos actuales.
* Antecedentes médicos.
* Antecedentes odontológicos.
* Hábitos.
* Observaciones generales.

## 7.3. Registro y modificación

El odontólogo podrá registrar y actualizar la información clínica.

Las modificaciones importantes deberán quedar registradas mediante auditoría.

### 7.3.1. Modo detalle y modo edición (implementado)

La pestaña **Historia general** trabaja en dos modos:

* **Detalle (por defecto):** muestra lo que está guardado en forma de lectura: alergias,
  enfermedades y hábitos como fichas; los medicamentos como filas (medicamento y dosis); y los
  textos largos tal como se escribieron. Cuando un campo no tiene dato se indica **"Sin registrar"**,
  que es distinto de un texto explícito como **"No refiere alergias"**.

* **Edición:** al pulsar el lápiz ✏️ del encabezado de una sección (*Antecedentes médicos* o
  *Antecedentes odontológicos*) esa sección cambia a campos de texto con **lo que ya estaba
  guardado**, para corregirlo o agregar más información. Aparecen entonces los botones
  **"Guardar cambios"** y **"Cancelar"**.

Reglas del modo edición:

* Solo se edita una sección a la vez (mientras una está abierta, el lápiz de la otra queda
  deshabilitado).
* **"Cancelar"** vuelve a leer la historia de la base de datos, de manera que los cambios escritos
  se descartan sin guardarse.
* Al guardar (o al cambiar de paciente) la pantalla vuelve sola al modo detalle.
* Una sección pasa a edición únicamente con el permiso `HistoriaClinica.Editar`. Los roles que solo
  tienen `HistoriaClinica.Ver` ven el detalle en modo lectura y sin lápices.

## 7.4. Consultas clínicas

La historia clínica tendrá un historial de consultas.

Cada consulta podrá registrar:

* Fecha.
* Odontólogo.
* Motivo.
* Observaciones.
* Diagnóstico.
* Plan de tratamiento.

Las consultas anteriores serán de solo lectura una vez finalizadas, salvo que posteriormente se establezca una funcionalidad explícita para corrección.

### 7.4.1. Registro de la consulta (implementado)

En la pantalla de Historia Clínica, la pestaña **Consultas** incluye el botón **"+ Nueva consulta"**.
Se muestra a los usuarios cuyo rol tenga el permiso `HistoriaClinica.Crear`: el acceso lo decide el
permiso del rol, igual que en el resto de los módulos.

La ventana solicita: odontólogo tratante, fecha, motivo, diagnóstico, plan de tratamiento y
observaciones. El odontólogo, el motivo y el diagnóstico son obligatorios, y la fecha no puede ser
futura. La lista de odontólogos trae solo los activos y deja preseleccionado al odontólogo vinculado
al usuario de la sesión, cuando ese usuario es odontólogo (el vínculo se crea al asignarle el rol
Odontólogo a un usuario, desde el módulo de Usuarios).

La primera consulta de un paciente crea su historia clínica (la tabla de consultas necesita una
historia a la que pertenecer); esto no contradice la regla de "no crear historias vacías", porque
la consulta ya es un dato clínico.

Cada consulta nace en estado `Abierta` y puede cerrarse con el botón **"Cerrar consulta"**. Una vez
cerrada queda de solo lectura. Tanto el registro como el cierre quedan anotados en la auditoría.

### 7.4.2. Puente cita → consulta (implementado)

Cuando una cita se marca como `Atendida` (desde el detalle de la cita con el botón **"Marcar como
atendida"**, o al dejar la cita en ese estado desde su formulario de edición), el sistema registra
automáticamente la **consulta clínica de esa cita**. Esto une el flujo `Cita → Atención → Historia
Clínica` descrito en el apartado 18.

La consulta así creada:

* Hereda de la cita el paciente, el odontólogo tratante, la fecha y el motivo.
* Nace en estado `Abierta`.
* Queda ligada a esa cita (una cita genera una sola consulta; si el usuario vuelve a marcar la
  misma cita como atendida, no se duplica nada).
* Si el paciente todavía no tenía historia clínica, se le crea en ese momento.

Los campos clínicos (diagnóstico y plan de tratamiento) los completa después el odontólogo desde la
pestaña **Consultas** de la Historia Clínica con el botón **"Completar consulta"**.

### 7.4.3. Completar una consulta abierta (implementado)

Mientras una consulta siga `Abierta`, el odontólogo puede modificarla con el botón **"Completar
consulta"** (por ejemplo para terminarle el diagnóstico a la consulta que nació de una cita). Se
abre la misma ventana del registro, con los datos que ya tenía la consulta cargados, y se puede
corregir también el odontólogo tratante.

El botón se muestra con el permiso `HistoriaClinica.Editar` (modificar información clínica), mientras
que **"+ Nueva consulta"** y **"Cerrar consulta"** se muestran con `HistoriaClinica.Crear` (registrar
la consulta), tal como se definió en 7.4.1.

Una consulta `Cerrada` es de solo lectura: el servicio rechaza cualquier intento de modificarla. Cada
consulta completada queda anotada en la auditoría.

---

# 8. Módulo de Odontograma

## 8.1. Descripción

Permitirá visualizar y registrar el estado de las piezas dentales del paciente.

Se utilizará la numeración FDI.

## 8.2. Visualización

El odontograma mostrará las piezas dentales organizadas de acuerdo con su numeración.

Cada pieza tendrá un estado visual que permita identificar su condición.

Estados iniciales:

```text
Sano
Caries
Restauración
Ausente
Fracturado
Tratamiento realizado
```

## 8.3. Selección de pieza

Al seleccionar una pieza dental se mostrará información como:

* Número de pieza.
* Estado actual.
* Observaciones.
* Fecha de última modificación.
* Odontólogo que realizó la modificación.

## 8.4. Actualización

El odontólogo podrá:

* Seleccionar una pieza.
* Cambiar su estado.
* Agregar observaciones.
* Guardar los cambios.

Las modificaciones deberán quedar asociadas al paciente y registradas mediante auditoría.

## 8.5. Alcance

El odontograma será una representación simplificada.

No se contempla inicialmente:

* Representación detallada de superficies dentales.
* Diagnóstico automatizado.
* Radiografías.
* Imágenes médicas.
* Gráficos clínicos avanzados.

---

# 9. Módulo de Tratamientos

## 9.1. Descripción

El módulo tendrá dos funciones principales:

1. Administrar el catálogo de tratamientos.
2. Registrar tratamientos realizados a pacientes.

---

## 9.2. Catálogo de tratamientos

Permitirá administrar los tratamientos disponibles.

Cada tratamiento tendrá:

* Nombre.
* Descripción.
* Precio base.
* Estado.

Ejemplo:

```text
Limpieza dental       RD$ 1,500
Restauración          RD$ 3,000
Extracción            RD$ 2,500
Endodoncia            RD$ 12,000
```

## 9.3. Administración del catálogo

Los usuarios autorizados podrán:

* Crear tratamientos.
* Editar tratamientos.
* Desactivar tratamientos.
* Consultar tratamientos.

Un tratamiento desactivado no podrá utilizarse para nuevos registros, pero permanecerá asociado a los tratamientos históricos.

---

## 9.4. Tratamiento realizado

Un odontólogo podrá registrar un tratamiento realizado a un paciente.

Información:

* Paciente.
* Tratamiento.
* Odontólogo.
* Fecha.
* Pieza dental, cuando corresponda.
* Precio.
* Estado.
* Observaciones.

## 9.5. Estados

Inicialmente:

```text
Pendiente
En proceso
Completado
Cancelado
```

## 9.6. Tratamientos asociados a piezas

Cuando un tratamiento se relacione con una pieza dental específica, se podrá seleccionar la pieza correspondiente del odontograma.

Ejemplo:

```text
Tratamiento:
Restauración

Paciente:
Juan Pérez

Pieza:
36

Estado:
Completado
```

---

# 10. Módulo de Pagos

## 10.1. Descripción

Permitirá registrar y consultar los pagos realizados por los pacientes.

## 10.2. Registro de pago

Un pago deberá estar asociado a un tratamiento realizado.

Información:

* Paciente.
* Tratamiento.
* Fecha.
* Monto.
* Método de pago.
* Observaciones.

## 10.3. Métodos de pago

Inicialmente:

```text
Efectivo
Tarjeta
Transferencia
```

## 10.4. Pagos parciales

Un tratamiento podrá tener múltiples pagos.

Ejemplo:

```text
Tratamiento: Endodoncia

Precio total:       RD$ 12,000

Pago 1:             RD$ 5,000
Pago 2:             RD$ 3,000

Total pagado:       RD$ 8,000
Pendiente:          RD$ 4,000
```

El sistema deberá calcular automáticamente:

```text
Monto pendiente =
Precio del tratamiento - Total de pagos
```

## 10.5. Validaciones

El sistema no deberá permitir registrar un pago cuyo monto sea superior al saldo pendiente.

Una vez cubierto el precio total, el saldo deberá ser cero.

## 10.6. Pagos del paciente (implementado)

En la ficha del paciente (`/pacientes/{id}`) la pestaña **Pagos** muestra:

* Tres tarjetas de totales: **Total en tratamientos**, **Total pagado** (con la cantidad de pagos) y
  **Saldo pendiente**.
* La tabla **Pagos realizados** con **todos** los pagos hechos por el paciente, del más reciente al
  más antiguo: fecha, tratamiento al que pertenece el pago, monto, método, quién lo registró y un
  botón **Ver**.

El botón **Ver** lleva al **detalle del pago** (página `/pagos/{id del tratamiento}`), donde se ve
la ficha del tratamiento con sus cuotas. No lleva al listado del módulo de Pagos.

Todo el contenido de esta pestaña (las tarjetas y la tabla) está protegido con el permiso
`Pagos.Ver`: un rol que no lo tenga (por ejemplo el odontólogo) ve un aviso en lugar de los montos,
y así el botón **Ver** nunca lleva a una pantalla de "Acceso denegado".

---

# 11. Módulo de Usuarios

## 11.1. Descripción

Permitirá administrar las cuentas que tienen acceso al sistema.

## 11.2. Listado

Mostrará:

* Nombre de usuario.
* Correo.
* Nombre.
* Estado.
* Roles.
* Fecha de creación.

## 11.3. Creación

El Administrador podrá crear usuarios indicando:

* Nombre.
* Correo.
* Contraseña inicial.
* Estado.
* Rol o roles.

Si el usuario será odontólogo, podrá asociarse posteriormente con su registro de odontólogo.

## 11.4. Edición

El Administrador podrá:

* Cambiar información básica.
* Cambiar roles.
* Activar o desactivar usuarios.
* Restablecer credenciales mediante el mecanismo de autenticación implementado.

## 11.5. Desactivación

Un usuario desactivado no podrá iniciar sesión.

Su información histórica deberá conservarse para fines de auditoría.

---

# 12. Módulo de Roles y Permisos

## 12.1. Descripción

Permitirá administrar el acceso funcional del sistema.

## 12.2. Roles

El sistema tendrá inicialmente:

```text
Administrador
Recepcionista
Odontólogo
```

## 12.3. Permisos

Los permisos representarán acciones específicas.

Ejemplos:

```text
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

## 12.4. Administración

El Administrador podrá:

* Consultar roles.
* Crear roles.
* Editar roles.
* Consultar permisos.
* Asignar permisos a roles.

La eliminación física de roles no será necesaria inicialmente.

---

# 13. Módulo de Auditoría

## 13.1. Descripción

Permitirá consultar las operaciones relevantes realizadas dentro del sistema.

## 13.2. Información registrada

Cada evento podrá contener:

* Fecha y hora.
* Usuario.
* Acción.
* Módulo.
* Entidad.
* Identificador del registro.
* Descripción.

## 13.3. Acciones auditables

Como mínimo:

* Inicio de sesión.
* Creación de registros.
* Modificación de registros importantes.
* Desactivación de registros.
* Modificaciones de información clínica.
* Cambios en el odontograma.
* Registro de pagos.
* Cambios de roles y permisos.

## 13.4. Consulta

El Administrador podrá buscar y filtrar registros por:

* Usuario.
* Fecha.
* Acción.
* Módulo.
* Entidad.

Los registros de auditoría no podrán ser modificados desde la interfaz.

---

# 14. Módulo de Reportes

## 14.1. Descripción

Permitirá generar consultas resumidas de la información almacenada.

Los reportes serán principalmente informativos y estarán orientados a las necesidades administrativas y clínicas básicas.

## 14.2. Reporte de citas

Filtros:

* Fecha inicial.
* Fecha final.
* Odontólogo.
* Estado.

Información:

* Fecha.
* Hora.
* Paciente.
* Odontólogo.
* Estado.

## 14.3. Reporte de pacientes

Podrá mostrar:

* Total de pacientes.
* Pacientes activos.
* Pacientes inactivos.
* Pacientes registrados durante un período.

## 14.4. Reporte de tratamientos

Podrá mostrar:

* Tratamiento.
* Cantidad realizada.
* Odontólogo.
* Estado.
* Período.

## 14.5. Reporte de pagos

Podrá mostrar:

* Total cobrado.
* Cantidad de pagos.
* Pagos por método.
* Pagos pendientes.
* Ingresos por período.

## 14.6. Reportes por usuario

El contenido disponible dependerá del rol y permisos.

El Administrador tendrá acceso a los reportes generales.

El Recepcionista tendrá acceso a reportes administrativos.

El Odontólogo tendrá acceso a reportes relacionados con su actividad clínica.

---

# 15. Búsqueda y filtros

Los módulos que manejen cantidades considerables de registros deberán permitir búsquedas o filtros apropiados.

No será obligatorio implementar búsqueda avanzada en todas las pantallas.

Como mínimo:

| Módulo       | Búsqueda/Filtros                         |
| ------------ | ---------------------------------------- |
| Pacientes    | Nombre, identificación, teléfono, estado |
| Odontólogos  | Nombre, especialidad, estado             |
| Citas        | Fecha, odontólogo, paciente, estado      |
| Tratamientos | Nombre, estado                           |
| Pagos        | Fecha, paciente, método                  |
| Usuarios     | Nombre, correo, estado, rol              |
| Auditoría    | Usuario, fecha, acción, módulo           |
| Reportes     | Filtros según reporte                    |

---

# 16. Estados y eliminación de registros

El sistema deberá evitar la eliminación física de información que tenga relevancia histórica.

Se utilizará preferentemente un mecanismo de activación/desactivación para entidades como:

* Pacientes.
* Odontólogos.
* Tratamientos del catálogo.
* Usuarios.

Las entidades transaccionales o históricas, como:

* Citas.
* Tratamientos realizados.
* Pagos.
* Registros de auditoría.
* Consultas clínicas.

deberán conservarse para mantener la trazabilidad del sistema.

---

# 17. Navegación general

La navegación dependerá de los permisos del usuario.

Una estructura inicial podría ser:

```text
Dashboard

Gestión
├── Pacientes
├── Odontólogos
├── Citas
├── Tratamientos
└── Pagos

Clínica
├── Historia Clínica
└── Odontograma

Administración
├── Usuarios
├── Roles y Permisos
└── Auditoría

Reportes
└── Reportes
```

Los elementos de navegación que el usuario no tenga permiso para utilizar no deberán mostrarse.

Sin embargo, la ocultación visual de opciones no sustituirá la validación de autorización en la aplicación.

## 17.1. Dirección no encontrada (error 404)

Si el usuario abre una dirección que no corresponde a ninguna pantalla del sistema (un enlace
roto, o una dirección escrita a mano en el navegador), el sistema muestra una pantalla propia
de "Página no encontrada", con una ilustración y un botón para volver al inicio. La respuesta
conserva el código HTTP 404.

La misma pantalla se muestra en los dos caminos posibles:

| Cómo llega el usuario a la dirección | Quién muestra la pantalla |
|---|---|
| Escribe la dirección directamente en el navegador | El servidor, mediante `UseStatusCodePagesWithReExecute("/404")` en `Program.cs` |
| Llega navegando dentro de la aplicación (enlace roto) | El router de Blazor, mediante `<NotFound>` en `Components/Routes.razor` |

La pantalla también es accesible de forma directa en `/404` (con respuesta 200), lo que permite
verla o capturarla sin inventar una dirección inválida.

La imagen mostrada vive en `wwwroot/images/404.png` y es reemplazable: basta sustituir ese
archivo. El botón "Volver al inicio" que viene dibujado dentro de la imagen se vuelve
cliqueable mediante un enlace transparente superpuesto, cuyas medidas (en porcentajes) están
en `Components/PaginaNoEncontrada.razor.css`.

La ilustración ocupa todo el espacio disponible del área de contenido y, si la ventana se hace
más pequeña (o más baja), se contrae sin cortarse ni deformarse. Para lograrlo se toma el menor
entre dos límites: el ancho disponible y el ancho que permite el alto de la ventana. El alto se
obtiene de `100dvh` menos los 18px de margen que el sistema pone arriba y abajo del área de
contenido, multiplicado por la proporción de la imagen (1264 × 843). Así:

- en una ventana ancha y baja, manda el alto y la imagen queda centrada con aire a los lados;
- en una ventana angosta, manda el ancho y la imagen ocupa todo el ancho;
- en pantallas de 900px o menos, la barra lateral se oculta y la imagen aprovecha ese espacio.

La propiedad `aspect-ratio: 1264 / 843` conserva la proporción exacta de la imagen, que es lo que
garantiza que el enlace del botón siga cayendo en el mismo lugar en cualquier tamaño de ventana
(verificado a 1600×1000, 1366×768 y 900×650).

---

# 18. Principales flujos funcionales

Los módulos estarán relacionados principalmente mediante los siguientes flujos:

### Flujo de atención

```text
Paciente
   ↓
Cita
   ↓
Atención
   ↓
Historia Clínica
   ↓
Odontograma
   ↓
Tratamiento
   ↓
Pago
```

### Flujo administrativo

```text
Usuario
   ↓
Rol
   ↓
Permisos
   ↓
Acceso a módulos
   ↓
Operaciones auditadas
```

### Flujo de tratamiento

```text
Catálogo de tratamiento
          ↓
Tratamiento realizado
          ↓
Paciente
          ↓
Pieza dental (si aplica)
          ↓
Pagos
```

Estos flujos servirán como base para definir posteriormente los casos de uso y las reglas de negocio.

---

# 19. Criterios generales para la implementación

Las funcionalidades descritas en este documento representan el alcance funcional inicial.

Durante la implementación:

* No deberán agregarse módulos fuera del alcance sin documentarlos previamente.
* Las reglas de negocio deberán implementarse fuera de la interfaz cuando corresponda.
* Las operaciones sensibles deberán estar protegidas mediante autorización.
* Las operaciones relevantes deberán generar registros de auditoría.
* La información histórica deberá conservarse.
* La interfaz deberá priorizar funcionalidad y claridad sobre complejidad visual.
* Las funcionalidades deberán mantenerse suficientemente simples para el contexto académico del proyecto.

Cualquier modificación importante del alcance deberá reflejarse en este documento antes de implementarse.
