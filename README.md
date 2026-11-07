# Sonrisa Digital

**Sistema de gestión integral para clínicas dentales**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Interactive%20Server-512BD4?logo=blazor&logoColor=white)](https://learn.microsoft.com/aspnet/core/blazor/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![EF Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4)](https://learn.microsoft.com/ef/core/)
[![Licencia](https://img.shields.io/badge/Licencia-Propietaria-red)](#licencia)

Sonrisa Digital centraliza la operación clínica y administrativa de una clínica dental en una sola aplicación web: pacientes, odontólogos, citas, historia clínica, odontograma, tratamientos y pagos, con control de acceso granular y auditoría de actividades.

---

## Tabla de contenido

- [Descripción general](#descripción-general)
- [Funcionalidades](#funcionalidades)
- [Roles y permisos](#roles-y-permisos)
- [Arquitectura](#arquitectura)
- [Tecnologías](#tecnologías)
- [Requisitos](#requisitos)
- [Instalación y configuración](#instalación-y-configuración)
- [Primer acceso](#primer-acceso)
- [Seguridad en producción](#seguridad-en-producción)
- [Documentación](#documentación)
- [Hoja de ruta](#hoja-de-ruta)
- [Versionado](#versionado)
- [Licencia](#licencia)
- [Contacto y soporte](#contacto-y-soporte)

---

## Descripción general

Sonrisa Digital está diseñado para el **personal interno de la clínica** (administración, recepción y odontólogos). No incluye, por ahora, un portal para pacientes.

**Problema que resuelve**

- Información dispersa entre hojas de cálculo, agendas y registros en papel.
- Falta de trazabilidad sobre quién consultó o modificó información clínica.
- Dificultad para conocer citas del día, tratamientos en curso y saldos pendientes.

**Propuesta de valor**

- Una única fuente de verdad para la operación diaria.
- Acceso basado en roles y permisos, no en "confianza".
- Registro automático de actividades para auditoría.
- Interfaz web sin instalación en los equipos de la clínica.

---

## Funcionalidades

### Módulos implementados

| Módulo | Descripción | Estado |
|---|---|---|
| **Dashboard** | Indicadores de pacientes y odontólogos activos, citas del día, actividad reciente y saldo pendiente. | ✅ |
| **Pacientes** | Registro, búsqueda, edición, detalle y activación/desactivación de pacientes. | ✅ |
| **Odontólogos** | Gestión del personal odontológico y su información profesional. | ✅ |
| **Citas** | Programación, consulta, edición, cancelación y atención de citas. | ✅ |
| **Historia clínica** | Registro y consulta de información clínica por paciente. | ✅ |
| **Odontograma** | Odontograma interactivo con catálogo de hallazgos dentales. | ✅ |
| **Tratamientos** | Catálogo y registro de tratamientos asociados al flujo clínico. | ✅ |
| **Pagos** | Registro de pagos, historial y control de saldos pendientes. | ✅ |
| **Usuarios** | Administración de cuentas, roles y estado de acceso. | ✅ |
| **Roles y permisos** | Asignación granular de permisos por rol (38 permisos en 14 módulos). | ✅ |
| **Auditoría** | Registro automático de operaciones y actividad reciente en el dashboard. | ✅ |
| **Centro de ayuda** | Guías, preguntas frecuentes, solución de problemas y contacto de soporte. | ✅ |
| **Reportes** | Panel de reportes de citas, pacientes atendidos, tratamientos y pagos con filtros, exportación PDF y alcance por odontólogo. | ✅ |
| **Suscripción** | Prueba de 30 días desde el primer ingreso, mensualidad en USD, registro manual de pagos y bloqueo automático al vencer. | ✅ |

### Capacidades transversales

- Autenticación con **ASP.NET Core Identity** (cookie, bloqueo por intentos fallidos).
- Autorización basada en **permisos** (`sonrisadigital.permission`), no solo en roles.
- Protección **antiforgery** en formularios y acciones sensibles.
- **HTTPS** obligatorio y **HSTS** en producción.
- Página 404 personalizada y manejo controlado de errores.
- Datos iniciales (roles, permisos, Dueño y catálogo odontográfico) creados automáticamente al arrancar. La clínica demo y su administrador solo se crean en Development/E2E.
- **Fechas y horas del negocio** centralizadas en `RelojNegocio` (`America/Santo_Domingo`). Las fechas de calendario (citas, pagos, tratamientos, nacimiento y estudios) usan `DateOnly` y se guardan como columna `date`, así que no se desplazan por la zona horaria del servidor. Las marcas de tiempo reales (registro, subida, auditoría) se guardan en UTC.

### Suscripción y facturación

Cada clínica opera con una mensualidad en **USD** (un solo plan mientras se valida el producto) y recibe **30 días de prueba** que empiezan a contar en su **primer ingreso**, no al crearla.

- El **Dueño** registra los pagos manualmente (transferencia, efectivo o tarjeta) desde **Clínicas → Facturación**; no hay pasarela de pago.
- Un pago extiende el vencimiento un mes desde la fecha de vencimiento actual o desde hoy si ya venció.
- Al vencer, la clínica tiene **3 días de gracia** con un aviso visible; después se bloquea el acceso sin borrar datos.
- El Dueño siempre puede entrar, incluso con la clínica bloqueada, para registrar el pago.
- La regla se evalúa en cada inicio de sesión, navegación y revalidación de sesión; no depende de un proceso programado.
- Una clínica suspendida por **falta de pago** se reactiva automáticamente al registrar el pago.
- Una clínica suspendida por **decisión administrativa** conserva el bloqueo después del pago; el Dueño debe reactivarla explícitamente.
- Un vencimiento nulo no concede acceso: una clínica comercial o una prueba ya iniciada sin fecha queda vencida.

El precio propuesto y los días de aviso se configuran en `Suscripcion` (`PrecioMensualUsd` y `DiasAviso`). El monto real de cada pago queda guardado en su propio registro.

---

## Roles y permisos

Sonrisa Digital incluye cuatro roles predefinidos: **Dueño**, **Administrador**, **Recepcionista** y **Odontólogo**. Los roles y sus permisos son **globales**: un cambio afecta a todas las clínicas y no se personaliza por clínica.

| Módulo | Dueño | Administrador | Recepcionista | Odontólogo |
|---|---|---|---|---|
| Pacientes | Total | Total | Total | Consulta |
| Odontólogos | Total | Total | Consulta | Consulta |
| Citas | Total | Total | Total (excepto atender) | Consulta y atención |
| Historia clínica | Total | Total | Consulta | Total |
| Odontograma | Total | Total | Consulta | Total |
| Tratamientos | Total | Total | Consulta | Total |
| Pagos | Total | Total | Registrar y consultar | — |
| Usuarios | Total | — | — | — |
| Roles y permisos | Total | — | — | — |
| Auditoría | Total | — | — | — |
| Clínicas | Total | — | — | — |
| Archivos del paciente (ocultar/restaurar) | Total | — | — | — |
| Reportes | Total | Total | Consulta | Consulta |

> **Regla dura:** Usuarios, Roles y permisos, Auditoría, Clínicas y ocultar/restaurar archivos son exclusivos del Dueño. El sistema rechaza cualquier intento de asignar esos permisos a otro rol, aunque quien edite sea Dueño.
> Los roles son acumulativos: un usuario puede tener más de un rol y recibe la unión de sus permisos.
> En **Reportes**, el usuario con rol Odontólogo ve únicamente su propia actividad clínica.

---

## Arquitectura

Monolito modular organizado en tres capas, con dependencias unidireccionales:

```mermaid
flowchart TB
    UI["SonrisaDigital.UI<br/>Blazor · Componentes · Páginas"]
    Services["SonrisaDigital.Services<br/>Casos de uso · Validaciones · Autorización · Auditoría"]
    Data["SonrisaDigital.Data<br/>EF Core · Repositorios · Identity"]
    DB[("SQL Server")]

    UI --> Services
    Services --> Data
    Data --> DB
```

**Principios**

- La UI **no** accede directamente al `DbContext`.
- La lógica de negocio y las validaciones viven en `Services`.
- El acceso a datos se encapsula en repositorios dentro de `Data`.
- La autorización se evalúa por permisos, no por nombres de rol.

### Estructura del repositorio

```text
SonrisaDigital/
├── src/
│   ├── SonrisaDigital.UI/          # Aplicación Blazor (punto de entrada)
│   ├── SonrisaDigital.Services/    # Lógica de negocio y casos de uso
│   └── SonrisaDigital.Data/        # Entidades, DbContext, repositorios, migraciones
├── docs/                    # Visión, roles, módulos, modelo de dominio y arquitectura
├── SonrisaDigital.slnx             # Solución .NET
└── README.md
```

---

## Tecnologías

| Componente | Tecnología | Versión |
|---|---|---|
| Plataforma | .NET | 10.0 |
| Interfaz | Blazor Web App (Interactive Server) | .NET 10 |
| Persistencia | Entity Framework Core | 10.0.12 |
| Base de datos | Microsoft SQL Server | 2019+ / 2022 |
| Seguridad | ASP.NET Core Identity | 10.0.13 |
| Arquitectura | Monolito modular por capas | — |

---

## Requisitos

- **.NET SDK 10.0** o superior.
- **SQL Server** (Express, Developer o Standard) accesible desde el equipo.
- Herramienta **`dotnet-ef`** (solo para aplicar migraciones).
- Navegador moderno (Chrome, Edge, Firefox o Safari recientes).

---

## Instalación y configuración

### 1. Restaurar dependencias

```powershell
dotnet restore SonrisaDigital.slnx
```

### 2. Crear la base de datos

Crea una base de datos vacía llamada `SonrisaDigital` en SQL Server (SSMS, Azure Data Studio o `sqlcmd`). Las tablas se crean con las migraciones; **no** es necesario ejecutar scripts SQL manuales.

### 3. Configurar la cadena de conexión y el acceso inicial

Para desarrollo, usa **user-secrets** en lugar de editar archivos versionados:

```powershell
dotnet user-secrets init --project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj

dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "Server=localhost;Database=SonrisaDigital;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True" `
  --project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj
```

En **Development/E2E**, la aplicación crea la clínica `clinica-demo` y su administrador con estas claves. Son opcionales y se ignoran en producción:

```powershell
dotnet user-secrets set "Authentication:AdminEmail" "admin@tudominio.com" `
  --project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj

dotnet user-secrets set "Authentication:AdminPassword" "CAMBIA-ESTA-CLAVE" `
  --project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj
```

Para probar el flujo real de producción en desarrollo, configura la cuenta global del Dueño:

```powershell
dotnet user-secrets set "Dueno:Email" "dueno@tudominio.com" `
  --project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj

dotnet user-secrets set "Dueno:Password" "CAMBIA-ESTA-CLAVE" `
  --project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj
```

En **producción**, `Dueno:Email` y `Dueno:Password` son obligatorios. La aplicación no crea la demo ni un administrador automático: el Dueño registra la primera clínica y luego crea su administrador desde la interfaz.

Ajusta `Server=` según tu instancia (por ejemplo `localhost\SQLEXPRESS` o `MI-SERVIDOR`).

La mensualidad se configura en dólares; cambia el precio real antes de facturar:

```powershell
dotnet user-secrets set "Suscripcion:PrecioMensualUsd" "20" `
  --project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj
```

### 4. Aplicar las migraciones

```powershell
dotnet tool install --global dotnet-ef   # solo la primera vez

dotnet ef database update `
  --project src/SonrisaDigital.Data/SonrisaDigital.Data.csproj `
  --startup-project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj
```

Esto crea las tablas, relaciones, índices y las tablas de ASP.NET Core Identity.

### 5. Ejecutar la aplicación

```powershell
dotnet run --project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj
```

También puedes iniciar el proyecto `SonrisaDigital.UI` desde Visual Studio o VS Code. La URL se muestra en la consola.

---

## Primer acceso

Al arrancar por primera vez, la aplicación crea automáticamente:

- Los roles **Administrador**, **Recepcionista**, **Odontólogo** y **Dueño**.
- Los 38 permisos del catálogo y su asignación inicial.
- La cuenta global del **Dueño**, definida en `Dueno:Email` / `Dueno:Password`. Es obligatoria en producción y no pertenece a ninguna clínica.
- El catálogo base del odontograma.

En **Development/E2E** también se crea la clínica `clinica-demo` y el administrador de `Authentication:AdminEmail` / `Authentication:AdminPassword` para que las pruebas tengan una clínica donde trabajar. En **producción no se crea ni la demo ni un administrador automático**.

Inicia sesión con la cuenta del Dueño y:

1. Entra en **Clínicas → Nueva clínica** y registra la clínica real.
2. En la fila de esa clínica, pulsa **Usuarios** para seleccionarla.
3. Pulsa **Registrar usuario**, asigna el rol **Administrador** y define su contraseña.
4. Cierra sesión y entra con el nuevo administrador para operar la clínica.
5. Revisa y ajusta los permisos por rol.

> Si la base de datos no existe o no tiene migraciones aplicadas, en Development el seeding se omite y se registra una advertencia. En producción el arranque falla para no quedar sin Dueño.

---

## Seguridad en producción

Antes de desplegar con clientes reales:

> La guía de instalación, configuración, respaldos y operación se entrega aparte, junto con la instalación de cada clínica.

- [x] Login de demostración retirado y sin credenciales por defecto en la interfaz.
- [x] La aplicación valida al arrancar el dominio, la cadena de conexión y la ruta de Data Protection fuera de Development.
- [x] Producción no crea la clínica `clinica-demo` ni un administrador automático; la primera clínica y su administrador se crean desde la cuenta del Dueño.
- [ ] Cambiar la contraseña del Dueño después del primer acceso.
- [ ] Mover cadenas de conexión y credenciales a **variables de entorno** o un almacén de secretos.
- [ ] No versionar `appsettings.Development.json` ni archivos `.env`.
- [x] `appsettings.Development.json` y su plantilla quedan excluidos de `dotnet publish`.
- [ ] Configurar `DataProtection:KeysPath` en una carpeta persistente y respaldada.
- [ ] Publicar únicamente sobre **HTTPS** y mantener **HSTS** habilitado.
- [ ] Usar un usuario de SQL Server con permisos mínimos (no `sa`).
- [ ] Configurar respaldos automáticos de la base de datos y probar la restauración.
- [ ] Revisar periódicamente la **auditoría** de actividades.
- [ ] Mantener actualizados .NET y los paquetes NuGet.
- [ ] Definir política de contraseñas y retención de datos clínicos.

---

## Documentación

| Documento | Contenido |
|---|---|
| [Visión y alcance](docs/01-vision-alcance.md) | Objetivos, alcance funcional y límites del sistema. |
| [Actores, roles y permisos](docs/02-actores-rolels.md) | Perfiles de usuario y reglas de autorización. |
| [Módulos y funcionalidades](docs/03-modulos-funcionalidades.md) | Comportamiento esperado de cada módulo. |
| [Modelo de dominio](docs/04-modelos-dominio.md) | Entidades, relaciones y reglas del negocio. |
| [Arquitectura](docs/05-arquitectura.md) | Capas, dependencias y decisiones técnicas. |

---

## Hoja de ruta

- [x] Módulo de **Reportes** con filtros, períodos predefinidos, alcance por odontólogo y exportación a PDF.
- [x] Pantalla dedicada de **Auditoría** con búsqueda y filtros.
- [x] Exportación de reportes a **PDF** con resumen ejecutivo y anexo de detalle.
- [ ] Recordatorios de citas por correo o WhatsApp.
- [ ] Portal de pacientes (consulta de citas e historial).
- [ ] Soporte multi-clínica / multi-sucursal.
- [x] Pruebas automatizadas y pipeline de integración continua.
- [x] Despliegue en IIS documentado en [docs/06-despliegue.md](docs/06-despliegue.md). Incluye un `Dockerfile` para contenedores Windows, todavía sin verificar.
- [ ] Puesta en producción y aceptación con la clínica.
- [ ] Interfaz en inglés (i18n).

---

## Versionado

El proyecto sigue [Versionado Semántico](https://semver.org/lang/es/) (`MAYOR.MENOR.PARCHE`). Los cambios relevantes se documentarán en un `CHANGELOG.md`.

---

## Licencia

**Software propietario. Todos los derechos reservados.**

Este código se distribuye únicamente bajo autorización expresa del titular. Queda prohibida su copia, modificación, distribución o uso comercial sin un acuerdo por escrito. El uso del software por parte de clientes se rige por el contrato de licencia correspondiente (EULA).

---

## Contacto y soporte

- **Titular:** Ernest José Santiago Cambero
- **Correo:** ernestsantiago03@gmail.com
- **Soporte:** Pendiente de definir
- **Sitio web:** Pendiente de definir

---

© 2026 Sonrisa Digital. Todos los derechos reservados.
