# Pruebas, integración continua y respaldos

Esta guía describe cómo se valida SonrisaDigital antes de publicar una versión: pruebas unitarias, pruebas de integración contra SQL Server, pruebas de concurrencia, pruebas E2E con Playwright, cobertura, pipeline de GitHub Actions y respaldos verificables. El cierre técnico, el despliegue y la aceptación con la clínica se detallan en [Puesta en producción y aceptación](08-puesta-en-produccion.md).

> **Ámbito:** proyectos `SonrisaDigital.Tests` y `SonrisaDigital.E2E`, SQL Server 2022, .NET 10, Node.js y GitHub Actions.
>
> Las pruebas de integración crean una base temporal por clase, aplican las migraciones reales y la eliminan al terminar. Las pruebas E2E usan una base temporal independiente y un administrador generado en tiempo de ejecución. No usan la base de desarrollo ni la de producción.

---

## 1. Requisitos

### Desarrollo local

- .NET SDK 10.0.403 o una versión 10.0.4xx compatible (`global.json`).
- SQL Server Express, Developer o Standard en ejecución.
- `sqlcmd` disponible para los scripts de respaldo.
- Permiso para crear y eliminar bases de datos temporales.
- Node.js 20 o superior y npm para las pruebas E2E.
- Navegador Chromium de Playwright: `npx playwright install chromium`.

La conexión local por defecto es:

```text
Server=.\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

Si la instancia tiene otro nombre, definir la variable:

```powershell
$env:SONRISADIGITAL_TEST_CONNECTION = "Server=MI_SERVIDOR;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

La base indicada en `Database` se reemplaza por una base temporal con nombre único. La conexión debe tener permiso para `CREATE DATABASE` y `DROP DATABASE`.

### Integración continua

GitHub Actions levanta un contenedor de SQL Server 2022 y define `SONRISADIGITAL_TEST_CONNECTION` con el usuario `sa`. No se necesitan secretos del repositorio: las credenciales son de prueba y solo existen dentro del job.

---

## 2. Proyecto de pruebas

```text
tests/SonrisaDigital.Tests/
├── Unit/
│   ├── CatalogoCacheTests.cs
│   ├── PagedResultTests.cs
│   ├── CitaServiceValidationTests.cs
│   └── PagoServiceTests.cs
├── Integration/
│   ├── SqlServerFixture.cs
│   ├── RepositoryPaginationTests.cs
│   ├── CitaConcurrencyTests.cs
│   └── PagoConcurrencyTests.cs
├── Support/
│   ├── TestData.cs
│   └── TestDoubles.cs
└── AssemblyInfo.cs

tests/SonrisaDigital.E2E/
├── tests/
│   └── flujo-operativo.spec.ts
├── playwright.config.ts
├── package.json
└── package-lock.json
```

Las pruebas de xUnit se ejecutan en serie (`DisableTestParallelization`) porque comparten SQL Server y crean y eliminan bases. Esto evita carreras entre pruebas y resultados intermitentes. Las pruebas E2E se ejecutan con un solo trabajador de Playwright.

### 2.1. Pruebas unitarias

| Archivo | Qué comprueba |
|---|---|
| `CatalogoCacheTests` | Reutilización de caché, separación de tratamientos activos/todos, invalidación y protección contra resultados viejos. |
| `PagedResultTests` | Total de páginas y botones anterior/siguiente. |
| `CitaServiceValidationTests` | Paciente y odontólogo obligatorios, fecha, horas, fin de semana, horario de atención, almuerzo y permiso. |
| `PagoServiceTests` | Tratamiento, monto, fecha, método de pago, permiso y cálculo del saldo pendiente. |

### 2.2. Pruebas de integración

| Archivo | Qué comprueba |
|---|---|
| `PacienteRepositoryPaginationTests` | `CountAsync`, `Skip`/`Take`, filtros por texto/estado y corrección de una página fuera de rango. |
| `CitaRepositoryTests` | Paginación por fecha, paciente, odontólogo y estado; detección de choque por odontólogo o paciente; exclusión de canceladas y de la propia cita. |
| `PagoRepositoryTests` | Filtros Pendiente, Pago parcial y Pagado calculados en SQL; búsqueda por texto; totales facturados, cobrados y tratamientos con saldo. |
| `CitaConcurrencyTests` | Dos reservas simultáneas del mismo horario: solo una se guarda. |
| `PagoConcurrencyTests` | Dos cobros simultáneos de RD$60 sobre un tratamiento de RD$100: solo uno se guarda. |

### 2.3. Pruebas de concurrencia

Las pruebas usan una barrera asíncrona para que las dos operaciones lleguen al punto crítico antes de continuar:

- **Citas:** ambas transacciones leen el horario y después intentan insertar. La transacción `Serializable` y el índice de agenda impiden duplicar la reserva.
- **Pagos:** ambas transacciones esperan antes de pedir el bloqueo `UPDLOCK, HOLDLOCK`. Una obtiene el bloqueo, registra el pago y confirma; la segunda lee el saldo actualizado y rechaza el cobro.

No se usa un proveedor en memoria porque no reproduce bloqueos, aislamiento ni transacciones de SQL Server.

### 2.4. Pruebas E2E con Playwright

El proyecto `tests/SonrisaDigital.E2E` recorre el flujo operativo principal en un navegador real:

1. Inicio de sesión con el administrador temporal.
2. Alta de un odontólogo.
3. Alta de un paciente.
4. Creación de una cita en el siguiente día laborable.
5. Alta de un tratamiento en el catálogo.
6. Registro de ese tratamiento como realizado.
7. Registro de un pago parcial.
8. Consulta del detalle de pago.

La prueba usa un sufijo único por ejecución para evitar choques si la base no se elimina. Antes de escribir en cada formulario espera la señal `Blazor.reconnect`, porque el HTML se muestra antes de que el circuito interactivo esté conectado. Sin esa espera, los campos aceptan texto pero el modelo del servidor permanece vacío.

El runner local es `scripts/ejecutar-e2e-local.ps1`:

- Crea una base temporal con nombre único.
- Aplica las migraciones reales.
- Genera un administrador temporal.
- Arranca la aplicación en un puerto libre.
- Espera a que `/health/ready` indique base de datos disponible.
- Ejecuta Playwright.
- Detiene la aplicación y elimina la base, salvo que se use `-KeepDatabase`.

La contraseña del administrador temporal no se guarda en archivos: el runner la genera y la pasa por variables de entorno al proceso de Playwright.

---

## 3. Ejecución local

Restaurar y compilar:

```powershell
dotnet restore SonrisaDigital.slnx
dotnet build SonrisaDigital.slnx -c Release -warnaserror
```

Ejecutar todas las pruebas:

```powershell
dotnet test tests/SonrisaDigital.Tests/SonrisaDigital.Tests.csproj -c Release --no-build
```

Ejecutar con cobertura:

```powershell
dotnet test tests/SonrisaDigital.Tests/SonrisaDigital.Tests.csproj `
  -c Release `
  --no-build `
  --results-directory tests/SonrisaDigital.Tests/TestResults `
  --logger "trx;LogFileName=test-results.trx" `
  --collect:"XPlat Code Coverage"
```

Verificar el umbral:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verificar-cobertura.ps1 `
  -RutaResultados tests/SonrisaDigital.Tests/TestResults `
  -MinimoLinea 0.60
```

El umbral actual es **60% de líneas**. En la última ejecución local se obtuvo:

- 30 pruebas aprobadas, 0 fallidas.
- Cobertura de líneas: **65.5%**.
- Cobertura de ramas: **11.89%**.
- Duración aproximada: 7 segundos.

La cobertura de ramas todavía es baja porque los servicios incluyen muchas pantallas y flujos auxiliares. El umbral de líneas evita que una versión nueva elimine pruebas críticas sin que el pipeline lo detecte.

### 3.1. Pruebas E2E

Instalar dependencias y el navegador una sola vez:

```powershell
cd tests/SonrisaDigital.E2E
npm ci
npm run install:browsers
cd ../..
```

Ejecutar el flujo completo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ejecutar-e2e-local.ps1
```

El runner acepta estas opciones:

| Opción | Uso |
|---|---|
| `-Configuration Release` | Compila y ejecuta con la misma configuración del pipeline. |
| `-Headed` | Muestra el navegador durante la prueba. |
| `-KeepDatabase` | Conserva la base temporal para inspeccionarla. |

El runner restaura `dotnet-ef` desde `.config/dotnet-tools.json` (versión 10.0.13), crea una base temporal, aplica las migraciones reales, arranca la aplicación con un administrador generado en memoria y espera a que `/health/ready` indique que la base responde. La contraseña temporal no se escribe en archivos: se pasa por variables de entorno al proceso de Playwright.

La prueba espera la señal `Blazor.reconnect` después de cada navegación a una página interactiva. El HTML se muestra antes de que el circuito de Blazor esté conectado; sin esa espera, los campos aceptan texto pero el modelo del servidor queda vacío y el formulario no se guarda.

La prueba E2E encontró una condición de carrera real en `/tratamientos`: al cambiar de pestaña mientras una tabla seguía cargando, dos consultas usaban el mismo `ApplicationDbContext` y EF Core lanzaba `A second operation was started on this context`. La página ahora serializa las cargas y la lectura de opciones con un `SemaphoreSlim`, igual que la pantalla de Pagos.

La última ejecución local terminó con **1 prueba E2E aprobada** tanto en `Debug` como en `Release`. Al finalizar, el runner detuvo la aplicación y eliminó la base temporal.

---

## 4. Pipeline de GitHub Actions

Archivo: `.github/workflows/ci.yml`.

### 4.1. Job `build-test`

1. Descarga el código.
2. Instala .NET 10.
3. Restaura la solución.
4. Compila en Release con `-warnaserror`.
5. Levanta SQL Server 2022 como servicio.
6. Ejecuta las 30 pruebas con cobertura.
7. Verifica el umbral de cobertura.
8. Publica `test-results.trx` y `coverage.cobertura.xml` como artefacto, incluso si las pruebas fallan.

### 4.2. Job `e2e`

Se ejecuta después de `build-test` y levanta su propio servicio de SQL Server 2022:

1. Instala .NET 10 y Node.js 22.
2. Restaura las herramientas locales de .NET (`dotnet-ef`).
3. Instala dependencias con `npm ci` y descarga Chromium.
4. Compila la solución en Release.
5. Crea la base `SonrisaDigitalE2E` y aplica las migraciones.
6. Arranca la aplicación con un administrador temporal y espera `/health/ready`.
7. Ejecuta Playwright.
8. Detiene la aplicación y elimina la base, incluso si la prueba falla.

La contraseña del administrador E2E es de prueba y solo existe dentro del job. No se necesitan secretos del repositorio.

El PR #5 validó `build-test` y `e2e`. Al fusionar, el workflow de `main` ejecutó también `publish` y terminó en verde; el artefacto `sonrisadigital-publicacion` quedó publicado.

### 4.3. Job `publish`

Se ejecuta solo en `push` a `main`, después de que `build-test` y `e2e` terminen bien:

1. Restaura dependencias.
2. Publica `SonrisaDigital.UI` en Release.
3. Guarda la carpeta `publish/` como artefacto descargable.

El pipeline no despliega automáticamente en un servidor. La publicación queda como artefacto para revisión y despliegue manual.

### 4.4. Dependabot

Archivo: `.github/dependabot.yml`.

- Revisa paquetes NuGet cada lunes.
- Revisa acciones de GitHub cada lunes.
- Agrupa las actualizaciones de NuGet en un solo PR para reducir ruido.

---

## 5. Respaldos

### 5.1. Respaldo completo

Archivo: `scripts/backup-sonrisadigital.ps1`.

Ejemplo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/backup-sonrisadigital.ps1 `
  -Server ".\SQLEXPRESS" `
  -Database "SonrisaDigital" `
  -BackupDirectory "D:\Backups\SonrisaDigital" `
  -RetentionDays 30
```

El script:

1. Comprueba que la base existe.
2. Ejecuta `BACKUP DATABASE` con `CHECKSUM`.
3. Ejecuta `RESTORE VERIFYONLY`.
4. Calcula SHA256 y guarda un archivo `.sha256`.
5. Elimina respaldos más antiguos que la retención indicada.

Si no se indica `-BackupDirectory`, usa la carpeta de respaldos de la instancia de SQL Server. Esa carpeta suele estar protegida: la cuenta que ejecuta el script puede no tener permiso de lectura. En ese caso el respaldo y la verificación se completan, pero el script avisa que no pudo calcular SHA256 ni aplicar retención. Para operación real se recomienda una carpeta compartida con:

- Permiso de escritura para la cuenta de servicio de SQL Server.
- Permiso de lectura para el operador que copia el respaldo fuera del servidor.
- Permiso de escritura para el operador que aplica la retención.

El script no recibe contraseñas por parámetro. Si se usa autenticación SQL, la contraseña se toma de `SQLCMDPASSWORD`.

### 5.2. Prueba de restauración

Archivo: `scripts/probar-restauracion-sonrisadigital.ps1`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/probar-restauracion-sonrisadigital.ps1 `
  -Server ".\SQLEXPRESS" `
  -BackupFile "D:\Backups\SonrisaDigital\SonrisaDigital_FULL_20261025-211926.bak"
```

El script:

1. Lee la lista de archivos lógicos del respaldo.
2. Restaura en una base temporal con nombre único.
3. Mueve los archivos `.mdf`, `.ndf` y `.ldf` a la carpeta de datos de la instancia.
4. Ejecuta `DBCC CHECKDB`.
5. Elimina la base temporal al terminar, incluso si la restauración falla.

Esta prueba se ejecutó localmente con una base temporal y confirmó `DBCC CHECKDB sin errores`. No se dejó ninguna base de prueba en la instancia.

### 5.3. Retención

La retención del script aplica a los archivos con el patrón `SonrisaDigital_FULL_*.bak` dentro de la carpeta indicada. Una política mínima:

| Tipo | Frecuencia | Retención |
|---|---|---|
| Completo diario | Diario | 30 días |
| Completo semanal | Semanal | 12 semanas |
| Completo mensual | Mensual | 12 meses |
| Log | Cada 15–30 minutos | 7–14 días |

Mantener al menos una copia fuera del servidor de producción. Los respaldos contienen datos clínicos y financieros: cifrarlos o restringir su acceso según la normativa aplicable.

---

## 6. Pendientes

- Descargar y revisar el artefacto `sonrisadigital-publicacion` generado por el pipeline. La primera ejecución ya confirmó el servicio de SQL Server, las bases temporales, el job E2E y la publicación del artefacto.
- Aplicar la migración `20261025015324_AddCitaPacienteFechaIndex` en el servidor de producción. Ya está aplicada en la base local `SonrisaDigital` y en las bases temporales de pruebas.
- Revisar la cobertura de ramas y elevarla en los servicios con más flujos de negocio.
- Ampliar las pruebas E2E a historia clínica, odontograma y administración de permisos.
- Probar el respaldo y la restauración en el servidor de producción o en una copia aislada antes de la salida comercial.

---

## 7. Diagnóstico rápido

### SQL Server no está disponible

```powershell
Get-Service 'MSSQL$SQLEXPRESS'
sqlcmd -S ".\SQLEXPRESS" -E -C -Q "SELECT @@VERSION"
```

### La prueba no puede crear la base

Verificar que la conexión de `SONRISADIGITAL_TEST_CONNECTION` tenga permiso para crear bases. En SQL Server Express, la cuenta de Windows que ejecuta las pruebas suele ser administradora de la instancia.

### El pipeline falla en cobertura

Revisar el archivo `coverage.cobertura.xml` publicado como artefacto. El umbral está en el paso `Verificar cobertura mínima`.

### Playwright no encuentra el navegador

Instalar Chromium dentro del proyecto E2E:

```powershell
cd tests/SonrisaDigital.E2E
npm run install:browsers
```

### La aplicación no responde durante las pruebas E2E

Revisar `tests/SonrisaDigital.E2E/test-results/app.out.log` y `app.err.log`. El runner imprime las últimas líneas cuando `/health/ready` no responde a tiempo.

### El respaldo no se puede leer

Es habitual si se usó la carpeta de respaldos de la instancia. Usar `-BackupDirectory` con una carpeta compartida y conceder permisos a `NT Service\MSSQL$SQLEXPRESS` o a la cuenta de servicio correspondiente.

### La prueba E2E llena campos pero no guarda

Verificar que la espera de `Blazor.reconnect` siga funcionando. Si el formulario se llena antes de que el circuito interactivo esté conectado, el HTML muestra el texto pero el modelo del servidor queda vacío. La prueba debe esperar esa señal después de cada navegación a una página interactiva.

### El job E2E no conecta con SQL Server

Revisar el log del servicio `sqlserver` y confirmar que la cadena use `Server=localhost,1433`, el usuario `sa` y `TrustServerCertificate=True`. El servicio tarda unos segundos en aceptar conexiones; el pipeline espera a que `/health/ready` responda antes de ejecutar Playwright.
