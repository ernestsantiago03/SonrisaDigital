# Puesta en producción y aceptación

Esta guía define el cierre técnico, el despliegue y la aceptación de SonrisaDigital en una clínica real. Es la secuencia que sigue después de la entrega validada en [Despliegue y operación](06-despliegue.md) y [Pruebas, integración continua y respaldos](07-pruebas-y-ci.md).

> **Objetivo:** pasar de una versión validada en local y en el pipeline a una instalación operativa, respaldada y aceptada por la clínica, sin exponer datos reales antes de completar el cierre técnico.

---

## 1. Condiciones de entrada

Antes de iniciar el despliegue:

- [ ] El pipeline de `main` está en verde: `build-test`, `e2e` y `publish`.
- [ ] El artefacto `sonrisadigital-publicacion` fue descargado y revisado.
- [ ] La migración pendiente está identificada y probada en una copia de la base.
- [ ] El responsable técnico, el DBA y el responsable de la clínica están definidos.
- [ ] Existe una ventana de mantenimiento acordada.
- [ ] Hay un respaldo completo reciente y verificado.
- [ ] Existe un plan de rollback documentado.
- [ ] El dominio, el certificado TLS y el acceso al servidor están disponibles.

Si alguna condición falta, no se inicia la ventana de despliegue.

---

## 2. Alcance y límites

### Incluido

- Instalación de una instancia por clínica.
- Configuración de HTTPS, proxy, WebSocket y cabeceras reenviadas.
- Base de datos SQL Server, migraciones y usuario de aplicación con permisos mínimos.
- Respaldos automáticos, retención y prueba de restauración.
- Health checks, logs, auditoría y monitoreo básico.
- Pruebas de humo, aceptación con usuarios reales y capacitación inicial.
- Retiro del login de demostración y de las credenciales por defecto; verificación de que el producto no genera contraseñas automáticamente.

### Fuera de alcance

- Portal de pacientes.
- Soporte multiclínica o multi-sucursal.
- Aplicación móvil nativa.
- Integraciones con facturación electrónica, seguros o WhatsApp.
- Migración masiva de datos históricos desde otros sistemas, salvo que se acuerde por separado.

---

## 3. Roles y responsabilidades

| Rol | Responsabilidad |
|---|---|
| Propietario del producto | Aprueba el alcance, la ventana, el go/no-go y la aceptación final. |
| Responsable técnico | Prepara la publicación, configura la aplicación y ejecuta el despliegue. |
| DBA o responsable de SQL Server | Crea la base, aplica migraciones, configura respaldos y valida la restauración. |
| Responsable de la clínica | Define usuarios, permisos, horarios y participa en la aceptación. |
| Usuarios clave | Validan los flujos de recepción, odontología, tratamientos, pagos y reportes. |
| Soporte | Recibe la instalación, la documentación y el procedimiento de incidentes. |

---

## 4. Fase 6A — Cierre técnico

El objetivo de 6A es dejar una versión lista para instalar, sin cambios de código pendientes ni configuraciones de demostración.

### 4.1. Retirar funciones de demostración

- [x] Eliminar el login de demostración y sus credenciales.
- [x] Confirmar que el producto no genera contraseñas automáticamente; la contraseña temporal del runner E2E es solo de pruebas.
- [x] Revisar que no queden rutas, botones, textos ni datos de prueba visibles.
- [x] Ajustar las pruebas y la documentación afectadas.
- [x] Confirmar que el administrador real se crea solo con el seeding controlado.

El login de demostración fue retirado de la interfaz. Antes de vender, verificar que no se reintroduzca en una publicación y que el administrador inicial se cree únicamente con las variables de entorno configuradas.

### 4.2. Configuración de producción

> Los puntos marcados con `[x]` quedaron implementados o verificados en local. Los que dependen del dominio, del servidor o de la red se cierran en 6B.

- [ ] Configurar `AllowedHosts` con el dominio real.
- [ ] Configurar `ReverseProxy:KnownProxies` según la ubicación del proxy.
- [ ] Verificar `X-Forwarded-For` y `X-Forwarded-Proto`.
- [ ] Publicar únicamente por HTTPS y confirmar HSTS.
- [x] Confirmar cookies `Secure`, `HttpOnly` y `SameSite`. Están configuradas en `DependencyInjection`; `Secure` se activa fuera de Development.
- [ ] Habilitar WebSocket en IIS o en el proxy.
- [x] Persistir las claves de Data Protection en una ruta configurable con `DataProtection:KeysPath`.
- [ ] Respaldar la carpeta de claves de Data Protection en producción.
- [ ] Configurar la zona horaria del servidor y del contenedor, si aplica.
- [x] El repositorio no contiene cadenas ni secretos reales; la configuración real se suministra por variables de entorno en 6B.
- [x] Confirmar que `appsettings.Development.json` no se publica en producción. Está excluido en el `.csproj` y el pipeline lo verifica.

### 4.3. Base de datos y migraciones

- [ ] Crear la base de producción con el DBA.
- [ ] Crear un usuario de aplicación con permisos mínimos, sin `sa`.
- [x] Generar el script de migraciones y revisarlo con `scripts/generar-script-migraciones.ps1`.
- [ ] Aplicar `20261025015324_AddCitaPacienteFechaIndex` en producción.
- [x] Confirmar el índice `IX_Citas_PacienteId_Fecha_HoraInicio` como `NONCLUSTERED` en una copia restaurada.
- [x] Verificar el historial de migraciones de EF Core en la copia.
- [ ] Crear el administrador inicial y cambiar su contraseña en el primer acceso.
- [ ] Retirar `Authentication__AdminPassword` del entorno después del primer acceso.
- [ ] Revisar roles, permisos y usuarios reales.

### 4.4. Respaldos y restauración

- [ ] Definir frecuencia, retención y ubicación definitivas con la clínica.
- [ ] Configurar respaldo completo, diferencial y de log según el RPO/RTO acordado.
- [ ] Guardar una copia fuera del servidor de producción.
- [ ] Verificar permisos de la cuenta de servicio sobre la carpeta de respaldos.
- [x] Ejecutar `RESTORE VERIFYONLY` sobre un respaldo local.
- [x] Restaurar en una base temporal y ejecutar `DBCC CHECKDB` sin errores.
- [x] Documentar el procedimiento de restauración. El tiempo real se medirá en el servidor de producción.

### 4.5. Observabilidad y operación

- [ ] Verificar `/health` y `/health/ready` en producción.
- [ ] Configurar monitoreo de disponibilidad y espacio en disco.
- [ ] Revisar logs de aplicación, IIS o systemd.
- [ ] Definir quién revisa los logs y con qué frecuencia.
- [ ] Confirmar que la auditoría de actividades se registra en producción.
- [ ] Documentar contactos, escalamiento y ventana de soporte.
- [x] Preparar el runbook de incidentes y rollback en `docs/06-despliegue.md`.

### 4.6. Seguridad y revisión final

- [ ] Revisar permisos por rol con el responsable de la clínica.
- [x] Confirmar rate limiting y cabeceras de seguridad con `scripts/verificar-produccion.ps1`.
- [x] Revisar dependencias: no hay paquetes vulnerables y Dependabot está configurado.
- [x] Confirmar que no hay secretos en el repositorio ni en el artefacto publicado.
- [ ] Revisar la política de contraseñas y retención de datos clínicos.
- [ ] Confirmar la regla de Pull Request y la protección de `main` en GitHub.
- [x] Definir la política de actualización de Dependabot.

### 4.7. Criterios de salida de 6A

6A se considera cerrada cuando:

- [x] El login de demostración fue retirado y no hay generación automática de contraseñas en el producto.
- [x] La configuración de producción está revisada, validada al arrancar y documentada.
- [x] La migración está probada en una copia de la base.
- [x] El respaldo y la restauración fueron verificados en local.
- [x] El runbook y el rollback están documentados.
- [ ] El pipeline está en verde y el artefacto fue revisado.
- [ ] No hay defectos abiertos de severidad alta o crítica.

---

## 5. Fase 6B — Despliegue

### 5.1. Preparar la infraestructura

- [ ] Elegir IIS, Linux con nginx o Docker y documentar la decisión.
- [ ] Instalar el runtime o Hosting Bundle correspondiente.
- [ ] Crear el sitio, el pool o el servicio con una cuenta de mínimo privilegio.
- [ ] Configurar DNS, firewall, certificado TLS y renovación.
- [ ] Crear las carpetas versionadas de publicación y respaldo.
- [ ] Confirmar el acceso al servidor y las credenciales de despliegue.

### 5.2. Publicar la versión

- [ ] Descargar el artefacto aprobado del pipeline.
- [ ] Copiar la publicación a una carpeta nueva con versión o fecha.
- [ ] Configurar variables de entorno y secretos.
- [ ] Detener la aplicación o poner el sitio en mantenimiento.
- [ ] Ejecutar un respaldo completo antes de tocar la base.
- [ ] Aplicar las migraciones con el DBA.
- [ ] Arrancar la aplicación y revisar el log de inicio.
- [ ] Verificar `/health` y `/health/ready`.

### 5.3. Cutover

- [ ] Confirmar la ventana de mantenimiento y la congelación de cambios.
- [ ] Ejecutar el respaldo previo.
- [ ] Aplicar migraciones.
- [ ] Publicar la versión.
- [ ] Ejecutar las pruebas de humo.
- [ ] Comunicar la disponibilidad a la clínica.
- [ ] Registrar hora, versión, responsable y resultado.

### 5.4. Pruebas de humo después del despliegue

- [ ] `/health` responde `200`.
- [ ] `/health/ready` responde `200`.
- [ ] `/login` carga por HTTPS.
- [ ] El administrador puede iniciar sesión.
- [ ] El dashboard carga datos.
- [ ] La agenda muestra citas.
- [ ] Se puede abrir un paciente y su historia clínica.
- [ ] Se puede registrar una cita.
- [ ] Se puede registrar un tratamiento y un pago.
- [ ] Se puede descargar un PDF de reporte.
- [ ] El cierre de sesión funciona por `POST`.
- [ ] Un usuario sin permiso recibe acceso denegado, no un error técnico.
- [ ] La aplicación reconecta tras una caída breve de red.

### 5.5. Rollback

- [ ] Conservar la carpeta de la versión anterior.
- [ ] Conservar el respaldo previo a la migración.
- [ ] Definir el criterio para revertir: error crítico, datos inconsistentes o indisponibilidad prolongada.
- [ ] Documentar cómo restaurar la base y volver a la versión anterior.
- [ ] Probar el rollback en un entorno aislado antes de producción.

La migración de índice no modifica datos existentes, pero el rollback debe considerar igualmente la versión de la aplicación y el respaldo.

### 5.6. Criterios de salida de 6B

- [ ] La aplicación está disponible por HTTPS en el dominio real.
- [ ] Las pruebas de humo están completas.
- [ ] Los respaldos automáticos están activos.
- [ ] El monitoreo y las alertas están configurados.
- [ ] El administrador cambió su contraseña.
- [ ] Los usuarios reales fueron creados con sus permisos.
- [ ] El soporte tiene acceso a logs, runbook y contactos.
- [ ] El cutover quedó registrado.

---

## 6. Fase 6C — Aceptación

### 6.1. Aceptación funcional

Validar con usuarios reales, usando datos de prueba controlados:

- [ ] Inicio y cierre de sesión.
- [ ] Gestión de pacientes.
- [ ] Gestión de odontólogos y usuarios.
- [ ] Agenda y creación de citas.
- [ ] Historia clínica.
- [ ] Odontograma.
- [ ] Catálogo de tratamientos.
- [ ] Tratamientos realizados.
- [ ] Pagos, pagos parciales y saldos.
- [ ] Reportes y exportación a PDF.
- [ ] Auditoría de actividades.
- [ ] Permisos por rol.
- [ ] Mensajes de error y validaciones en español.

### 6.2. Aceptación no funcional

- [ ] Tiempo de respuesta aceptable con la cantidad esperada de usuarios.
- [ ] Dos usuarios pueden trabajar simultáneamente sin errores de contexto.
- [ ] La agenda y los pagos resisten intentos concurrentes.
- [ ] La restauración de un respaldo fue probada.
- [ ] La aplicación funciona en los navegadores acordados.
- [ ] La zona horaria y las fechas son correctas.
- [ ] Los logs no muestran excepciones repetidas.
- [ ] El sistema se recupera de una desconexión breve de red.

### 6.3. Capacitación y soporte

- [ ] Capacitar al administrador de la clínica.
- [ ] Capacitar a recepción en pacientes, citas y pagos.
- [ ] Capacitar a odontólogos en historia clínica, odontograma y tratamientos.
- [ ] Entregar el manual de uso o la guía operativa.
- [ ] Definir el canal de soporte y el horario.
- [ ] Definir el procedimiento para incidentes y respaldos.
- [ ] Registrar los usuarios y permisos aprobados.

### 6.4. Criterios de salida de 6C

- [ ] La clínica firmó o aprobó la aceptación.
- [ ] No quedan defectos abiertos de severidad alta o crítica.
- [ ] Los defectos menores tienen responsable y fecha.
- [ ] La capacitación fue completada.
- [ ] El soporte recibió la documentación y el runbook.
- [ ] El respaldo y la restauración fueron verificados en producción.
- [ ] El propietario autorizó el go-live comercial.

---

## 7. Secuencia recomendada

1. Completar 6A y obtener el go técnico.
2. Descargar y revisar el artefacto del pipeline.
3. Preparar la infraestructura y la base de producción.
4. Ejecutar el cutover de 6B en una ventana acordada.
5. Completar las pruebas de humo.
6. Ejecutar la aceptación 6C con usuarios reales.
7. Documentar lecciones aprendidas y pasar a soporte.

No se debe iniciar 6B si 6A tiene pendientes críticos. No se debe abrir el sistema a la clínica si 6B no pasó las pruebas de humo. No se debe cerrar la entrega si 6C no tiene aceptación.

---

## 8. Riesgos y mitigaciones

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Login de demostración activo en producción | Acceso no autorizado | Retirarlo en 6A y verificarlo en las pruebas de humo. |
| Dominio, TLS o proxy mal configurados | Indisponibilidad o bucle HTTPS | Probar en staging y revisar `AllowedHosts`, `KnownProxies` y `X-Forwarded-Proto`. |
| Migración fallida o sin respaldo | Pérdida de datos o indisponibilidad | Respaldo verificado, script revisado y DBA presente. |
| WebSocket bloqueado | Blazor reconecta o no responde | Habilitar WebSocket en IIS/nginx y probar en el navegador. |
| Zona horaria incorrecta | Citas en fecha equivocada | Configurar servidor y clínica en la misma zona. |
| Respaldos en la misma máquina | Pérdida total ante fallo del servidor | Copia externa y prueba de restauración. |
| Permisos de usuario demasiado amplios | Exposición de datos clínicos | Revisión por rol y principio de mínimo privilegio. |
| Dependencias desactualizadas | Vulnerabilidades | Revisar Dependabot y aplicar actualizaciones antes del go-live. |
| Falta de soporte definido | Incidentes sin respuesta | Acordar canal, horario y escalamiento antes de 6C. |

---

## 9. Definition of Done

La puesta en producción se considera terminada cuando:

- [ ] La versión está publicada por HTTPS en el dominio real.
- [ ] La base de producción tiene las migraciones aplicadas.
- [ ] El administrador y los usuarios reales están creados.
- [x] El login de demostración fue retirado y no hay generación automática de contraseñas en el producto.
- [ ] Los respaldos automáticos y la restauración están verificados.
- [ ] El monitoreo y los health checks están activos.
- [ ] Las pruebas de humo y la aceptación de la clínica están completas.
- [ ] La capacitación y el soporte fueron entregados.
- [ ] El propietario autorizó el go-live comercial.

---

## 10. Decisiones pendientes

- Proveedor de hosting y sistema operativo: IIS, Linux con nginx o Docker.
- Dominio definitivo y proveedor de certificados TLS.
- Responsable de SQL Server y ubicación de los respaldos externos.
- RPO, RTO y frecuencia de respaldo acordados con la clínica.
- Fecha y duración de la ventana de mantenimiento.
- Usuarios clave que participarán en la aceptación.
- Canal, horario y nivel de servicio del soporte.
- Política de actualizaciones de dependencias y manejo de Dependabot.

---

## 11. Enlaces

- [Despliegue y operación en producción](06-despliegue.md)
- [Pruebas, integración continua y respaldos](07-pruebas-y-ci.md)
- [Cambios de red, despliegue, integridad y rendimiento](CAMBIOS-RED-Y-DESPLIEGUE.md)
- [Arquitectura](05-arquitectura.md)
- [Módulos y funcionalidades](03-modulos-funcionalidades.md)
