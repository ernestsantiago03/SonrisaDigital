# escape=`
# Sonrisa Digital usa System.Drawing para procesar las imágenes del odontograma y solo
# funciona en Windows. La imagen debe usar el mismo canal de Windows que el host:
#   - Windows Server 2022 -> ltsc2022
#   - Windows Server 2025 -> ltsc2025
# Ver docs/06-despliegue.md, sección 8.
#
# Construcción:
#   docker build -t sonrisadigital:local .
#   docker build --build-arg WINDOWS_TAG=ltsc2025 -t sonrisadigital:local .

ARG DOTNET_VERSION=10.0
ARG WINDOWS_TAG=ltsc2022

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-windowsservercore-${WINDOWS_TAG} AS build
WORKDIR C:\src

# Primero solo los proyectos: si no cambian sus dependencias, la restauración se
# reutiliza de la caché y el build no vuelve a descargar paquetes.
COPY global.json SonrisaDigital.slnx ./
COPY src/SonrisaDigital.Data/SonrisaDigital.Data.csproj src/SonrisaDigital.Data/
COPY src/SonrisaDigital.Services/SonrisaDigital.Services.csproj src/SonrisaDigital.Services/
COPY src/SonrisaDigital.UI/SonrisaDigital.UI.csproj src/SonrisaDigital.UI/
RUN dotnet restore src/SonrisaDigital.UI/SonrisaDigital.UI.csproj

# El resto del código. .dockerignore excluye bin/obj, pruebas, documentación y secretos.
COPY . .
RUN dotnet publish src/SonrisaDigital.UI/SonrisaDigital.UI.csproj `
    -c Release `
    -o C:\app\publish `
    --no-restore `
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-windowsservercore-${WINDOWS_TAG} AS runtime
WORKDIR C:\app

# Carpetas persistentes. Se pueden reemplazar por variables de entorno y montar
# volúmenes; si no se montan, quedan dentro de la capa escribible del contenedor.
ENV DataProtection__KeysPath=C:\datos\claves
ENV Archivos__Directorio=C:\datos\archivos
RUN powershell -NoProfile -Command `
    "New-Item -ItemType Directory -Force -Path 'C:\datos\claves','C:\datos\archivos' | Out-Null"

COPY --from=build C:\app\publish .

# La cadena de conexión, el Dueño y AllowedHosts llegan al ejecutar el contenedor.
# No se incluyen secretos en la imagen.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SonrisaDigital.UI.dll"]
