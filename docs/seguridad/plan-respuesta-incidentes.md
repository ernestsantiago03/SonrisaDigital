# Plan de respuesta a incidentes

Este plan describe qué hacer ante una sospecha o confirmación de acceso indebido, pérdida o alteración de datos clínicos. Los comandos y pantallas citados existen en la aplicación actual.

## 1. Roles

| Rol | Responsabilidad en un incidente |
|---|---|
| Dueño de la plataforma | Coordina la respuesta, decide suspensiones y revoca accesos. |
| Operador del servidor | Ejecuta contención técnica, respaldos y restauración. |
| Administrador de la clínica afectada | Informa a pacientes y a la clínica, y conserva evidencia. |

## 2. Detección

Fuentes de alerta:
- Pantalla **Auditoría** (solo Dueño y roles con permiso): acciones inusuales, eliminaciones de archivos o cambios de roles.
- Logs de la aplicación en formato JSON, filtrables por `CorrelationId`. Buscar los mensajes `Acceso bloqueado por suscripción` y `Token antiforgery inválido`.
- Intentos fallidos de login repetidos (respuestas `invalid`, `locked`, `throttled`).
- Reporte externo a través de [SECURITY.md](../../SECURITY.md).

## 3. Contención (primeras 2 horas)

1. **Cuenta comprometida:** desactivarla con `dotnet SonrisaDigital.UI.dll desactivar-dueno <correo>` si es una cuenta del Dueño. Para otros usuarios, desactivarlos desde la pantalla **Usuarios**; esto cierra sus sesiones en el siguiente ciclo de revalidación (30 s).
2. **Segundo factor comprometido:** `dotnet SonrisaDigital.UI.dll restablecer-mfa-dueno <correo>` y volver a activarlo en el siguiente inicio de sesión.
3. **Clínica afectada:** suspenderla desde **Clínicas**. Sus sesiones abiertas se cierran en menos de 30 s.
4. **Vulnerabilidad activa en la aplicación:** detener el servicio y dejar el sitio fuera de línea hasta corregir.
5. **Evidencia:** antes de cambiar nada, respaldar la base y los logs del periodo afectado. La bitácora de auditoría no se puede modificar desde la aplicación.

## 4. Evaluación

Responder por escrito:
- ¿Qué datos se vieron, modificaron o borraron? (pacientes, historias, radiografías, pagos).
- ¿Qué clínicas y cuántos registros?
- ¿Desde cuándo y por qué medio ocurrió?
- ¿Se usó una cuenta válida o se explotó una vulnerabilidad?

## 5. Notificación

- Informar a la clínica afectada en un plazo máximo de 72 horas desde la confirmación.
- Si hubo exposición de datos de pacientes, la clínica decide la notificación a los titulares según la normativa aplicable.
- Registrar la fecha y el contenido de cada comunicación.

## 6. Recuperación

1. Corregir la causa y verificar con pruebas automatizadas (`dotnet test`) y con la suite E2E.
2. Restaurar datos desde el último respaldo válido solo si la integridad está comprometida. Probar la restauración antes de usarla en producción (`scripts/probar-restauracion-sonrisadigital.ps1`).
3. Reactivar la clínica o la cuenta solo después de verificar la corrección.
4. Forzar el cambio de contraseña de las cuentas afectadas.

## 7. Cierre

Documentar en un registro de incidente: cronología, causa raíz, impacto, acciones tomadas y cambios para evitar que se repita. Revisar el registro en la siguiente [revisión de accesos](revision-de-accesos.md).
