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

## 8. Contenedor Windows (no verificado)

El repositorio incluye un `Dockerfile` multi-etapa para contenedores Windows. **No se ha construido ni ejecutado**: en el equipo de preparación Docker Desktop estaba en modo Linux, y la aplicación necesita Windows para el procesamiento de imágenes.

Para probarlo en un servidor con Docker en modo Windows:

```powershell
docker build -t sonrisadigital:local .
docker run -d -p 8080:8080 `
  -e ConnectionStrings__DefaultConnection="..." `
  -e AllowedHosts="localhost" `
  -e DataProtection__KeysPath="C:\claves" `
  -e Archivos__Directorio="C:\archivos" `
  -e Dueno__Email="..." -e Dueno__Password="..." `
  -v D:\sd\claves:C:\claves -v D:\sd\archivos:C:\archivos `
  sonrisadigital:local
```

La imagen base de `aspnet` debe coincidir con la versión de Windows del host. Si la prueba falla, IIS sigue siendo la vía soportada.
