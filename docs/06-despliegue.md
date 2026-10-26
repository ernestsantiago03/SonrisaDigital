# Despliegue y operación en producción

Esta guía describe cómo publicar SonrisaDigital en un servidor real, configurar la base de datos, proteger la instalación con HTTPS y operar el sistema con respaldos y procedimientos de actualización.

> **Ámbito:** aplicación ASP.NET Core 10, Blazor Server, Entity Framework Core y SQL Server.
>
> La aplicación ya incluye las defensas de red y sesión descritas en esta guía: ForwardedHeaders, HSTS, redirección HTTPS, health checks, reintentos de base de datos, descarga de PDF por HTTP y retención de circuitos. Esta guía explica cómo operarlas en un despliegue real.
>
> No cubre el plan multiclínica. El login de demostración fue retirado de la interfaz y no se publica en producción.

---

## 1. Arquitectura recomendada

Para una clínica, la instalación más simple y mantenible es:

```text
Navegador
   │
   │ HTTPS 443
   ▼
IIS o nginx  ──────────────►  Aplicación ASP.NET Core
   │                              │
   │ HTTP interno                 │ EF Core
   ▼                              ▼
Kestrel / ANCM                SQL Server
```

Componentes:

1. **Proxy HTTPS:** IIS en Windows o nginx en Linux. Termina TLS, redirige HTTP a HTTPS y permite WebSocket.
2. **Aplicación:** un único proceso de SonrisaDigital. Blazor Server mantiene los circuitos en memoria.
3. **Base de datos:** SQL Server, preferiblemente en un servidor separado o en una instancia administrada.
4. **Respaldos:** SQL Server y una copia fuera del servidor de producción.

### Decisión importante: una instancia por clínica

Blazor Server guarda el estado de cada circuito en la memoria del proceso. Por eso:

- Un **recycle** del pool de IIS o un reinicio del servicio **corta las sesiones abiertas**. El modal de reconexión ayuda con caídas de red, pero no recupera el estado perdido por un reinicio del proceso.
- Si se ejecutan **varias instancias** detrás de un balanceador, se necesitan **sesiones persistentes** (sticky sessions) y un **key ring de Data Protection compartido**. Sin eso, el usuario puede rebotar entre instancias y perder el circuito.
- Para una clínica, la opción recomendada es **una sola instancia** con mantenimiento programado fuera del horario de atención.

---

## 2. Requisitos del servidor

### Windows + IIS

- Windows Server 2022 o superior.
- **.NET 10 Hosting Bundle** (incluye el ASP.NET Core Runtime y el módulo ANCM).
- IIS con los módulos:
  - Static Content
  - Default Document
  - HTTP Errors
  - **WebSocket Protocol**
  - Application Initialization (opcional, para precalentar la aplicación)
- Certificado TLS válido para el dominio.
- Acceso a SQL Server.
- Puerto 443 abierto; puerto 80 solo si se desea redirigir a HTTPS.

### Linux + nginx

- Distribución soportada por .NET 10 (Ubuntu, Debian, RHEL, etc.).
- **ASP.NET Core Runtime 10** (no hace falta el SDK completo en producción).
- nginx.
- Certificado TLS (por ejemplo, con Certbot).
- Acceso a SQL Server.
- Puertos 80 y 443 abiertos; Kestrel escuchando solo en loopback.

### Equipo de publicación

- .NET SDK 10.
- Acceso al repositorio.
- Herramienta `dotnet-ef` si se aplican migraciones desde ese equipo.

---

## 3. Variables de entorno y configuración

### 3.1. Regla general

La configuración de producción se suministra por **variables de entorno** o por un almacén de secretos. No se guardan contraseñas, cadenas de conexión reales ni certificados privados en el repositorio.

ASP.NET Core traduce `__` a `:` en la jerarquía de configuración:

| Variable | Equivalente en configuración | Obligatoria | Contenido |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | — | Sí | `Production` |
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | Sí | Cadena de conexión a SQL Server |
| `Authentication__AdminEmail` | `Authentication:AdminEmail` | Obligatoria para crear el administrador | Correo del administrador inicial |
| `Authentication__AdminPassword` | `Authentication:AdminPassword` | Obligatoria para crear el administrador | Contraseña inicial del administrador |
| `AllowedHosts` | `AllowedHosts` | Sí | Dominio o dominios separados por `;` |
| `DataProtection__KeysPath` | `DataProtection:KeysPath` | Sí en producción | Carpeta persistente y respaldada para las claves de Data Protection |
| `ReverseProxy__ForwardLimit` | `ReverseProxy:ForwardLimit` | No | Número de proxies de confianza; por defecto `1` |
| `ReverseProxy__KnownProxies__0` | `ReverseProxy:KnownProxies[0]` | Solo si el proxy no es loopback | IP del proxy |
| `Logging__LogLevel__Default` | `Logging:LogLevel:Default` | No | Nivel de log global |
| `Logging__LogLevel__SonrisaDigital` | `Logging:LogLevel:SonrisaDigital` | No | Nivel de log de la aplicación |

### 3.2. Ejemplo de configuración en Windows

Ejecutar en PowerShell **como administrador**. Los valores son ejemplos: reemplazar dominio, servidor y credenciales.

```powershell
[Environment]::SetEnvironmentVariable(
    "ASPNETCORE_ENVIRONMENT",
    "Production",
    "Machine")

[Environment]::SetEnvironmentVariable(
    "AllowedHosts",
    "clinica.ejemplo.com;www.clinica.ejemplo.com",
    "Machine")

[Environment]::SetEnvironmentVariable(
    "ConnectionStrings__DefaultConnection",
    "Server=sql01.ejemplo.local,1433;Database=SonrisaDigital;User Id=sonrisa_app;Password=CAMBIA_ESTA_CLAVE;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;",
    "Machine")

[Environment]::SetEnvironmentVariable(
    "Authentication__AdminEmail",
    "admin@clinica.ejemplo.com",
    "Machine")

[Environment]::SetEnvironmentVariable(
    "Authentication__AdminPassword",
    "CAMBIA_ESTA_CLAVE_INICIAL",
    "Machine")

[Environment]::SetEnvironmentVariable(
    "DataProtection__KeysPath",
    "C:\Sites\SonrisaDigital\data-protection-keys",
    "Machine")
```

Crear la carpeta de claves antes de arrancar y dar permiso de escritura a la identidad del pool:

```powershell
New-Item -ItemType Directory -Path "C:\Sites\SonrisaDigital\data-protection-keys" -Force
icacls "C:\Sites\SonrisaDigital\data-protection-keys" /grant "IIS APPPOOL\SonrisaDigital:(OI)(CI)(M)" /T
```

Después de cambiar variables de máquina, **reciclar el pool de IIS** o reiniciar el servicio para que el proceso las lea.

> Las variables de máquina son legibles por administradores. Para un entorno más exigente, usar un almacén de secretos o una cuenta de servicio con permisos restringidos. No dejar contraseñas en scripts versionados ni en el historial de PowerShell.

### 3.3. Ejemplo de configuración en Linux

Archivo `/etc/sonrisadigital/sonrisa.env`, con permisos `600` y propietario de la cuenta de servicio:

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
AllowedHosts=clinica.ejemplo.com;www.clinica.ejemplo.com
ConnectionStrings__DefaultConnection=Server=sql01.ejemplo.local,1433;Database=SonrisaDigital;User Id=sonrisa_app;Password=CAMBIA_ESTA_CLAVE;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;
Authentication__AdminEmail=admin@clinica.ejemplo.com
Authentication__AdminPassword=CAMBIA_ESTA_CLAVE_INICIAL
DataProtection__KeysPath=/var/lib/sonrisadigital/data-protection-keys
ReverseProxy__ForwardLimit=1
```

```bash
sudo chown root:sonrisa /etc/sonrisadigital/sonrisa.env
sudo chmod 600 /etc/sonrisadigital/sonrisa.env
```

### 3.4. `appsettings.Production.json`

El archivo `src/SonrisaDigital.UI/appsettings.Production.json` está versionado, pero **no contiene secretos**. Sus valores son marcadores:

- `ConnectionStrings:DefaultConnection` está vacío.
- `AllowedHosts` contiene `REEMPLAZAR_CON_DOMINIO_REAL`.
- `DataProtection:KeysPath` está vacío.
- `ReverseProxy:KnownProxies` está vacío.
- El logging baja a `Warning` y `SonrisaDigital: Error`.

No editar ese archivo con la cadena real ni con contraseñas. Las variables de entorno sobrescriben sus valores.

La aplicación valida esta configuración al arrancar fuera de Development. Si `AllowedHosts` sigue con el marcador, la cadena de conexión está vacía o falta `DataProtection:KeysPath`, **el proceso no arranca** y escribe los errores en el log. Esto evita publicar una instancia con valores de ejemplo. En Development la validación no se aplica para no romper el runner E2E.

> El archivo `appsettings.Development.json` y su plantilla quedaron excluidos de `dotnet publish`. El artefacto de producción solo incluye `appsettings.json` y `appsettings.Production.json`.

### 3.5. Cadena de conexión de producción

Ejemplo:

```text
Server=sql01.ejemplo.local,1433;Database=SonrisaDigital;User Id=sonrisa_app;Password=CAMBIA_ESTA_CLAVE;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;
```

Recomendaciones:

- Usar `Encrypt=True` y `TrustServerCertificate=False`.
- Si SQL Server usa un certificado interno, instalar la CA correspondiente en el servidor de aplicación. No desactivar la validación del certificado para “que funcione”.
- No usar `sa` ni un usuario `sysadmin` para la aplicación.
- Mantener `MultipleActiveResultSets=True` si la aplicación lo requiere; es el valor que ya usa el ejemplo de desarrollo.
- La aplicación reintenta errores transitorios hasta **5 veces**, con una espera máxima de **30 segundos** entre intentos. Eso no reemplaza una base altamente disponible ni un plan de respaldo.

### 3.6. Administrador inicial

Al arrancar, `IdentitySeeder`:

1. Crea los roles **Administrador**, **Recepcionista** y **Odontólogo** si no existen.
2. Crea el catálogo de permisos y asigna los permisos iniciales a los roles nuevos.
3. Crea el usuario administrador si no existe y si `Authentication:AdminEmail` y `Authentication:AdminPassword` están configuradas.
4. Crea el catálogo base del odontograma.

Detalles importantes:

- Si falta `Authentication:AdminEmail` o `Authentication:AdminPassword`, **no se crea el administrador** y se registra una advertencia.
- Si el administrador ya existe, **no se actualiza su contraseña** al cambiar la variable de entorno. La contraseña se cambia desde la interfaz.
- No hay correo predeterminado. Configurar siempre un correo real.
- Si la base no existe o no tiene migraciones, el arranque continúa, pero el seeding falla y se registra una advertencia. La aplicación puede responder, pero el login no funcionará hasta corregir la base.

Después del primer acceso:

1. Cambiar la contraseña del administrador.
2. Crear los usuarios reales de la clínica.
3. Revisar los permisos por rol.
4. Retirar `Authentication__AdminPassword` del entorno.

El login de demostración fue retirado de la interfaz: `/login` no muestra credenciales de prueba. El producto tampoco genera contraseñas automáticamente para usuarios reales; el administrador asigna la contraseña inicial y puede restablecerla desde **Usuarios**.

Después de crear el administrador y cambiar su contraseña, se puede retirar `Authentication__AdminPassword` del entorno. El seeding seguirá arrancando, registrará una advertencia por la ausencia de la clave y no modificará al administrador existente.

### 3.7. Proxies y reenvío de cabeceras

La aplicación configura `ForwardedHeaders` para leer:

- `X-Forwarded-For`
- `X-Forwarded-Proto`

Configuración:

```json
"ReverseProxy": {
  "ForwardLimit": 1,
  "KnownProxies": []
}
```

- **IIS en la misma máquina:** dejar `KnownProxies` vacío. El loopback se considera confiable.
- **nginx en la misma máquina:** también puede dejarse vacío si nginx envía las cabeceras desde loopback.
- **Proxy en otra IP:** agregar su IP. En variables de entorno:

```powershell
[Environment]::SetEnvironmentVariable(
    "ReverseProxy__KnownProxies__0",
    "10.0.0.10",
    "Machine")
```

- **Varios proxies:** agregar `ReverseProxy__KnownProxies__0`, `__1`, etc., y ajustar `ForwardLimit` al número de saltos confiables.

No configurar `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` si ya se usa la configuración explícita de la aplicación. Esa variable habilita un comportamiento genérico que confía en cabeceras de proxies no declarados.

> Si el proxy no envía `X-Forwarded-Proto`, la aplicación puede creer que la petición es HTTP y entrar en un bucle de redirección. El login por HTTP tampoco funcionará, porque en producción la cookie se marca `Secure`. Verificar siempre las cabeceras en el servidor.

---

## 4. Publicación de la aplicación

### 4.1. Compilar y publicar

Desde la raíz del repositorio:

```powershell
dotnet restore SonrisaDigital.slnx
dotnet build SonrisaDigital.slnx -c Release
dotnet publish src/SonrisaDigital.UI/SonrisaDigital.UI.csproj `
  -c Release `
  -o "C:\Sites\SonrisaDigital\releases\20261024-1"
```

El resultado incluye:

- `SonrisaDigital.UI.dll`
- `appsettings.json` y `appsettings.Production.json` (no se publica `appsettings.Development.json`)
- `web.config` (necesario para IIS)
- `wwwroot` y los assets estáticos generados
- Dependencias de la aplicación

No publicar directamente sobre la carpeta que está sirviendo la versión actual. Usar carpetas versionadas:

```text
C:\Sites\SonrisaDigital\
  releases\
    20261024-1\
    20261101-1\
  current\        ← junction o acceso directo a la versión activa
```

En Linux:

```text
/opt/sonrisadigital/
  releases/20261024-1/
  current -> releases/20261024-1
```

La carpeta `current` permite cambiar de versión sin reescribir la configuración de IIS o nginx.

### 4.2. Verificación previa a la publicación

Antes de publicar, ejecutar la compilación estricta, las pruebas automatizadas y el flujo E2E contra una base temporal:

```powershell
dotnet restore SonrisaDigital.slnx
dotnet build SonrisaDigital.slnx -c Release -warnaserror
dotnet test tests/SonrisaDigital.Tests/SonrisaDigital.Tests.csproj -c Release --no-build
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/ejecutar-e2e-local.ps1 `
  -Configuration Release
```

El proyecto debe compilar con **0 errores y 0 warnings** y las 35 pruebas deben aprobarse. El runner E2E crea una base temporal, aplica las migraciones, arranca la aplicación y recorre login, odontólogo, paciente, cita, tratamiento y pago; al terminar elimina la base y detiene la aplicación. No usa datos de producción.

Si el equipo de publicación no tiene Node.js o SQL Server local, ejecutar el pipeline de GitHub Actions y descargar el artefacto solo cuando `build-test` y `e2e` estén en verde. En cualquier caso, antes de abrir el sistema a los usuarios hay que completar las pruebas manuales de humo descritas al final de esta guía.

### 4.3. Publicación en IIS

El `web.config` generado por `dotnet publish` usa el módulo ANCM y el hosting **in-process**:

```xml
<aspNetCore processPath="dotnet"
            arguments=".\SonrisaDigital.UI.dll"
            stdoutLogEnabled="false"
            stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess" />
```

Para fijar el entorno de producción se puede usar una variable de máquina o agregar el valor en `web.config`:

```xml
<aspNetCore processPath="dotnet"
            arguments=".\SonrisaDigital.UI.dll"
            stdoutLogEnabled="false"
            stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
  </environmentVariables>
</aspNetCore>
```

No guardar contraseñas en `web.config`. Para secretos, usar variables de máquina, una cuenta de servicio o un almacén de secretos.

---

## 5. Base de datos y migraciones

### 5.1. Crear la base y el usuario de aplicación

La base puede crearla el DBA. Un esquema mínimo de permisos es:

```sql
-- Crear el login de la aplicación (ejecutar como administrador de SQL Server).
CREATE LOGIN sonrisa_app
WITH PASSWORD = 'CAMBIA_ESTA_CLAVE',
     CHECK_POLICY = ON;

USE SonrisaDigital;

CREATE USER sonrisa_app FOR LOGIN sonrisa_app;

-- Permisos de lectura y escritura para la operación normal.
ALTER ROLE db_datareader ADD MEMBER sonrisa_app;
ALTER ROLE db_datawriter ADD MEMBER sonrisa_app;
```

Para aplicar migraciones se necesita un usuario con permisos DDL. Opciones:

- Usar un login de despliegue con `db_ddladmin` o `db_owner` solo durante la migración.
- Generar un script SQL y ejecutarlo con el DBA.
- No dar permisos DDL permanentes al usuario `sonrisa_app`.

### 5.2. Aplicar migraciones

La aplicación **no aplica migraciones automáticamente**. Hay dos formas controladas:

#### Opción A: desde el equipo de publicación

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__DefaultConnection = "Server=sql01.ejemplo.local,1433;Database=SonrisaDigital;User Id=sonrisa_deploy;Password=CAMBIA_ESTA_CLAVE;Encrypt=True;TrustServerCertificate=False;"

dotnet ef database update `
  --project src/SonrisaDigital.Data/SonrisaDigital.Data.csproj `
  --startup-project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj
```

#### Opción B: generar un script y ejecutarlo con el DBA

```powershell
dotnet ef migrations script --idempotent `
  --project src/SonrisaDigital.Data/SonrisaDigital.Data.csproj `
  --startup-project src/SonrisaDigital.UI/SonrisaDigital.UI.csproj `
  --output migrate-sonrisadigital.sql
```

El script idempotente se puede revisar y ejecutar en la base de producción. Es la opción recomendada cuando el DBA controla los cambios de esquema.

La migración `20261025015324_AddCitaPacienteFechaIndex` ya está aplicada en la base local `SonrisaDigital`; en el servidor de producción sigue pendiente. Después de aplicarla, confirmar que el índice quedó creado:

```sql
SELECT name, type_desc
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.Citas')
  AND name = 'IX_Citas_PacienteId_Fecha_HoraInicio';
```

El resultado debe ser `IX_Citas_PacienteId_Fecha_HoraInicio` con `type_desc = NONCLUSTERED`. Si la migración se aplicó desde otro equipo, ejecutar `dotnet tool restore` para usar la versión de `dotnet-ef` fijada en `.config/dotnet-tools.json`.

### 5.3. Orden de una puesta en marcha inicial

1. Crear la base vacía.
2. Configurar la cadena de conexión de producción.
3. Aplicar todas las migraciones.
4. Configurar `Authentication:AdminEmail` y `Authentication:AdminPassword`.
5. Arrancar la aplicación.
6. Verificar `/health/ready`.
7. Iniciar sesión con el administrador inicial.
8. Cambiar la contraseña y crear los usuarios reales.

### 5.4. Seeding y verificación

El seeding se ejecuta en cada arranque, pero es idempotente:

- No duplica roles ni permisos.
- No cambia los permisos que el administrador ya modificó.
- No actualiza la contraseña de un administrador existente.

Si el seeding falla, el error se registra como advertencia y la aplicación sigue arrancando. Revisar los logs y `/health/ready` antes de dar por buena la instalación.

---

## 6. Despliegue con IIS

### 6.1. Instalar el Hosting Bundle

1. Descargar e instalar el **.NET 10 Hosting Bundle** en el servidor.
2. Reiniciar IIS o ejecutar `iisreset` si el instalador lo solicita.
3. Verificar que el runtime esté disponible:

```powershell
dotnet --list-runtimes
```

Debe aparecer `Microsoft.AspNetCore.App 10.x`.

### 6.2. Crear el pool de aplicaciones

En el Administrador de IIS:

- **Nombre:** `SonrisaDigital`
- **Versión de .NET CLR:** `No Managed Code`
- **Modo de canalización:** `Integrated`
- **Identidad:** `ApplicationPoolIdentity` (o una cuenta de servicio restringida)
- **Load User Profile:** `True` si se usa el key ring predeterminado de Data Protection
- **Start Mode:** `AlwaysRunning` (opcional, recomendado)
- **Idle Time-out:** `0` o un valor mayor que el horario de atención
- **Regular Time Interval:** desactivado o programado fuera del horario de atención

> Los recicles del pool cortan los circuitos de Blazor. Programarlos fuera del horario de atención o desactivarlos si la operación lo permite.

### 6.3. Permisos de carpeta

La identidad del pool necesita:

- **Lectura y ejecución** sobre la carpeta de la release y `current`.
- **Escritura** sobre la carpeta de logs si se habilita `stdoutLogEnabled`.
- **Escritura** sobre la carpeta de claves de Data Protection, si se configura una carpeta persistente.
- **Sin permiso de escritura** sobre los binarios de la aplicación.

Ejemplo con `icacls`:

```powershell
icacls "C:\Sites\SonrisaDigital" /grant "IIS APPPOOL\SonrisaDigital:(OI)(CI)(RX)" /T
icacls "C:\Sites\SonrisaDigital\logs" /grant "IIS APPPOOL\SonrisaDigital:(OI)(CI)(M)" /T
```

Ajustar las rutas y la cuenta según la instalación.

### 6.4. Crear el sitio y el binding HTTPS

1. Crear un sitio en IIS apuntando a `C:\Sites\SonrisaDigital\current`.
2. Agregar un binding **HTTPS 443** con el certificado del dominio.
3. Opcional: agregar un binding **HTTP 80** para que la aplicación redirija a HTTPS con `308`.
4. Configurar el nombre de host (`clinica.ejemplo.com`).
5. Verificar que `AllowedHosts` incluya ese dominio.

La aplicación aplica HSTS solo fuera de Development. Con `ASPNETCORE_ENVIRONMENT=Production`, la respuesta HTTPS incluye:

```text
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

`Preload` está desactivado. No enviar el dominio a la lista de precarga hasta que todos los subdominios funcionen por HTTPS.

### 6.5. WebSocket

Blazor Server usa SignalR y, preferentemente, WebSocket. En IIS:

1. Instalar la característica **WebSocket Protocol**.
2. Verificar que el sitio no tenga un módulo o proxy que bloquee `Upgrade`.
3. Abrir la aplicación en el navegador y confirmar en las herramientas de desarrollo que `/_blazor` establece un WebSocket con código `101`.

Si WebSocket no funciona, SignalR puede intentar otros transportes, pero la experiencia empeora y el modal de reconexión puede aparecer con más frecuencia.

### 6.6. Data Protection

Data Protection firma las cookies de autenticación y protege el estado de los circuitos de Blazor. La aplicación lee la ruta desde `DataProtection:KeysPath`:

```json
"DataProtection": {
  "KeysPath": "C:\\Sites\\SonrisaDigital\\data-protection-keys"
}
```

Comportamiento:

- Si la ruta está configurada, las claves se guardan ahí con `PersistKeysToFileSystem` y se identifican con el nombre de aplicación `SonrisaDigital`.
- En producción la ruta es **obligatoria**: si falta, la aplicación no arranca. En Development puede quedar vacía y se usa el key ring predeterminado.
- La carpeta debe estar **fuera de la carpeta de publicación**, con permiso de escritura para la cuenta de la aplicación y respaldo periódico.
- En IIS se recomienda `C:\Sites\SonrisaDigital\data-protection-keys`; en Linux, `/var/lib/sonrisadigital/data-protection-keys`; en Docker, un volumen persistente.
- Si se escala a varios servidores, compartir el mismo key ring entre las instancias.
- No borrar la carpeta de claves al publicar una nueva versión.

La aplicación crea la carpeta si no existe, pero no puede concederse permisos a sí misma. La cuenta de servicio debe tenerlos antes del arranque.

Si las claves se pierden o cambian, los usuarios deben iniciar sesión de nuevo. Esto no corrompe datos, pero afecta la continuidad.

### 6.7. Logs en IIS

- Mantener `stdoutLogEnabled="false"` en operación normal.
- Habilitarlo solo para diagnosticar un fallo de arranque; los archivos `stdout_*.log` pueden crecer sin control.
- Revisar el **Visor de eventos de Windows** para errores de ANCM y del pool.
- Revisar los logs de la aplicación según el nivel configurado.

Para diagnosticar un `500.30` o `500.31`, habilitar stdout temporalmente, reproducir el error, revisar el archivo y volver a deshabilitarlo.

---

## 7. Despliegue con Linux y nginx

### 7.1. Publicar y copiar

```bash
dotnet publish src/SonrisaDigital.UI/SonrisaDigital.UI.csproj \
  -c Release \
  -o /opt/sonrisadigital/releases/20261024-1
```

Crear la cuenta de servicio y los directorios:

```bash
sudo useradd --system --home /var/lib/sonrisadigital --shell /usr/sbin/nologin sonrisa
sudo mkdir -p /opt/sonrisadigital/releases /var/lib/sonrisadigital
sudo chown -R sonrisa:sonrisa /opt/sonrisadigital /var/lib/sonrisadigital
sudo ln -sfn /opt/sonrisadigital/releases/20261024-1 /opt/sonrisadigital/current
```

### 7.2. Servicio systemd

Crear `/etc/systemd/system/sonrisadigital.service`:

```ini
[Unit]
Description=SonrisaDigital
After=network.target

[Service]
Type=simple
User=sonrisa
Group=sonrisa
WorkingDirectory=/opt/sonrisadigital/current
ExecStart=/usr/bin/dotnet /opt/sonrisadigital/current/SonrisaDigital.UI.dll
Restart=always
RestartSec=10
SyslogIdentifier=sonrisadigital
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=HOME=/var/lib/sonrisadigital
EnvironmentFile=/etc/sonrisadigital/sonrisa.env
NoNewPrivileges=true
PrivateTmp=true
TimeoutStopSec=30

[Install]
WantedBy=multi-user.target
```

Activar y arrancar:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now sonrisadigital
sudo systemctl status sonrisadigital
```

### 7.3. Configuración de nginx

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 80;
    server_name clinica.ejemplo.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name clinica.ejemplo.com;

    ssl_certificate     /etc/letsencrypt/live/clinica.ejemplo.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/clinica.ejemplo.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;

        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;

        # SignalR/Blazor mantiene conexiones largas.
        proxy_buffering off;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }
}
```

Verificar y recargar:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

Puntos importantes:

- `Host $host` conserva el dominio para `AllowedHosts`.
- `X-Forwarded-Proto $scheme` permite que la aplicación sepa que la petición original era HTTPS.
- `Upgrade` y `Connection` son necesarios para WebSocket.
- Kestrel escucha solo en `127.0.0.1:5000`; no exponerlo directamente a Internet.
- Si nginx está en otra máquina, agregar su IP en `ReverseProxy__KnownProxies__0`.

### 7.4. Certificado y renovación

Con Certbot:

```bash
sudo certbot --nginx -d clinica.ejemplo.com -d www.clinica.ejemplo.com
sudo systemctl status certbot.timer
```

Verificar la renovación automática y renovar manualmente una vez en un entorno de prueba.

### 7.5. Logs en Linux

- `journalctl -u sonrisadigital -f` para los logs del servicio.
- `/var/log/nginx/error.log` y `/var/log/nginx/access.log` para el proxy.
- Configurar rotación y retención para no llenar el disco.

---

## 8. Despliegue con Docker (opcional)

La aplicación no incluye un `Dockerfile` en el repositorio. Si se elige Docker, usar una plantilla como la siguiente y revisarla antes de publicar.

### 8.1. `.dockerignore`

```text
**/bin
**/obj
.git
.vs
docs
appsettings.Development.json
*.user
```

### 8.2. `Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore SonrisaDigital.slnx
RUN dotnet publish src/SonrisaDigital.UI/SonrisaDigital.UI.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build --chown=app:app /app/publish .
USER app

# Carpeta persistente para las claves de Data Protection. El volumen se monta
# sobre esta ruta para que las sesiones sobrevivan a la recreación del contenedor.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SonrisaDigital.UI.dll"]
```

### 8.3. Construir y ejecutar

```bash
docker build -t sonrisadigital:1.0 .
```

```bash
docker run -d \
  --name sonrisadigital \
  --restart unless-stopped \
  -p 127.0.0.1:8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e AllowedHosts="clinica.ejemplo.com" \
  -e ConnectionStrings__DefaultConnection="Server=sql01.ejemplo.local,1433;Database=SonrisaDigital;User Id=sonrisa_app;Password=CAMBIA_ESTA_CLAVE;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;" \
  -e Authentication__AdminEmail="admin@clinica.ejemplo.com" \
  -e Authentication__AdminPassword="CAMBIA_ESTA_CLAVE_INICIAL" \
  -e DataProtection__KeysPath="/home/app/.aspnet/DataProtection-Keys" \
  -v sonrisadigital-keys:/home/app/.aspnet/DataProtection-Keys \
  sonrisadigital:1.0
```

Recomendaciones:

- Publicar el puerto solo en `127.0.0.1` y poner nginx o IIS delante.
- No incluir contraseñas en la imagen ni en el `Dockerfile`.
- Usar un archivo de entorno con permisos restringidos o un gestor de secretos; no dejar la contraseña en el historial del shell.
- Mantener SQL Server fuera del contenedor para producción, salvo que se administre con respaldo, licencia y monitoreo adecuados.
- **Persistir las claves de Data Protection.** Configurar `DataProtection__KeysPath` y montar un volumen en esa ruta. Sin el volumen, al recrear el contenedor se invalidan las sesiones. El ejemplo usa `/home/app/.aspnet/DataProtection-Keys` porque la imagen ejecuta como el usuario `app`.
- Configurar el health check en el proxy o el orquestador contra `/health/ready`.
- Revisar la zona horaria del contenedor si la clínica depende de fechas locales.

---

## 9. HTTPS, HSTS y cookies

### 9.1. HTTPS obligatorio

- Publicar siempre por HTTPS.
- Mantener un certificado válido y renovado.
- Redirigir HTTP a HTTPS con `308`.
- No iniciar sesión por HTTP: en producción la cookie se marca `Secure` y el navegador no la enviará por HTTP.

### 9.2. HSTS

La aplicación configura:

- `MaxAge = 365 días`
- `IncludeSubDomains = true`
- `Preload = false`
- `UseHsts()` solo fuera de Development

Antes de habilitar HSTS en un dominio real, confirmar que **todos** los subdominios funcionan por HTTPS. `IncludeSubDomains` puede dejar inaccesibles subdominios que solo tengan HTTP.

### 9.3. Cookies y sesión

La cookie de autenticación:

- Se llama `SonrisaDigital.Auth`.
- Es `HttpOnly`.
- Usa `SameSite=Lax`.
- Usa `Secure` en producción.
- Dura **8 horas fijas**, sin renovación deslizante.

El login tiene:

- Límite de **5 intentos por minuto por IP**.
- Bloqueo de cuenta tras **5 intentos fallidos**.
- Logout por `POST` con validación antiforgery.

> El rate limiting usa la IP reconstruida por ForwardedHeaders. Si el proxy no está bien configurado, todos los usuarios pueden compartir la IP del proxy y agotar el límite. Verificar `KnownProxies` y `X-Forwarded-For`.

### 9.4. Cabeceras de seguridad

La aplicación agrega a todas las respuestas:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: geolocation=(), microphone=(), camera=()`
- `Content-Security-Policy` compatible con Blazor Server

La CSP mantiene `'unsafe-inline'` y `'unsafe-eval'` por compatibilidad con Blazor. No agregar orígenes externos sin revisar el impacto.

---

## 10. Health checks, logs y monitoreo

### 10.1. Endpoints de salud

| Endpoint | Uso | Respuesta |
|---|---|---|
| `GET /health` | Liveness: el proceso está vivo | `200 {"status":"Healthy"}` |
| `GET /health/ready` | Readiness: la base responde | `200` si SQL Server conecta; `503` si no |

Características:

- Son anónimos.
- Se responden sin redirección a HTTPS para que un balanceador pueda consultarlos por HTTP.
- No exponen cadenas de conexión, servidores ni trazas.
- `/health/ready` usa `CanConnectAsync`; no valida que las migraciones estén al día.

Si el sitio IIS solo tiene binding HTTPS, consultar los health checks por HTTPS. El short-circuit evita el `308`, pero no puede atender un puerto que no está escuchando.

### 10.2. Monitoreo mínimo

- Consultar `/health` cada 30 segundos.
- Consultar `/health/ready` cada 60 segundos.
- Alertar si `/health/ready` responde `503` durante más de 2 minutos.
- Monitorear espacio en disco de SQL Server, backups y logs.
- Monitorear la fecha de vencimiento del certificado.
- Verificar que el job de backup haya terminado correctamente.
- Ejecutar `scripts/verificar-produccion.ps1` después de cada despliegue y de cada cambio de configuración.
- Revisar la auditoría de la aplicación periódicamente.

### 10.3. Logging

Niveles configurados:

- `appsettings.json`: `Default: Information`, `Microsoft.AspNetCore: Warning`, `Microsoft.EntityFrameworkCore: Warning`, `SonrisaDigital: Information`.
- `appsettings.Production.json`: `Default: Warning`, `Microsoft.AspNetCore: Warning`, `Microsoft.EntityFrameworkCore: Warning`, `SonrisaDigital: Error`.

En producción se reduce el volumen para evitar llenar el disco. Si se necesita más detalle durante una instalación, subir temporalmente `Logging__LogLevel__Default` a `Information` y revertirlo al terminar.

La aplicación usa el logging integrado de ASP.NET Core; no incluye Serilog ni OpenTelemetry. Si se requiere correlación centralizada, integrar un proveedor de logging y enviar los eventos por un canal seguro.

Recomendaciones:

- No registrar datos clínicos ni contraseñas.
- Revisar los logs de errores de la aplicación y de ANCM/nginx.
- Definir retención de logs según la política de la clínica.
- Si se usa un colector central, enviar los logs por un canal seguro.

### 10.4. Auditoría

La aplicación registra auditoría en la base de datos. Esa información:

- Se incluye en los respaldos.
- Debe conservarse según la política de la clínica.
- No debe exponerse en logs públicos.
- Debe revisarse ante incidentes.

---

## 11. Respaldos y restauración

### 11.1. Qué respaldar

1. **Base de datos `SonrisaDigital`**: pacientes, citas, historias clínicas, pagos, usuarios, permisos y auditoría.
2. **Claves de Data Protection**, si se usa una carpeta persistente.
3. **Configuración de producción** (variables, bindings, certificados) en un almacén seguro, no en el repositorio.
4. **Certificado TLS** y su clave privada, si no se usa un proveedor que los gestione.

### 11.2. Backup completo

Ejemplo con SQL Server:

```sql
BACKUP DATABASE [SonrisaDigital]
TO DISK = N'D:\Backups\SonrisaDigital_FULL_20261024_2200.bak'
WITH INIT,
     COMPRESSION,
     CHECKSUM,
     STATS = 10;
```

Verificar el archivo:

```sql
RESTORE VERIFYONLY
FROM DISK = N'D:\Backups\SonrisaDigital_FULL_20261024_2200.bak'
WITH CHECKSUM;
```

### 11.3. Backup de log

Si la clínica no puede perder un día completo de trabajo, usar el modelo de recuperación completa y backups de log frecuentes:

```sql
BACKUP LOG [SonrisaDigital]
TO DISK = N'D:\Backups\SonrisaDigital_LOG_20261024_2215.trn'
WITH INIT,
     COMPRESSION,
     CHECKSUM,
     STATS = 10;
```

Programar los log backups cada 15 o 30 minutos según el RPO acordado.

### 11.4. Retención sugerida

| Tipo | Frecuencia | Retención |
|---|---|---|
| Completo | Diario | 30 días |
| Completo semanal | Semanal | 12 semanas |
| Completo mensual | Mensual | 12 meses |
| Log | Cada 15–30 minutos | 7–14 días |

Ajustar según la normativa aplicable, el volumen de datos y el RPO/RTO acordados con la clínica. Guardar al menos una copia **fuera del servidor de producción**.

### 11.5. Prueba de restauración

Nunca dar por válido un backup que no se haya restaurado. Una prueba mínima:

1. Listar los archivos lógicos:

```sql
RESTORE FILELISTONLY
FROM DISK = N'D:\Backups\SonrisaDigital_FULL_20261024_2200.bak';
```

2. Restaurar en una base de prueba, con nombres diferentes:

```sql
RESTORE DATABASE [SonrisaDigital_Prueba]
FROM DISK = N'D:\Backups\SonrisaDigital_FULL_20261024_2200.bak'
WITH MOVE 'SonrisaDigital' TO N'D:\Restore\SonrisaDigital_Prueba.mdf',
     MOVE 'SonrisaDigital_log' TO N'D:\Restore\SonrisaDigital_Prueba_log.ldf',
     RECOVERY,
     REPLACE;
```

3. Verificar:
   - La aplicación puede iniciar con la base restaurada.
   - El login funciona.
   - El dashboard y los reportes devuelven datos.
   - La auditoría está presente.

Repetir la prueba al menos una vez por trimestre y después de cambios importantes de esquema.

### 11.6. Automatización

Opciones:

- **SQL Server Agent** con un job diario.
- **Windows Task Scheduler** con `sqlcmd`.
- **cron/systemd timer** en Linux.
- **Herramienta de backup del proveedor** si SQL Server está en una nube administrada.

Ejemplo de PowerShell para un job de Windows:

```powershell
$fecha = Get-Date -Format "yyyyMMdd-HHmm"
$archivo = "D:\Backups\SonrisaDigital_$fecha.bak"

sqlcmd -S "sql01.ejemplo.local" -E -Q @"
BACKUP DATABASE [SonrisaDigital]
TO DISK = N'$archivo'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;
"@

if ($LASTEXITCODE -ne 0) {
    throw "El backup de SonrisaDigital falló con código $LASTEXITCODE."
}
```

Usar autenticación de Windows para el job si es posible. Si se usa autenticación SQL, proteger las credenciales en el programador de tareas o en un almacén de secretos.

### 11.7. Seguridad de los respaldos

- Los backups contienen datos clínicos y financieros: tratarlos como información sensible.
- Restringir el acceso a la carpeta de backups.
- Cifrar los backups si la normativa lo exige.
- No enviar backups por correo ni por almacenamiento público.
- Probar la restauración en un entorno aislado.

### 11.8. Scripts incluidos en el repositorio

El repositorio incluye dos scripts de PowerShell para estandarizar la operación:

**Respaldo completo:**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/backup-sonrisadigital.ps1 `
  -Server ".\SQLEXPRESS" `
  -Database "SonrisaDigital" `
  -BackupDirectory "D:\Backups\SonrisaDigital" `
  -RetentionDays 30
```

El script ejecuta `BACKUP DATABASE`, `RESTORE VERIFYONLY`, calcula SHA256 y elimina los respaldos fuera de retención. Si no se indica `-BackupDirectory`, usa la carpeta de respaldos de la instancia. Esa carpeta suele estar protegida para la cuenta del operador; en ese caso el respaldo se verifica igualmente, pero el script avisa que no pudo calcular hash ni aplicar retención. Para operación real, usar una carpeta compartida con permisos de escritura para la cuenta de servicio de SQL Server y de lectura para el operador.

**Prueba de restauración:**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/probar-restauracion-sonrisadigital.ps1 `
  -Server ".\SQLEXPRESS" `
  -BackupFile "D:\Backups\SonrisaDigital\SonrisaDigital_FULL_20261025-211926.bak"
```

El script restaura el respaldo en una base temporal, ejecuta `DBCC CHECKDB` y elimina la base al terminar. No se debe ejecutar contra la base de producción.

**Script de migraciones:**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/generar-script-migraciones.ps1
```

Genera `artefactos/migraciones-sonrisadigital.sql` con `dotnet ef migrations script --idempotent`. El archivo queda fuera de Git (`artefactos/` está ignorado), se revisa y se entrega al DBA. El script verifica que esté presente la migración `20261025015324_AddCitaPacienteFechaIndex`.

**Verificación de producción:**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verificar-produccion.ps1 `
  -BaseUrl "https://clinica.ejemplo.com" `
  -RequireHttps
```

Comprueba `/health`, `/health/ready`, la pantalla `/login`, la ausencia de textos demo y las cabeceras de seguridad. Con `-RequireHttps` también exige `Strict-Transport-Security`. Sin ese modificador se puede usar en un smoke test local por HTTP.

La guía completa de pruebas, cobertura y pipeline está en `docs/07-pruebas-y-ci.md`.

---

## 12. Actualización y rollback

### 12.1. Procedimiento de actualización

1. Avisar la ventana de mantenimiento. Los circuitos abiertos se cortarán.
2. Hacer un backup completo y verificar que termine bien.
3. Publicar la nueva versión en una carpeta nueva.
4. Detener el pool de IIS o el servicio.
5. Aplicar las migraciones.
6. Cambiar `current` a la nueva release.
7. Arrancar el pool o el servicio.
8. Verificar `/health/ready`.
9. Ejecutar `scripts/verificar-produccion.ps1 -BaseUrl "https://clinica.ejemplo.com" -RequireHttps`.
10. Probar login, dashboard, agenda, pacientes y descarga de un PDF.
11. Revisar logs de errores.
12. Conservar la release anterior y el backup hasta confirmar la estabilidad.

### 12.2. Rollback

Si la nueva versión falla:

1. Detener el pool o el servicio.
2. Volver `current` a la release anterior.
3. Arrancar.
4. Verificar `/health/ready` y el login.

Si la migración de base de datos no es compatible con la versión anterior, restaurar el backup previo antes de volver a arrancar. Por eso el backup es obligatorio antes de cada migración.

### 12.3. Cambios de configuración

- Cambiar variables de entorno y reciclar el pool/servicio.
- No editar archivos de la release en caliente.
- No publicar una release nueva solo para cambiar una contraseña; usar variables o el almacén de secretos.
- Documentar cada cambio de configuración en la bitácora de operación.

---

## 13. Checklist de salida a producción

### Seguridad

- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `AllowedHosts` contiene solo los dominios reales; no queda `*` ni el marcador.
- [x] La aplicación valida al arrancar `AllowedHosts`, la cadena de conexión y `DataProtection:KeysPath` fuera de Development.
- [ ] HTTPS obligatorio con certificado válido.
- [ ] HSTS presente en respuestas HTTPS.
- [ ] Cadena de conexión y contraseñas fuera del repositorio.
- [ ] `appsettings.Production.json` sin secretos.
- [x] `appsettings.Development.json` y su plantilla quedan excluidos de `dotnet publish`.
- [ ] Usuario de SQL Server con permisos mínimos; no `sa`.
- [ ] `Encrypt=True` y `TrustServerCertificate=False`.
- [ ] Contraseña del administrador inicial cambiada.
- [x] Login de demostración retirado de la interfaz y sin credenciales por defecto.
- [ ] `KnownProxies` y `ForwardLimit` correctos.
- [ ] Health checks monitoreados y, si es posible, restringidos a la red interna.
- [x] La aplicación soporta una ruta persistente para Data Protection.
- [ ] `DataProtection:KeysPath` configurado, con permisos y respaldo.
- [ ] Certificado y claves de Data Protection respaldados.
- [ ] Firewall: solo 80/443 expuestos; SQL Server no expuesto a Internet.

### Operación

- [ ] Backup completo programado y verificado.
- [ ] Backups de log programados si el RPO lo exige.
- [ ] Prueba de restauración realizada.
- [ ] Retención de backups definida.
- [ ] Monitoreo de `/health` y `/health/ready`.
- [ ] Monitoreo de disco, certificado y jobs de backup.
- [ ] Logs con retención definida.
- [ ] Recicles del pool programados fuera del horario de atención.
- [ ] WebSocket funcionando.
- [ ] Key ring de Data Protection persistente, con ruta configurada y respaldo verificado.
- [ ] Procedimiento de actualización y rollback probado.
- [ ] Responsable de soporte y contacto definidos.

### Pruebas de humo después del despliegue

- [ ] `/health` responde `200`.
- [ ] `/health/ready` responde `200`.
- [ ] `/login` carga por HTTPS.
- [ ] El administrador puede iniciar sesión.
- [ ] El dashboard carga datos.
- [ ] La agenda muestra citas.
- [ ] Se puede abrir un paciente y su historia clínica.
- [ ] Se puede descargar un PDF de reporte.
- [ ] El PDF responde `Content-Disposition: attachment` y `Cache-Control: no-store`.
- [ ] El cierre de sesión funciona por `POST`.
- [ ] Un usuario sin permiso recibe acceso denegado, no un error técnico.
- [ ] La aplicación reconecta tras una caída breve de red.

---

## 14. Diagnóstico de problemas frecuentes

| Síntoma | Causa probable | Acción |
|---|---|---|
| `500.30` en IIS | La aplicación no arrancó | Revisar Visor de eventos, stdout y cadena de conexión |
| `500.31` en IIS | Falta el runtime o ANCM | Instalar el .NET 10 Hosting Bundle y reiniciar IIS |
| `502.5` en IIS | El proceso de ANCM falló | Revisar stdout, permisos y variables de entorno |
| `400 Bad Request` con el dominio real | `AllowedHosts` no incluye el dominio | Configurar `AllowedHosts` y reciclar |
| La aplicación no arranca y muestra “configuración de producción incompleta” | Falta `AllowedHosts` real, cadena de conexión o `DataProtection:KeysPath` | Revisar el log y completar las variables de entorno |
| Bucle de redirección HTTPS | El proxy no envía `X-Forwarded-Proto` o `KnownProxies` está mal | Revisar ForwardedHeaders y el proxy |
| Login no funciona en producción | Cookie `Secure` sobre HTTP o falta HTTPS | Publicar por HTTPS y revisar el binding |
| `/health/ready` responde `503` | SQL Server no conecta | Revisar cadena, firewall, certificado y credenciales |
| WebSocket falla o reconecta constantemente | Proxy sin Upgrade/Connection o IIS sin WebSocket | Habilitar WebSocket y revisar la configuración |
| Todos los usuarios reciben `429` | Rate limiting detrás de un proxy mal configurado | Corregir `X-Forwarded-For` y `KnownProxies` |
| PDF responde `403` | El usuario no tiene `Reportes.Ver` o permisos específicos | Revisar permisos del rol |
| Usuarios pierden la sesión al reciclar | Data Protection no persiste o el pool no carga perfil | Persistir y respaldar el key ring |
| La aplicación no crea el administrador | Falta `Authentication:AdminEmail` o `Authentication:AdminPassword` | Configurar ambas y reiniciar |
| El administrador existente no cambia de contraseña | El seeding no actualiza usuarios existentes | Cambiar la contraseña desde la interfaz |
| Los estilos o Blazor no cargan | Publicación incompleta o assets no servidos | Verificar `wwwroot`, `MapStaticAssets` y la release publicada |

### 14.1. Comandos útiles

Windows/IIS:

```powershell
iisreset
Restart-WebAppPool -Name "SonrisaDigital"
Get-WinEvent -LogName Application -MaxEvents 50
Invoke-WebRequest https://clinica.ejemplo.com/health
Invoke-WebRequest https://clinica.ejemplo.com/health/ready

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verificar-produccion.ps1 `
  -BaseUrl "https://clinica.ejemplo.com" `
  -RequireHttps
```

Linux/nginx:

```bash
sudo systemctl status sonrisadigital
sudo journalctl -u sonrisadigital -n 100 --no-pager
sudo nginx -t
curl -i https://clinica.ejemplo.com/health
curl -i https://clinica.ejemplo.com/health/ready
```

Docker:

```bash
docker logs --tail 100 sonrisadigital
curl -i http://127.0.0.1:8080/health
curl -i http://127.0.0.1:8080/health/ready
```

---

## 15. Resumen operativo

- Una instancia por clínica; los circuitos viven en memoria.
- HTTPS en el proxy, HSTS en la aplicación, WebSocket habilitado.
- Secretos por variables de entorno o almacén seguro, nunca en el repositorio.
- Migraciones controladas y backup previo obligatorio.
- Health checks monitoreados; `/health/ready` es el indicador real de base disponible.
- Backups automáticos, fuera del servidor y con prueba de restauración.
- Actualizaciones en carpetas versionadas, con rollback y ventana de mantenimiento.
- Recicles y mantenimiento fuera del horario de atención.

---

## 16. Plan de puesta en producción

La secuencia de cierre técnico, despliegue, cutover y aceptación con la clínica está en [Puesta en producción y aceptación](08-puesta-en-produccion.md).
