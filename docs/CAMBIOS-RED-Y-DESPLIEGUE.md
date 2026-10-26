# Cambios — Red, despliegue, integridad y rendimiento

Documento acumulativo de los cambios de red, despliegue, resiliencia, integridad de datos y rendimiento.

## Proxy, HTTPS y HSTS

**Archivos modificados**
- `src/SonrisaDigital.UI/Program.cs`
- `src/SonrisaDigital.UI/appsettings.json`
- `src/SonrisaDigital.UI/appsettings.Production.json`

### Qué se cambió

1. **ForwardedHeaders**
   - `app.UseForwardedHeaders()` es el primer middleware del pipeline.
   - Se leen `X-Forwarded-For` y `X-Forwarded-Proto`.
   - `ForwardLimit` se toma de `ReverseProxy:ForwardLimit` (por defecto `1`).
   - Los proxies de confianza se leen de `ReverseProxy:KnownProxies`. Si la lista está vacía se conserva el comportamiento por defecto (loopback), útil para IIS en la misma máquina.

2. **HSTS**
   - `MaxAge = 365 días`.
   - `IncludeSubDomains = true`.
   - `Preload = false` hasta confirmar el dominio en la lista de precarga.
   - `UseHsts()` sigue aplicándose solo fuera de Development.

3. **Redirección HTTPS**
   - `AddHttpsRedirection` usa `308 Permanent Redirect`.
   - `UseHttpsRedirection()` se movió antes de autenticación/autorización para que un request HTTP no genere un challenge de login sobre HTTP.

### Configuración

```json
"ReverseProxy": {
  "ForwardLimit": 1,
  "KnownProxies": []
}
```

- **IIS en la misma máquina:** dejar `KnownProxies` vacío (loopback).
- **nginx / balanceador en otra IP:** agregar la IP del proxy, por ejemplo `[ "10.0.0.10" ]`.

### Verificación

- `dotnet build SonrisaDigital.slnx` → 0 errores, 0 warnings.
- Development:
  - HTTP sin cabecera → `308` hacia HTTPS.
  - HTTP con `X-Forwarded-Proto: https` → `302` al login, sin redirección a HTTPS.
- Production:
  - HTTP → `308` hacia HTTPS.
  - HTTPS devolvió `Strict-Transport-Security: max-age=31536000; includeSubDomains`.
  - También se verificaron `X-Content-Type-Options: nosniff` y `X-Frame-Options: DENY`.

## Resiliencia de la base de datos

**Archivos modificados**
- `src/SonrisaDigital.Services/DependencyInjection.cs`
- `src/SonrisaDigital.Data/DatabaseFacadeExtensions.cs` (nuevo)
- `src/SonrisaDigital.Services/Usuarios/UsuarioService.cs`
- `src/SonrisaDigital.Services/Autorizacion/RolService.cs`
- `src/SonrisaDigital.Data/Repositories/OdontogramaRepository.cs`
- `src/SonrisaDigital.Services/Tratamientos/TratamientoService.cs`

### Qué se cambió

1. **Reintentos automáticos**
   - `UseSqlServer` configura `EnableRetryOnFailure(5, 30s, null)`.
   - Los errores transitorios de SQL Server se reintentan hasta 5 veces.
   - `CommandTimeout` se elevó a 60 segundos para reportes y odontogramas grandes.

2. **Transacciones compatibles con reintentos**
   - Se creó `DatabaseFacadeExtensions.ExecuteInTransactionAsync`, que:
     - Obtiene la estrategia de ejecución de EF Core.
     - Abre la transacción dentro de esa estrategia.
     - Ejecuta la operación, hace `Commit` y, si falla, `Rollback`.
   - Hay dos variantes:
     - Sobre `DatabaseFacade`: para operaciones que ya controlan sus entidades.
     - Sobre `DbContext`: limpia el rastreador antes de cada intento, para que un reintento no reutilice entidades marcadas por el intento anterior.
   - Se reemplazaron todos los `BeginTransactionAsync` manuales por este helper:
     - `UsuarioService` (crear, editar, activar/desactivar y restablecer contraseña).
     - `RolService` (actualizar permisos).
     - `OdontogramaRepository` (guardar pieza + auditoría).
     - `TratamientoService` (helper usado por crear/editar/cancelar tratamientos).

3. **Auditoría dentro de la transacción**
   - En `UsuarioService` y `RolService` la auditoría se movió antes del `Commit`.
   - Así el cambio y su registro quedan juntos: si falla la auditoría, se deshace la operación completa.
   - En `OdontogramaRepository` y `TratamientoService` la auditoría ya estaba dentro de la transacción.

### Por qué

Con `EnableRetryOnFailure`, EF Core lanza una excepción si se abre una transacción manual fuera de la estrategia de ejecución. El helper centraliza el patrón correcto y evita repetir la lógica en cada servicio.

### Nota sobre reintentos

El delegado de `ExecuteInTransactionAsync` puede ejecutarse más de una vez si ocurre un error transitorio. Por eso:
- En `UsuarioService.CreateAsync` el usuario se crea dentro del bloque.
- En `UsuarioService.UpdateAsync`, `SetStatusAsync` y `ResetPasswordAsync` la carga del usuario se hace dentro del bloque.
- En `RolService` la carga del rol y los permisos se hace dentro del bloque.
- En `OdontogramaRepository` se limpia el rastreador y se recarga o recrea la pieza antes de guardar.
- En `TratamientoService` las altas crean la entidad dentro del bloque; las ediciones y cancelaciones reutilizan una actualización idempotente.

### Verificación

- `dotnet build SonrisaDigital.slnx` → 0 errores, 0 warnings.
- Arranque verificado en `http://localhost:5464` y `https://localhost:7474`:
  - HTTP → `308` hacia HTTPS.
  - `https://localhost:7474/login` → `200`.
- Los logs mostraron `CommandTimeout='60'`, confirmando la nueva configuración de EF Core.

## Health checks y logging

### Health checks

- `GET /health` → `{"status":"Healthy"}` (live, sin dependencias externas).
- `GET /health/ready` → valida `CanConnectAsync` de SQL Server; responde `200` si la base está reachable, `503` si no.
- Ambos endpoints son **anónimos** y se responden por **HTTP sin redirección a HTTPS** (middleware corto-circuito colocado antes de `UseHttpsRedirection`), para que un balanceador/orquestador pueda consultarlos sin recibir un `308`.
- No se expone el detalle de excepciones de base de datos (cadena de conexión, servidor, trazas de pila).

### Logging

- `appsettings.json`: `Default: Information`, `Microsoft.AspNetCore: Warning`, `Microsoft.EntityFrameworkCore: Warning`, `SonrisaDigital: Information`.
- `appsettings.Production.json`: `Default: Warning`, `Microsoft.AspNetCore: Warning`, `Microsoft.EntityFrameworkCore: Warning`, `SonrisaDigital: Error`.
- En producción se reduce el volumen y solo se registran errores de la aplicación, evitando llenar el disco con trazas de desarrollo.

## PDF por HTTP

**Archivos modificados**
- `src/SonrisaDigital.UI/Program.cs`
- `src/SonrisaDigital.UI/Pages/Reportes/Reportes.razor`
- `src/SonrisaDigital.UI/Pages/Reportes/Reportes.razor.css`
- `src/SonrisaDigital.UI/Components/App.razor`
- `src/SonrisaDigital.UI/wwwroot/js/reportes.js` (eliminado)

### Qué se cambió

1. **Endpoint HTTP de descarga**
   - `GET /reportes/pdf` recibe `tipo`, `desde`, `hasta`, `odontologoId`, `estadoCita`, `estadoTratamiento` y `metodoPago`.
   - Valida el tipo de reporte, los enums y el rango de fechas; devuelve `400` con un mensaje claro si algo no es válido.
   - Genera el PDF con `IReportePdfService` y lo entrega con `Results.File`, que establece `Content-Type: application/pdf` y `Content-Disposition: attachment`.
   - Responde `Cache-Control: no-store` para que los datos clínicos y financieros no queden en caché.
   - Exige la política `Reportes.Ver`; los permisos específicos (`Citas.Ver`, `Pacientes.Ver`, `Tratamientos.Ver`, `Pagos.Ver`) siguen validándose dentro de `ReporteService`.

2. **Usuario actual en la petición HTTP**
   - `ICurrentUserService` se llena normalmente desde `MainLayout` en el circuito de Blazor.
   - Una petición HTTP a `/reportes/pdf` usa otro scope de DI, por lo que el endpoint alimenta `ICurrentUserService` desde `HttpContext.User` antes de llamar al servicio.
   - Sin este paso, `ReporteService.CrearContextoAsync` no encontraría la sesión y devolvería `401/403`.

3. **Descarga desde la interfaz**
   - Los cuatro botones "Descargar PDF" se reemplazaron por enlaces `<a>` a `/reportes/pdf`.
   - La URL se construye con `NavigationManager.GetUriWithQueryParameters` usando el mismo `CrearFiltro()` de la pantalla.
   - `data-enhance-nav="false"` evita que la navegación mejorada de Blazor intercepte el enlace y permite que el navegador procese `Content-Disposition: attachment`.
   - Se eliminó `DescargarPdfAsync`, `cargandoPdf`, `IJSRuntime`, `IReportePdfService` de la página y el script `reportes.js`.
   - Ya no se convierte el PDF a base64 ni se transporta por SignalR.

### Por qué

- El PDF viaja por HTTP con semántica de descarga correcta.
- Se elimina el aumento de tamaño de base64 (~33%).
- La generación no bloquea el circuito de Blazor.
- El endpoint reutiliza exactamente las mismas consultas, filtros y permisos que la pantalla.

### Verificación

- `dotnet build SonrisaDigital.slnx` → 0 errores, 0 warnings.
- Sesión real iniciada con el administrador y descarga de los cuatro reportes:
  - `Citas` → `200`, `application/pdf`, 44 KB.
  - `Pacientes` → `200`, `application/pdf`, 39 KB.
  - `Tratamientos` → `200`, `application/pdf`, 43 KB.
  - `Pagos` → `200`, `application/pdf`, 46 KB.
- Cabeceras verificadas:
  - `Content-Type: application/pdf`
  - `Content-Disposition: attachment; filename=reporte-citas-20250101-20261231.pdf`
  - `Cache-Control: no-cache,no-store`
- Los archivos descargados empiezan con la firma `%PDF-`.
- Filtros válidos (`estadoCita=Atendida`, `estadoTratamiento=Completado`, `metodoPago=Efectivo`, `odontologoId=1`) → `200`.
- Casos inválidos:
  - Tipo desconocido → `400`.
  - Fechas ausentes → `400`.
  - Rango invertido → `400`.
  - Enum desconocido → `400`.
- Sin sesión → `302` a `/login?ReturnUrl=...`.
- La página `/reportes` renderiza el enlace `href="/reportes/pdf?tipo=Citas&desde=...&hasta=..."` con `data-enhance-nav="false"` y ya no carga `reportes.js`.

## Circuitos y reconexión

**Archivos modificados**
- `src/SonrisaDigital.UI/Program.cs`
- `src/SonrisaDigital.UI/_Imports.razor`
- `src/SonrisaDigital.UI/Components/App.razor`
- `src/SonrisaDigital.UI/Components/ReconnectModal.razor` (nuevo)
- `src/SonrisaDigital.UI/Components/ReconnectModal.razor.css` (nuevo)
- `src/SonrisaDigital.UI/Components/ReconnectModal.razor.js` (nuevo)
- `src/SonrisaDigital.UI/Pages/Login/Login.razor`
- `src/SonrisaDigital.UI/Pages/Error/Error.razor`
- `src/SonrisaDigital.UI/Pages/Error/Pagina404.razor`
- `src/SonrisaDigital.UI/Components/AccesoDenegado.razor`

### Qué se cambió

1. **Opciones del circuito**
   - Se configuró `CircuitOptions` desde `AddInteractiveServerComponents`.
   - `DisconnectedCircuitRetentionPeriod = 5 minutos`: el servidor conserva el estado de un circuito desconectado durante 5 minutos, en lugar de los 3 minutos predeterminados. Esto permite recuperar el trabajo en curso tras una caída breve de red o después de suspender el equipo.
   - `DisconnectedCircuitMaxRetained = 100`: límite explícito de circuitos desconectados retenidos en memoria. Es suficiente para el uso de una clínica y evita un consumo sin control ante un pico de desconexiones.
   - `DetailedErrors = IsDevelopment()`: los detalles técnicos de excepciones solo se envían al navegador en Development. En producción no se filtran rutas, consultas SQL ni datos internos.
   - No se modificó `HubOptions.MaximumReceiveMessageSize`. Los PDF ya se descargan por HTTP, por lo que no hay mensajes grandes viajando por SignalR y se conserva el límite predeterminado de 32 KB.

2. **Modal de reconexión**
   - Se agregó `ReconnectModal` al `App.razor`, antes de `_framework/blazor.web.js`.
   - El componente usa un `<dialog id="components-reconnect-modal">`, que Blazor controla con las clases y el evento `components-reconnect-state-changed`.
   - Mensajes en español para cada estado:
     - `show`: "Reconectando con el servidor..." con indicador animado.
     - `retrying`: "No se pudo reconectar. Nuevo intento en N segundos."
     - `failed`: "No fue posible reconectar. Reintenta o recarga la página." con botón **Reintentar**.
     - `paused`: "La sesión fue pausada por el servidor." con botón **Reanudar**.
     - `resume-failed`: "No fue posible reanudar la sesión." con botón **Reanudar**.
     - `rejected`: recarga automática de la página, porque el servidor ya no conserva el circuito.
   - El script abre y cierra el diálogo, reintenta con `Blazor.reconnect()`, reanuda con `Blazor.resumeCircuit()` y vuelve a intentar cuando la pestaña recupera el foco.
   - El CSS es scoped, usa la paleta de la aplicación, respeta `prefers-reduced-motion` y mantiene el foco visible en los botones.
   - El script se carga como módulo con `@Assets`, por lo que recibe el nombre con fingerprint generado por `MapStaticAssets`.

3. **Páginas anónimas sin circuito**
   - `App.razor` calcula `PageRenderMode` con `HttpContext.AcceptsInteractiveRouting()`.
   - `HeadOutlet` y `Routes` usan ese modo: `InteractiveServer` para las páginas normales y `null` (SSR estático) para las páginas marcadas con `[ExcludeFromInteractiveRouting]`.
   - Se aplicó `[ExcludeFromInteractiveRouting]` a:
     - `Login.razor`
     - `Error.razor`
     - `Pagina404.razor`
     - `AccesoDenegado.razor`
   - El formulario de login sigue siendo un POST estándar y funciona sin circuito.
   - `AccesoDenegado.razor` ahora lee `HttpContext.User` en lugar de `AuthenticationStateProvider`, porque en SSR estático la cookie se refleja en el contexto HTTP y el proveedor del circuito no existe.
   - Se agregó `@using Microsoft.AspNetCore.Components` a `_Imports.razor` para disponer del atributo.

### Por qué

- Sin una UI de reconexión, una caída breve de red deja al usuario con una pantalla congelada sin explicación.
- Retener el circuito 5 minutos reduce la pérdida de formularios y consultas en curso.
- El modal distingue entre una caída temporal, una sesión pausada y un circuito que ya no existe, y ofrece la acción correcta en cada caso.
- Las páginas anónimas no gastan circuitos ni generan errores de negociación en la consola del navegador.
- Los detalles técnicos quedan fuera de producción.

### Verificación

- `dotnet build SonrisaDigital.slnx` → 0 errores, 0 warnings.
- La aplicación arranca correctamente en `http://localhost:5464` y `https://localhost:7474`.
- El HTML de `/login` contiene:
  - `<dialog id="components-reconnect-modal">` con los mensajes y botones.
  - `<script type="module" src="Components/ReconnectModal.<fingerprint>.razor.js">`.
- El asset JS responde `200` con `text/javascript` y el CSS scoped contiene las reglas `components-reconnect-*`.
- Prueba con Chrome headless simulando los estados del evento:
  - `show` → diálogo abierto y mensaje de reconexión visible.
  - `retrying` → mensaje de nuevo intento visible.
  - `failed` → mensaje de fallo y botón **Reintentar** visibles.
  - `paused` → mensaje de sesión pausada y botón **Reanudar** visibles.
  - `hide` → diálogo cerrado.
- Prueba real de desconexión en una página autenticada:
  - Inicio de sesión con el administrador de desarrollo y carga del panel `/`.
  - Se verificó que el circuito SignalR estaba conectado (`/_blazor/negotiate` y WebSocket activo).
  - Se terminó el proceso del servidor mientras la página seguía abierta.
  - El WebSocket se cerró con código `1006`.
  - El modal se abrió automáticamente con las clases `components-reconnect-show components-reconnect-retrying` y el mensaje de reconexión visible.
- En `/login` ya no se solicita `/_blazor/negotiate` ni aparece el `401` de una negociación sin sesión. La página se renderiza estáticamente y el formulario de login sigue funcionando.
- `/acceso-denegado` sin sesión responde `302` a `/login?returnUrl=%2Facceso-denegado`.
- `/acceso-denegado` con sesión autenticada muestra el mensaje "No tienes permisos para esta sección".
- `/404` responde `200` con la página estática de error.

## Guía de despliegue

Se agregó `docs/06-despliegue.md` con los procedimientos de instalación y operación:

- Publicación en IIS y configuración de nginx como proxy inverso.
- Variables de entorno, cadena de conexión, HTTPS, HSTS y proxies de confianza.
- Aplicación controlada de migraciones y verificación con `/health` y `/health/ready`.
- Respaldos, restauración, actualización y rollback.
- Lista de comprobación previa a producción y solución de problemas frecuentes.

El `README.md` enlaza la guía desde la sección de despliegue y desde la tabla de documentación.

## Integridad de citas y pagos

**Archivos modificados**
- `src/SonrisaDigital.Data/DatabaseFacadeExtensions.cs`
- `src/SonrisaDigital.Data/ApplicationDbContext.cs`
- `src/SonrisaDigital.Data/Repositories/ICitaRepository.cs`
- `src/SonrisaDigital.Data/Repositories/CitaRepository.cs`
- `src/SonrisaDigital.Data/Repositories/CitaConflicto.cs`
- `src/SonrisaDigital.Data/Repositories/IPagoRepository.cs`
- `src/SonrisaDigital.Data/Repositories/PagoRepository.cs`
- `src/SonrisaDigital.Services/Citas/CitaService.cs`
- `src/SonrisaDigital.Services/Pagos/PagoService.cs`
- `src/SonrisaDigital.UI/Pages/Citas/CitaForm.razor`
- `src/SonrisaDigital.Data/Migrations/20261025015324_AddCitaPacienteFechaIndex.cs`

### Qué se cambió

1. **Choque de horario en el servicio**
   - `CitaRepository.BuscarConflictoAsync` revisa si el odontólogo o el paciente ya tienen una cita activa que se cruce con el horario solicitado.
   - Las citas canceladas y las inasistencias no bloquean el horario.
   - Al editar, la propia cita se excluye de la comparación.
   - La validación se hace por identificadores, no por nombres, y ya no depende de la pantalla.

2. **Reserva protegida contra carreras**
   - `CitaService.CreateAsync` y `UpdateAsync` comprueban el choque y guardan la cita dentro de una transacción con aislamiento `Serializable`.
   - `CitaService.SetEstadoAsync` también guarda el estado, la consulta clínica y la auditoría en una sola transacción.
   - `DatabaseFacadeExtensions` incorpora una variante con nivel de aislamiento y limpia el rastreador cuando una transacción falla.

3. **Reglas de agenda en el servicio**
   - Se validan días laborables, horario de 8:00 a.m. a 6:00 p.m. y el bloque de almuerzo de 12:00 a 2:00 p.m.
   - Se normaliza el motivo de la cita antes de guardarlo.
   - Se impide editar una cita que ya está en estado terminal.

4. **Registro de pagos transaccional**
   - `PagoService.RegistrarAsync` valida el monto y el saldo, guarda el pago, cambia el tratamiento a completado cuando corresponde y registra la auditoría dentro de la misma transacción.
   - `PagoRepository.GetTratamientoParaPagoAsync` bloquea la fila del tratamiento con `UPDLOCK, HOLDLOCK` hasta terminar la transacción. Dos cobros simultáneos ya no pueden calcular el saldo sobre los mismos datos.
   - Si falla la auditoría o el guardado, se revierten el pago y el cambio de estado del tratamiento.

5. **Índice para la agenda del paciente**
   - La migración `AddCitaPacienteFechaIndex` reemplaza el índice simple de `PacienteId` por uno compuesto de `PacienteId`, `Fecha` y `HoraInicio`.
   - El índice del odontólogo ya existía y se conserva.

### Por qué

- La validación anterior solo vivía en `CitaForm.razor` y comparaba nombres. Dos recepcionistas podían reservar el mismo horario al mismo tiempo.
- Un pago y su auditoría podían quedar separados: si la auditoría fallaba, el cobro ya estaba guardado.
- Dos cobros simultáneos podían leer el mismo saldo y aceptar montos que juntos superaran el precio del tratamiento.
- Las reglas de agenda deben cumplirse aunque la petición no venga del formulario web.

### Verificación

- `dotnet build SonrisaDigital.slnx` → 0 errores, 0 warnings.
- `dotnet ef migrations script` genera el cambio de índice sin errores.
- La concurrencia de citas y pagos se verificó con dos sesiones reales contra SQL Server en el proyecto `SonrisaDigital.Tests` (ver la sección de pruebas).

### Despliegue

La migración `20261025015324_AddCitaPacienteFechaIndex` ya está aplicada en la base local `SonrisaDigital` y el índice `IX_Citas_PacienteId_Fecha_HoraInicio` quedó confirmado como `NONCLUSTERED`. En el servidor de producción sigue pendiente:

```bash
dotnet ef database update --project src/SonrisaDigital.Data --startup-project src/SonrisaDigital.UI
```

El índice solo afecta a `Citas`; no modifica datos existentes.

## Rendimiento de consultas

**Archivos modificados**
- `src/SonrisaDigital.Data/Common/PagoTotales.cs` (nuevo)
- `src/SonrisaDigital.Data/Common/PagedResult.cs`
- `src/SonrisaDigital.Data/Entities/EstadoFinanciero.cs` (nuevo)
- `src/SonrisaDigital.Services/Pagos/EstadoFinanciero.cs` (eliminado)
- `src/SonrisaDigital.Data/Repositories/IPacienteRepository.cs`
- `src/SonrisaDigital.Data/Repositories/PacienteRepository.cs`
- `src/SonrisaDigital.Data/Repositories/IOdontologoRepository.cs`
- `src/SonrisaDigital.Data/Repositories/OdontologoRepository.cs`
- `src/SonrisaDigital.Data/Repositories/ICitaRepository.cs`
- `src/SonrisaDigital.Data/Repositories/CitaRepository.cs`
- `src/SonrisaDigital.Data/Repositories/IPagoRepository.cs`
- `src/SonrisaDigital.Data/Repositories/PagoRepository.cs`
- `src/SonrisaDigital.Services/Dashboard/DashboardService.cs`
- `src/SonrisaDigital.Services/Pacientes/IPacienteService.cs`
- `src/SonrisaDigital.Services/Pacientes/PacienteService.cs`
- `src/SonrisaDigital.Services/Odontologos/IOdontologoService.cs`
- `src/SonrisaDigital.Services/Odontologos/OdontologoService.cs`
- `src/SonrisaDigital.Services/Citas/ICitaService.cs`
- `src/SonrisaDigital.Services/Citas/CitaService.cs`
- `src/SonrisaDigital.Services/Usuarios/IUsuarioService.cs`
- `src/SonrisaDigital.Services/Usuarios/UsuarioService.cs`
- `src/SonrisaDigital.Services/Pagos/IPagoService.cs`
- `src/SonrisaDigital.Services/Pagos/PagoService.cs`
- `src/SonrisaDigital.Services/Tratamientos/DTOs/TratamientoDtos.cs`
- `src/SonrisaDigital.Services/Tratamientos/TratamientoService.cs`
- `src/SonrisaDigital.UI/Components/EstadoFinancieroUi.cs`
- `src/SonrisaDigital.UI/Components/PaginationFooter.razor` (nuevo)
- `src/SonrisaDigital.UI/Components/PaginationFooter.razor.css` (nuevo)
- `src/SonrisaDigital.UI/Pages/Dashboard/Dashboard.razor`
- `src/SonrisaDigital.UI/Pages/Pacientes/Pacientes.razor`
- `src/SonrisaDigital.UI/Pages/Odontologos/Odontologos.razor`
- `src/SonrisaDigital.UI/Pages/Citas/Citas.razor`
- `src/SonrisaDigital.UI/Pages/Usuarios/Usuarios.razor`
- `src/SonrisaDigital.UI/Pages/Pagos/Pagos.razor`
- `src/SonrisaDigital.UI/_Imports.razor`

### Qué se cambió

1. **Dashboard con conteos en SQL**
   - `PacienteRepository.CountAsync` y `OdontologoRepository.CountAsync` cuentan directamente en la base de datos, con filtros opcionales por estado y fecha de registro.
   - `DashboardService` dejó de cargar todos los pacientes y odontólogos para contarlos en memoria.
   - La semana se calcula desde el lunes a las 00:00 en la zona horaria de la clínica y se convierte a UTC para compararla con `FechaRegistro`.
   - La pantalla del dashboard ahora usa `IDashboardService` y ya no inyecta los servicios de pacientes y odontólogos.

2. **Resumen de pagos con agregados**
   - `PagoRepository.GetTotalesAsync` calcula el total facturado, el total cobrado y la cantidad de tratamientos con saldo mediante `SUM` y `COUNT` en SQL.
   - `PagoService.GetResumenAsync` usa esos totales y conserva la misma fórmula de saldo pendiente.
   - Ya no se traen todos los tratamientos con todos sus pagos para sumarlos en memoria.

3. **Roles de usuarios sin N+1**
   - `UsuarioService.SearchPagedAsync` reemplazó las llamadas a `GetRolesAsync` dentro del bucle por una sola consulta a `UserRoles` y `Roles`, agrupada por usuario.
   - Los roles se consultan solo para los usuarios de la página actual.

4. **Permisos del dashboard**
   - El dashboard solo consulta citas si el usuario tiene `Citas.Ver`, y solo consulta pagos si tiene `Pagos.Ver`.
   - Las tarjetas y paneles correspondientes siguen ocultos mediante `AuthorizeView`.
   - Se evita que un rol sin `Pagos.Ver` provoque una excepción capturada en silencio.

5. **Paginación real en SQL**
   - Pacientes, Citas, Usuarios, Pagos y Odontólogos piden 10 filas por página; los servicios limitan el tamaño máximo a 50.
   - Cada listado hace `CountAsync` para conocer el total y después `Skip`/`Take` para traer solo la página.
   - El orden siempre incluye el `Id` para que la paginación sea determinista.
   - Si un filtro deja la página solicitada fuera de rango, el servicio la ajusta a la última página válida.
   - Al aplicar un filtro o una búsqueda se vuelve a la página 1.
   - `PaginationFooter` es un componente reutilizable que muestra el total, la página actual y los botones anterior/siguiente.
   - `PacienteService.SearchAsync`, `CitaService.SearchAsync` y `OdontologoService.SearchAsync` se conservan para los desplegables, el Dashboard y la ficha del paciente, que necesitan el conjunto completo.

6. **Filtro financiero de Pagos en SQL**
   - `EstadoFinanciero` se movió a `SonrisaDigital.Data.Entities` para que el repositorio pueda usarlo sin depender de la capa de servicios.
   - El filtro Pendiente / Pago parcial / Pagado se traduce a comparaciones y subconsultas sobre `Pagos`, sin cargar todos los tratamientos en memoria.
   - El filtro por paciente de `/pagos?paciente=12` también se aplica en SQL.

7. **Cargas serializadas por pantalla**
   - Las pantallas paginadas usan un `SemaphoreSlim` para atender una carga a la vez sobre el `DbContext` compartido del circuito.
   - Las escrituras de Usuarios y Pagos también esperan a que termine cualquier carga en curso antes de usar el contexto.

### Por qué

- Cargar listas completas solo para contar o sumar aumenta el uso de memoria y el tráfico de red a medida que crece la clínica.
- El N+1 de roles ejecutaba una consulta adicional por cada usuario del listado.
- El dashboard llamaba al resumen de pagos sin comprobar el permiso, aunque la tarjeta estuviera oculta.
- Sin paginación en SQL, la tabla de Pacientes descargaba todos los registros y los ordenaba en memoria; con muchos pacientes eso se vuelve lento y costoso.
- El estado financiero no es una columna de la base de datos. Filtrarlo en memoria obligaba a traer todos los tratamientos y todos sus pagos; ahora se calcula en la consulta.
- Serializar las cargas evita el error de EF Core cuando dos eventos de la interfaz intentan usar el mismo `DbContext` al mismo tiempo.

### Verificación

- `dotnet build SonrisaDigital.slnx` → 0 errores, 0 warnings.
- Dashboard contra SQL Server: 20 pacientes activos, 1 odontólogo activo y resumen de pagos coherente con la consulta directa.
- Consulta directa de control: facturado `RD$4,500`, cobrado `RD$4,500`, saldo `RD$0`, 0 tratamientos con saldo.
- La página de Pagos mostró los mismos totales.
- La lista de Usuarios siguió mostrando el rol `Administrador` del usuario inicial.
- Prueba de interfaz con Chrome headless y sesión real:
  - Pacientes: 20 activos en 2 páginas, 10 filas por página; la página 2 mostró filas distintas; el filtro por nombre devolvió 1 y reinició a la página 1.
  - Citas: el filtro por fecha `2026-10-22` devolvió 1 cita.
  - Usuarios: 3 usuarios; el filtro `admin` devolvió 1.
  - Pagos: 2 tratamientos; `Pago parcial` y `Pendiente` devolvieron 0; `Pagado` devolvió 2; el filtro por paciente 6 devolvió 1 y el paciente 5 devolvió 0.
  - Odontólogos: 1 activo.
- Prueba directa del repositorio contra SQL Server:
  - Pacientes: página 1 y página 2 con 10 filas; una página fuera de rango se ajustó a la página 2.
  - Citas, Odontólogos y Pagos devolvieron los conteos esperados.
  - Con datos temporales dentro de una transacción revertida: Pendiente 1, Pago parcial 1 y Pagado 1.
  - La transacción se revirtió y no quedaron tratamientos de prueba.
- Registro de pago con un tratamiento temporal de RD$1,000: se cobraron RD$400, la fila pasó a `Pago parcial`, el saldo quedó en RD$600 y la pantalla se recargó sin errores. El tratamiento y el pago temporales se eliminaron al terminar.
- Los logs no mostraron el error `A second operation was started on this context instance` después de serializar las cargas. El error se reprodujo antes del ajuste al cambiar filtros rápidamente en Pagos.
- Los 404 observados en Development corresponden a `/_framework/dotnet-browser-tools/hot-reload-settings.json`; no afectan a la aplicación.
- Este bloque no requiere migración de base de datos.

## Caché de catálogos

**Archivos modificados**
- `src/SonrisaDigital.Services/Catalogos/ICatalogoCache.cs` (nuevo)
- `src/SonrisaDigital.Services/Catalogos/CatalogoCache.cs` (nuevo)
- `src/SonrisaDigital.Services/DependencyInjection.cs`
- `src/SonrisaDigital.Services/Tratamientos/TratamientoService.cs`
- `src/SonrisaDigital.Services/Odontograma/OdontogramaService.cs`
- `src/SonrisaDigital.Services/Odontograma/OdontogramaCatalogSeeder.cs`
- `src/SonrisaDigital.Services/Autorizacion/RolService.cs`
- `src/SonrisaDigital.UI/Program.cs`

### Qué se cambió

1. **Caché en memoria compartida**
   - Se registró `IMemoryCache` y una instancia única de `ICatalogoCache` para todo el proceso.
   - La caché guarda únicamente catálogos globales: piezas dentales, tratamientos y la asignación de permisos de los roles.
   - Cada entrada expira a los 30 minutos como red de seguridad, además de la invalidación explícita.

2. **Piezas dentales**
   - `TratamientoService.GetPiezasAsync` y `OdontogramaService` comparten la misma lista de piezas.
   - El odontograma sigue consultando siempre los registros clínicos del paciente en la base de datos; solo reutiliza el catálogo fijo de piezas.
   - `OdontogramaCatalogSeeder` invalida la lista si agrega piezas nuevas.

3. **Tratamientos**
   - `GetTratamientosAsync` mantiene dos entradas separadas: tratamientos activos y todos los tratamientos.
   - Crear o editar un tratamiento invalida ambas listas después de que la transacción confirma.
   - Las altas y ediciones de tratamientos realizados no invalidan el catálogo, porque no modifican sus nombres, precios base ni estados.

4. **Permisos de los roles**
   - `RolService.GetRolesAsync` cachea los roles con sus permisos asignados.
   - Guardar permisos invalida esa entrada para que la pantalla muestre las casillas actualizadas.
   - La carga inicial de roles ahora resuelve los permisos de todos los roles en una sola consulta, en lugar de una consulta por rol.
   - El catálogo de definiciones (`PermissionCatalog`) ya vive en memoria y no necesita una consulta adicional.

5. **Invalidación segura**
   - Cada catálogo tiene una versión interna. Al invalidarlo se incrementa la versión y las lecturas siguientes usan una clave nueva.
   - Si una consulta lenta termina después de una edición, su resultado queda en una clave vieja que ya no se consulta y no reemplaza los datos actualizados.

6. **Datos que no se cachean**
   - No se cachean pacientes, citas, pagos, historias clínicas ni registros de odontograma.
   - Tampoco se cachean los permisos efectivos de la sesión: siguen calculándose desde los claims y el `SecurityStamp`, para que un cambio de rol invalide el acceso vigente.

### Por qué

- Los formularios de tratamientos y el odontograma consultaban los mismos catálogos una y otra vez.
- La pantalla de roles y el formulario de usuarios repetían la lectura de roles y permisos.
- Estos datos cambian poco, pero deben reflejar los cambios de inmediato; por eso se combina caché con invalidación explícita.
- Cachear datos clínicos o permisos efectivos sin un control de invalidación estricto podría mostrar información desactualizada o permitir accesos indebidos.

### Verificación

- `dotnet build SonrisaDigital.slnx` → 0 errores, 0 warnings.
- Prueba aislada de `CatalogoCache`:
  - Dos lecturas consecutivas de piezas reutilizan la misma lista y ejecutan una sola carga.
  - Tratamientos activos y todos los tratamientos usan entradas separadas.
  - Invalidar piezas, tratamientos o roles fuerza una nueva carga.
  - Una lectura lenta que termina después de una invalidación no reemplaza el resultado nuevo.
- Aplicación arrancada en `http://localhost:5164` con sesión real del administrador:
  - `/tratamientos` → `200`, con el catálogo visible.
  - `/roles` → `200`, con los roles y permisos visibles.
  - `/odontograma` → `200`.
  - Los logs no mostraron excepciones durante las consultas.
- Este bloque no requiere migración de base de datos.

## Pruebas, integración continua y respaldos

**Archivos nuevos**
- `tests/SonrisaDigital.Tests/SonrisaDigital.Tests.csproj`
- `tests/SonrisaDigital.Tests/Unit/*`
- `tests/SonrisaDigital.Tests/Integration/*`
- `tests/SonrisaDigital.Tests/Support/*`
- `.github/workflows/ci.yml`
- `.github/dependabot.yml`
- `global.json`
- `scripts/backup-sonrisadigital.ps1`
- `scripts/probar-restauracion-sonrisadigital.ps1`
- `scripts/verificar-cobertura.ps1`
- `docs/07-pruebas-y-ci.md` (fuera de Git)

**Archivos modificados**
- `SonrisaDigital.slnx`
- `.gitignore`

### Qué se cambió

1. **Proyecto de pruebas xUnit**
   - Se agregó `SonrisaDigital.Tests` con pruebas unitarias de validaciones, paginación y caché.
   - Las pruebas de integración usan SQL Server real, no un proveedor en memoria.
   - Cada clase de integración crea una base temporal, aplica las migraciones y la elimina al terminar.
   - Las pruebas se ejecutan en serie para que las bases temporales y las pruebas de concurrencia sean estables.

2. **Cobertura funcional**
   - Pacientes: conteo, paginación, filtros y página fuera de rango.
   - Citas: filtros, paginación, choque por odontólogo/paciente, canceladas y exclusión de la propia cita.
   - Pagos: filtros Pendiente/Parcial/Pagado, búsqueda y totales agregados.
   - Caché: reutilización, entradas separadas, invalidación y protección contra resultados viejos.
   - Servicios: validaciones de agenda, validaciones de pago, permisos y saldo pendiente.

3. **Concurrencia real**
   - `CitaConcurrencyTests` ejecuta dos reservas simultáneas del mismo horario y comprueba que solo quede una cita.
   - `PagoConcurrencyTests` ejecuta dos cobros simultáneos de RD$60 sobre un tratamiento de RD$100 y comprueba que solo quede un pago.
   - Las pruebas usan una barrera asíncrona para provocar la carrera y verificar el aislamiento `Serializable` y el bloqueo `UPDLOCK, HOLDLOCK`.

4. **Pipeline de GitHub Actions**
   - `build-test` restaura, compila en Release con `-warnaserror`, levanta SQL Server 2022 como servicio, ejecuta las pruebas y publica resultados y cobertura.
   - `publish` genera el artefacto de publicación solo en `push` a `main`, después de que `build-test` y `e2e` pasen.
   - La cobertura de líneas se valida con `scripts/verificar-cobertura.ps1`; el mínimo actual es 60%.

5. **Dependabot**
   - Revisa paquetes NuGet y acciones de GitHub cada lunes.
   - Agrupa las actualizaciones NuGet en un solo PR.

6. **Respaldos operativos**
   - `backup-sonrisadigital.ps1` ejecuta `BACKUP DATABASE`, `RESTORE VERIFYONLY`, calcula SHA256 y aplica retención.
   - `probar-restauracion-sonrisadigital.ps1` restaura el respaldo en una base temporal, ejecuta `DBCC CHECKDB` y elimina la base.
   - Si la carpeta de respaldos de la instancia está protegida, el respaldo se verifica igualmente y el script avisa que no pudo calcular hash ni retención. Para operación real se recomienda una carpeta compartida con permisos para SQL Server y para el operador.

7. **SDK fijado**
   - `global.json` fija .NET SDK 10.0.403 con `rollForward: latestFeature` para evitar que una versión preliminar de .NET 11 compile la solución por accidente.

### Por qué

- La validación manual no cubre carreras reales ni evita regresiones cuando se modifica la paginación o la caché.
- Un proveedor en memoria no reproduce bloqueos, transacciones ni el aislamiento de SQL Server.
- El pipeline debe fallar si aparece un warning, si una prueba crítica se rompe o si la cobertura baja del mínimo acordado.
- Un respaldo sin verificación y sin prueba de restauración no es un respaldo confiable.

### Verificación

- `dotnet build SonrisaDigital.slnx -c Release -warnaserror` → 0 errores, 0 warnings con SDK 10.0.403.
- `dotnet test` → 30 pruebas aprobadas, 0 fallidas, 0 omitidas.
- Cobertura de líneas: 65.5%; cobertura de ramas: 11.89%.
- Prueba de concurrencia de citas: una cita guardada de dos intentos simultáneos.
- Prueba de concurrencia de pagos: un pago de RD$60 guardado de dos intentos simultáneos; el tratamiento quedó en `EnProceso`.
- Las bases temporales de las pruebas se eliminaron al terminar; no quedaron bases `SonrisaDigital_Tests_*`.
- Respaldo y restauración probados con una base temporal: `RESTORE VERIFYONLY` válido, restauración correcta y `DBCC CHECKDB` sin errores. La base temporal se eliminó al finalizar.

## Pruebas E2E y concurrencia de Tratamientos

**Archivos nuevos**
- `tests/SonrisaDigital.E2E/package.json`
- `tests/SonrisaDigital.E2E/package-lock.json`
- `tests/SonrisaDigital.E2E/playwright.config.ts`
- `tests/SonrisaDigital.E2E/tests/flujo-operativo.spec.ts`
- `scripts/ejecutar-e2e-local.ps1`
- `.config/dotnet-tools.json`

**Archivos modificados**
- `.github/workflows/ci.yml`
- `.gitignore`
- `src/SonrisaDigital.UI/Pages/Tratamientos.razor`

### Qué se cambió

1. **Flujo E2E con Playwright**
   - Recorre login, alta de odontólogo, alta de paciente, cita, alta de tratamiento en catálogo, tratamiento realizado, pago parcial y detalle de pago.
   - Espera la señal `Blazor.reconnect` antes de escribir en cada formulario, porque el HTML se muestra antes de que el circuito interactivo esté conectado.
   - Usa un sufijo único por ejecución y un solo trabajador para evitar datos duplicados.

2. **Runner local aislado**
   - `ejecutar-e2e-local.ps1` crea una base temporal, aplica las migraciones reales, arranca la aplicación con un administrador temporal, espera `/health/ready`, ejecuta Playwright y elimina la base al terminar.
   - Acepta `-Configuration Release`, `-Headed` y `-KeepDatabase`.
   - No escribe contraseñas en archivos: las pasa por variables de entorno.
   - Usa `localhost` en la URL porque `AllowedHosts` solo permite ese host.

3. **Job E2E en el pipeline**
   - Se agregó el job `e2e`, que levanta SQL Server 2022 como servicio, compila en Release, aplica migraciones, arranca la aplicación y ejecuta Playwright.
   - `publish` ahora depende de `build-test` y `e2e`.
   - `.config/dotnet-tools.json` fija `dotnet-ef` en 10.0.13.

4. **Corrección de concurrencia en Tratamientos**
   - La prueba E2E reprodujo `A second operation was started on this context` al cambiar de pestaña mientras una tabla seguía cargando.
   - `Tratamientos.razor` ahora usa un `SemaphoreSlim` para serializar las cargas de catálogo, realizados y opciones de formulario sobre el `ApplicationDbContext` compartido.
   - El patrón es el mismo que ya usaba la pantalla de Pagos.

### Por qué

- Las pruebas de servicios no cubren la hidratación de Blazor, la navegación real ni los selectores del formulario.
- La prueba E2E encontró una carrera que no aparecía en las pruebas unitarias ni en la revisión manual.
- El pipeline debe validar el flujo completo antes de publicar un artefacto.

### Verificación

- `dotnet build SonrisaDigital.slnx -c Release -warnaserror` → 0 errores, 0 warnings.
- `dotnet test` → 30 pruebas aprobadas, 0 fallidas.
- E2E en `Debug` y `Release` → 1 prueba aprobada en cada configuración.
- La base temporal se eliminó y no quedaron procesos de la aplicación activos.
- La migración `20261025015324_AddCitaPacienteFechaIndex` está aplicada en la base local; el índice quedó como `NONCLUSTERED`.
- PR #5: `build-test` y `e2e` en verde; `publish` se omitió porque solo corre en `main`.
- Workflow de `main` (commit `372b939`, run `40262492849`): `build-test`, `e2e` y `publish` en verde; artefacto `sonrisadigital-publicacion` publicado.

## Cierre técnico 6A: retiro de restos del login demo

**Archivos modificados**
- `src/SonrisaDigital.UI/Pages/Login/Login.razor`
- `src/SonrisaDigital.UI/Pages/Login/Login.razor.css`
- `src/SonrisaDigital.Services/Autenticacion/IdentitySeeder.cs`
- `README.md`
- `docs/06-despliegue.md`
- `docs/07-pruebas-y-ci.md`
- `docs/08-puesta-en-produccion.md`

**Archivo local no versionado**
- `src/SonrisaDigital.UI/appsettings.Development.json`: se retiraron el correo y la contraseña de demostración. El archivo sigue ignorado por Git.

### Qué se cambió

1. **Limpieza del login**
   - Se eliminó el modelo `LoginModel`, que ya no se usaba después del retiro del bloque demo.
   - Se eliminaron los estilos `.login-demo` de `Login.razor.css`.
   - `/login` no muestra credenciales de prueba ni textos asociados a la demostración.

2. **Seeding sin credenciales por defecto**
   - `IdentitySeeder` ahora exige `Authentication:AdminEmail` y `Authentication:AdminPassword`.
   - Se eliminó el correo predeterminado `admin@sonrisadigital.com`.
   - Si falta cualquiera de las dos claves, no se crea el administrador y se registra una advertencia.

3. **Configuración local**
   - Se retiraron las credenciales demo de `appsettings.Development.json`.
   - El administrador local se configura con user-secrets o variables de entorno, como indica el README.

4. **Contraseñas de usuarios**
   - El producto no genera contraseñas automáticamente.
   - El administrador asigna la contraseña inicial y puede restablecerla desde **Usuarios**.
   - La contraseña temporal del runner E2E es exclusiva de las pruebas y no se publica.

5. **Documentación**
   - Se actualizaron README, despliegue, pruebas y el plan de puesta en producción para reflejar que el login demo ya fue retirado.

### Verificación

- `dotnet build SonrisaDigital.slnx -c Release -warnaserror` → 0 errores, 0 warnings.
- `dotnet test SonrisaDigital.slnx -c Release --no-build` → 30 pruebas aprobadas, 0 fallidas.
- E2E local en Release → 1 prueba aprobada; base temporal eliminada al terminar.
- Búsqueda de `login-demo`, `SonrisaDigitalAdmin123`, `LoginModel` y `admin@sonrisadigital.com` en código y configuración versionados, excluyendo este registro → sin coincidencias.
- PR #7: `build-test` y `e2e` en verde; `publish` se omitió porque solo corre en `main`.
- El PR #7 incluyó el commit `d64dbd1` de ajustes del Centro de Ayuda, equivalente al commit local `58097c2`.
- Workflow de `main` (commit `02f3db2`, run `40267116217`): `build-test`, `e2e` y `publish` en verde; artefacto `sonrisadigital-publicacion` publicado.

## Cierre técnico 6A: configuración de producción y verificación

**Archivos modificados**
- `src/SonrisaDigital.Services/Configuracion/ValidadorConfiguracionProduccion.cs`
- `src/SonrisaDigital.Data/ApplicationDbContextFactory.cs`
- `src/SonrisaDigital.UI/Program.cs`
- `src/SonrisaDigital.UI/SonrisaDigital.UI.csproj`
- `src/SonrisaDigital.UI/appsettings.json`
- `src/SonrisaDigital.UI/appsettings.Production.json`
- `src/SonrisaDigital.UI/appsettings.Development.example.json`
- `tests/SonrisaDigital.Tests/Unit/ValidadorConfiguracionProduccionTests.cs`
- `scripts/generar-script-migraciones.ps1`
- `scripts/verificar-produccion.ps1`
- `scripts/probar-restauracion-sonrisadigital.ps1`
- `.github/workflows/ci.yml`
- `.gitignore`
- `README.md`
- `docs/06-despliegue.md`
- `docs/07-pruebas-y-ci.md`
- `docs/08-puesta-en-produccion.md`

### Qué se cambió

1. **Validación de configuración de producción**
   - `ValidadorConfiguracionProduccion` revisa `AllowedHosts`, la cadena de conexión, `ReverseProxy` y `DataProtection:KeysPath`.
   - Fuera de Development, la aplicación no arranca si falta un valor obligatorio o si `AllowedHosts` conserva el marcador de ejemplo.
   - En Development no se aplica para no afectar el runner E2E.

2. **Persistencia de Data Protection**
   - `Program.cs` configura `PersistKeysToFileSystem` cuando existe `DataProtection:KeysPath`.
   - La carpeta se crea al arrancar; la cuenta de servicio debe tener permiso de escritura.
   - `appsettings.json` y `appsettings.Production.json` incluyen la sección vacía para que el operador la complete.

3. **Migraciones sin ejecutar Program.cs**
   - `ApplicationDbContextFactory` permite que `dotnet ef` use el contexto sin exigir toda la configuración web de producción.
   - La cadena se toma de `ConnectionStrings__DefaultConnection`; si no existe, usa la base local de desarrollo.

4. **Publicación sin configuración local**
   - `appsettings.Development.json` y su plantilla se excluyeron de `dotnet publish`.
   - El job `publish` verifica que el artefacto no contenga esos archivos.

5. **Scripts operativos**
   - `generar-script-migraciones.ps1` genera el SQL idempotente en `artefactos/`, que está ignorado por Git.
   - `verificar-produccion.ps1` revisa salud, base, login, restos demo, cabeceras y HSTS.
   - `probar-restauracion-sonrisadigital.ps1` ya no requiere permiso de lectura sobre la carpeta protegida de respaldos de SQL Server.

### Verificación

- `dotnet build SonrisaDigital.slnx -c Release -warnaserror` → 0 errores, 0 warnings.
- `dotnet test` → 35 pruebas aprobadas, 0 fallidas; cobertura de líneas 65.55%, ramas 14.68%.
- E2E local en Release → 1 prueba aprobada; base temporal eliminada.
- `RESTORE VERIFYONLY` y restauración temporal con `DBCC CHECKDB` → sin errores.
- Script de migraciones aplicado sobre una copia restaurada → cinco migraciones registradas y `IX_Citas_PacienteId_Fecha_HoraInicio` como `NONCLUSTERED`.
- `dotnet publish` → solo `appsettings.json` y `appsettings.Production.json`; sin archivos de Development.
- Arranque con `ASPNETCORE_ENVIRONMENT=Production` y configuración incompleta → falla con errores claros, como se espera.
- Verificación de humo local con `scripts/verificar-produccion.ps1` → salud, base, login y cabeceras correctas.
- `dotnet list package --vulnerable --include-transitive` → sin paquetes vulnerables.

## Pendiente

- Descargar y revisar el artefacto `sonrisadigital-publicacion` generado por el workflow `40267116217`.
- Aplicar la migración `20261025015324_AddCitaPacienteFechaIndex` en el servidor de producción.
- Elevar la cobertura de ramas en los servicios con más flujos de negocio.
- Ampliar las pruebas E2E a historia clínica, odontograma y administración de permisos.
- Probar el respaldo y la restauración en el servidor de producción o en una copia aislada.
- Mantener el login sin credenciales demo en cada publicación.
- Mantener la documentación actualizada y versionada con cada cambio.
- Mantener la regla de Pull Request y pipeline en verde para los cambios futuros.
