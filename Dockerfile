# Contenedor Windows (la aplicación usa System.Drawing y solo funciona en Windows).
# No verificado en el equipo de preparación: ver docs/06-despliegue.md, sección 8.
# La versión de Windows de la imagen debe coincidir con la del host.

FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/SonrisaDigital.UI/SonrisaDigital.UI.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# La configuración sensible (cadena de conexión, Dueño, rutas de claves y archivos) llega
# por variables de entorno al ejecutar el contenedor; no se incluye en la imagen.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SonrisaDigital.UI.dll"]
