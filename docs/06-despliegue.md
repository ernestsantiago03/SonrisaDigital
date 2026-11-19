# 06 · Despliegue

Este documento describe cómo publicar Sonrisa Digital. La vía soportada es **IIS en Windows Server** (la aplicación usa `System.Drawing` para procesar las imágenes del odontograma, que solo funciona en Windows). Hay además un `Dockerfile` para contenedores Windows, pero **no está verificado** en el equipo donde se preparó: ver la sección 8.

## 1. Requisitos del servidor

- Windows Server con **IIS** y el módulo **ASP.NET Core Hosting Bundle para .NET 10** (instalar después de IIS y reiniciar IIS con `iisreset`).
- **SQL Server** accesible desde el servidor (2019 o superior). La base se crea vacía; el esquema lo aplica el DBA con el script de la sección 4.
- Una carpeta **persistente y respaldada** para las claves de Data Protection (`DataProtection:KeysPath`).
- Una carpeta **fuera de la publicación** para las imágenes del odontograma (`Archivos:Directorio`).
- Certificado HTTPS para el dominio real. La aplicación exige HTTPS (HSTS de 365 días).

## 2. Publicar

Desde una máquina con .NET 10 SDK, en la raíz del repositorio:

```powershell
dotnet publish src/SonrisaDigital.UI/SonrisaDigital.UI.csproj -c Release -o publish
```

La publicación:

- genera `web.config` con `AspNetCoreModuleV2` en modo in-process;
- incluye `appsettings.json` y `appsettings.Production.json` (con valores de ejemplo que hay que reemplazar por variables de entorno o un almacén de secretos);
- **no** incluye `appsettings.Development.json`. La pipeline de CI verifica esto.

Copiar el contenido de `publish/` a la carpeta del sitio, por ejemplo `C:\Sites\SonrisaDigital`. Las carpetas de claves y de archivos **no** deben estar dentro de esa carpeta, porque se reemplaza en cada publicación.

## 3. Sitio y grupo de aplicaciones en IIS

1. Crear un grupo de aplicaciones con **.NET CLR Version: No Managed Code** y el modelo de canalización integrada.
2. Usar una identidad dedicada (por ejemplo `IIS AppPool\SonrisaDigital`) y darle **modificar** solo sobre la carpeta de claves de Data Protection y la carpeta de archivos del odontograma.
3. Crear el sitio apuntando a la carpeta publicada, con un enlace **https** y el nombre de host real.
4. Variables de entorno: definirlas en el grupo de aplicaciones o en `web.config` (sección `aspNetCore/environmentVariables`). Con el separador `__` se escriben igual que en `appsettings`.

### Configuración obligatoria

La aplicación **no arranca** fuera de Development si falta alguno de estos valores. El validador lo muestra en el log de arranque.

| Clave | Ejemplo | Notas |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `AllowedHosts` | `sonrisa.midominio.com` | No puede ser `*` ni contener `REEMPLAZAR`. |
| `ConnectionStrings__DefaultConnection` | `Server=...;Database=SonrisaDigital;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=False` | Sin `TrustServerCertificate=True` ni `Encrypt=False` (si están, se advierte). |
| `DataProtection__KeysPath` | `D:\SonrisaDigital\claves` | Ruta absoluta y persistente. |
| `Archivos__Directorio` | `D:\SonrisaDigital\archivos` | Ruta absoluta, fuera de `wwwroot`. |
| `Dueno__Email` | `duenio@midominio.com` | Cuenta global del Dueño. |
| `Dueno__Password` | (secreto) | Mínimo 16 caracteres. |
| `Seguridad__MfaDuenoObligatorio` | `true` (o sin definir) | **No puede ser `false`** en producción. |
| `ReverseProxy__KnownProxies__0` | `10.0.0.5` | Solo si hay un proxy o balanceador delante. Si la lista está vacía, solo se confía en loopback. |

No definir `Authentication__AdminEmail` ni `Authentication__AdminPassword`: fuera de Development se ignoran.

## 4. Base de datos

No ejecutar `dotnet ef database update` en producción. El esquema se genera como script idempotente y lo aplica el DBA:

```powershell
./scripts/generar-script-migraciones.ps1
```

El resultado queda en `artefactos/migraciones-sonrisadigital.sql`. Esa carpeta está excluida del repositorio: el script se genera en cada entrega y se revisa antes de aplicarlo. El script incluye todas las migraciones, por lo que un script anterior no sirve.

La cuenta SQL de la aplicación necesita, como mínimo, `db_datareader` y `db_datawriter` sobre la base. Los respaldos se programan con `scripts/programar-respaldo.ps1` y la restauración se prueba con `scripts/probar-restauracion-sonrisadigital.ps1`.

## 5. Primer arranque

1. Comprobar `https://sonrisa.midominio.com/health` (responde `Healthy`) y `/health/ready` (comprueba SQL Server).
2. Entrar con la cuenta del Dueño. El sistema pide **activar el segundo factor** antes de cualquier otra pantalla: se muestra la clave manual para la app autenticadora y, al confirmar, se entregan 10 códigos de recuperación que solo se muestran una vez. La activación cierra la sesión y obliga a entrar de nuevo con la contraseña y el código.
3. Crear la primera clínica y su administrador desde la interfaz. **Solo el Dueño crea usuarios y roles**: los roles Administrador, Recepcionista y Odontólogo no pueden administrar cuentas ni permisos.

Si el Dueño pierde el acceso al segundo factor, se restablece desde la consola del servidor (sin pantalla web):

```powershell
dotnet SonrisaDigital.UI.dll restablecer-mfa-dueno <correo>
```

Otros comandos del mismo tipo: `crear-dueno <correo> <nombre>` y `desactivar-dueno <correo>`.

## 6. Registros y diagnóstico

- Fuera de Development los registros salen en JSON por consola. Con `stdoutLogEnabled="true"` en `web.config` (y una carpeta `logs` escribible) quedan en disco para IIS.
- Cada petición lleva `X-Correlation-Id` en la respuesta y en las líneas de log.
- La auditoría de acciones está en la pantalla **Auditoría** del sistema.

## 7. Lista antes de abrir al público

- [ ] `AllowedHosts` con el dominio real y HTTPS en el enlace del sitio.
- [ ] Claves de Data Protection en carpeta persistente y respaldada.
- [ ] Cuenta SQL con permisos mínimos (no `sa`).
- [ ] Script de migraciones aplicado y `/health/ready` en `Healthy`.
- [ ] Segundo factor activo en la cuenta del Dueño y códigos de recuperación guardados fuera del equipo.
- [ ] Respaldo programado y restauración probada.

## 8. Contenedor Windows con docker compose (opcional)

> La aplicación usa `System.Drawing` y **solo funciona en contenedores Windows**. No se puede ejecutar en contenedores Linux. IIS sigue siendo la vía soportada; esta variante es para un servidor con Docker en modo Windows.

El repositorio incluye:

| Archivo | Para qué sirve |
|---|---|
| `Dockerfile` | Imagen de la aplicación (multi-etapa, Windows Server Core). |
| `docker-compose.yml` | Dos réplicas de la aplicación detrás de nginx. |
| `docker/nginx/Dockerfile` | Imagen de nginx para Windows. |
| `docker/nginx/nginx.conf` | Balanceo, afinidad de sesión y WebSocket. |
| `.env.example` | Plantilla de configuración y secretos. |

### 8.1. Requisitos

- Docker en **modo contenedores Windows** (`docker info` debe mostrar `OSType: windows`). Docker Desktop permite cambiar de modo desde la bandeja del sistema.
- El host y la imagen deben usar la misma versión de Windows:
  - Windows Server 2022 / Windows 10 → `WINDOWS_TAG=ltsc2022`.
  - Windows Server 2025 / Windows 11 24H2 o superior → `WINDOWS_TAG=ltsc2025`.
  - Si no coinciden, usar aislamiento Hyper-V o cambiar `WINDOWS_TAG`.
- SQL Server accesible desde los contenedores. **La base no se incluye** en el compose.
- Carpetas del host para las claves de Data Protection y las imágenes del odontograma, con espacio y respaldo.
- **Red:** el compose crea `sonrisadigital_net` con driver `nat`. Si el host no permite crear otra red NAT (por ejemplo, por conflicto de subredes), reemplazar la definición de `networks` en `docker-compose.yml` por la red integrada: `external: true` y `name: nat`.

La primera construcción descarga imágenes base de Windows de varios GB y tarda bastante.

### 8.2. Configuración

```powershell
Copy-Item .env.example .env
New-Item -ItemType Directory -Force `
  C:\SonrisaDigital\datos\claves, `
  C:\SonrisaDigital\datos\archivos
notepad .env
```

Variables de `.env`:

| Variable | Valor |
|---|---|
| `WINDOWS_TAG` | `ltsc2022` o `ltsc2025` según el host. |
| `DOTNET_VERSION` | `10.0`. |
| `APP_REPLICAS` | `2`. |
| `SD_TAG` | Etiqueta local de las imágenes; por ejemplo `local` o la versión que se publica. |
| `HTTP_PORT` | Puerto del host donde escucha nginx (`8080` por omisión). |
| `SD_DATA_DIR` | Carpeta del host compartida por las réplicas (`C:/SonrisaDigital/datos`). |
| `ALLOWED_HOSTS` | Hosts aceptados, separados por `;`. No usar `*`. En local, `localhost`; en producción, el dominio real. |
| `CONNECTION_STRING` | Cadena a SQL Server. Si el certificado no es de confianza en la prueba, agregar `TrustServerCertificate=True`. |
| `DUENO_EMAIL` / `DUENO_PASSWORD` | Cuenta global del Dueño. La contraseña debe cumplir la política (mínimo 16 caracteres). |

El `.env` contiene secretos y está excluido por `.gitignore`: no se versiona.

### 8.3. Arranque

```powershell
docker compose up -d --build
docker compose ps
Invoke-WebRequest http://localhost:8080/health
Invoke-WebRequest http://localhost:8080/health/ready
```

`docker compose up` crea las dos réplicas definidas en `scale` (y en `deploy.replicas`, para compatibilidad). También se puede forzar el número:

```powershell
docker compose up -d --scale app=2
```

Abrir `http://localhost:8080`. Solo nginx publica un puerto; la aplicación escucha en la red interna.

### 8.4. Cómo funciona el balanceo

- Las dos réplicas son el mismo servicio `app`. Docker devuelve una IP por réplica al resolver `app`.
- nginx reparte con **afinidad por cookie** (`hash $http_cookie consistent`). Blazor Server necesita que el `negotiate` de SignalR y el WebSocket caigan en la misma réplica; con round-robin simple el circuito se perdería.
- Los health checks `/health` y `/health/ready` no necesitan afinidad y se reparten con `least_conn`.
- Las réplicas comparten:
  - las claves de Data Protection (`C:\datos\claves`), sin las cuales una cookie emitida por una réplica no se puede descifrar en la otra;
  - las imágenes clínicas (`C:\datos\archivos`), para que una réplica pueda servir lo que guardó la otra.
- El estado de los circuitos de Blazor vive en memoria de cada réplica. Por eso la afinidad es obligatoria y una réplica reiniciada pierde sus circuitos (el usuario reconecta o recarga).
- nginx resuelve `app` al arrancar. Si se cambia el número de réplicas en caliente:

  ```powershell
  docker compose restart nginx
  ```

### 8.5. Operación

```powershell
# Estado y logs
docker compose ps
docker compose logs -f app
docker compose logs -f nginx

# Logs internos de nginx (access/error)
docker compose exec nginx powershell -NoProfile -Command "Get-Content C:\nginx\logs\error.log -Tail 100"

# Actualizar imágenes y recrear réplicas
docker compose up -d --build

# Detener sin borrar los datos del host
docker compose down
```

- **Respaldos:** respaldar `SD_DATA_DIR` (claves e imágenes) junto con la base de datos.
- **Permisos de datos:** la cuenta del contenedor debe poder escribir en `SD_DATA_DIR` (claves e imágenes). Si falla el arranque o la subida de archivos, revisar las ACL de esa carpeta en el host.
- **HTTPS:** el compose publica HTTP. En producción la cookie de autenticación se marca `Secure`, por lo que fuera de `localhost` hay que terminar TLS delante (proxy inverso del servidor, balanceador o un `server` 443 en `nginx.conf`). El proxy debe enviar `X-Forwarded-Proto: https`.
- **IP real del cliente:** si `ReverseProxy__KnownProxies` queda vacío, la aplicación registra una advertencia y usa la IP de nginx. Para conservar la IP del cliente hay que fijar la IP de nginx en la red y declararla en `ReverseProxy__KnownProxies__0`; en un compose con IP dinámica no se configura por omisión.
- **Health checks de Docker:** no están soportados en contenedores Windows. Usar un monitoreo externo contra `/health/ready`, o el propio nginx.
- **Escalado:** con más réplicas, revisar memoria del host y reiniciar nginx después de cambiar `APP_REPLICAS`.
- Si la construcción o el arranque fallan, revisar `docker compose logs` y confirmar que Docker está en modo Windows y que el `WINDOWS_TAG` coincide con el host. IIS sigue siendo la vía soportada.
