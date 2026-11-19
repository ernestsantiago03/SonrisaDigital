# escape=`
# Imagen de nginx para Windows. Se construye desde Windows Server Core porque
# nginx para Windows usa las DLL del runtime de Visual C++ y no existe una
# imagen oficial de nginx para contenedores Windows.
#
# La aplicacion (Sonrisa Digital) solo corre en Windows por System.Drawing, por
# lo que el balanceador debe usar el mismo sistema de contenedores.
#
# Construccion (la hace docker compose, no hace falta a mano):
#   docker build -t sonrisadigital-nginx:local docker/nginx
#   docker build --build-arg WINDOWS_TAG=ltsc2025 -t sonrisadigital-nginx:local docker/nginx

ARG WINDOWS_TAG=ltsc2022
FROM mcr.microsoft.com/windows/servercore:${WINDOWS_TAG}

ARG NGINX_VERSION=1.31.8

SHELL ["powershell", "-NoProfile", "-Command"]

# nginx para Windows (binario oficial de nginx.org).
RUN $ErrorActionPreference = 'Stop'; `
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; `
    $ProgressPreference = 'SilentlyContinue'; `
    Invoke-WebRequest -Uri "https://nginx.org/download/nginx-$env:NGINX_VERSION.zip" `
        -OutFile C:\nginx.zip -UseBasicParsing; `
    Expand-Archive -Path C:\nginx.zip -DestinationPath C:\ -Force; `
    Rename-Item -Path "C:\nginx-$env:NGINX_VERSION" -NewName 'nginx'; `
    Remove-Item -Path C:\nginx.zip -Force

# nginx.exe necesita el runtime de Visual C++ 2015-2022, que Server Core no trae.
RUN $ErrorActionPreference = 'Stop'; `
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; `
    $ProgressPreference = 'SilentlyContinue'; `
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vc_redist.x64.exe" `
        -OutFile C:\vc_redist.x64.exe -UseBasicParsing; `
    Start-Process -FilePath C:\vc_redist.x64.exe `
        -ArgumentList '/install', '/quiet', '/norestart' -Wait; `
    Remove-Item -Path C:\vc_redist.x64.exe -Force; `
    if (-not (Test-Path 'C:\Windows\System32\vcruntime140.dll')) { `
        throw 'No se pudo instalar el runtime de Visual C++.' `
    }

COPY nginx.conf C:\nginx\conf\nginx.conf

# El contenedor corre como ContainerUser; necesita escribir logs y el pid.
RUN icacls C:\nginx /grant "BUILTIN\Users:(OI)(CI)M" /T | Out-Null

EXPOSE 80
USER ContainerUser

ENTRYPOINT ["C:\\nginx\\nginx.exe"]
CMD ["-g", "daemon off;"]
