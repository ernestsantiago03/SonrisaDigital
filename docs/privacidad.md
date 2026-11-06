# Política de privacidad — Sonrisa Digital

Última actualización: 2026-11-06

Sonrisa Digital es un sistema de gestión para clínicas odontológicas. Esta política describe cómo se tratan los datos personales y clínicos almacenados por la aplicación.

## 1. Datos que se almacenan
- Datos de identificación y contacto de pacientes, odontólogos y usuarios.
- Historias clínicas, odontogramas, consultas y tratamientos realizados.
- Radiografías, fotografías y otros archivos clínicos (JPG/PNG).
- Registros de citas, pagos y auditoría de operaciones.
- Datos de suscripción y facturación de las clínicas.

## 2. Finalidad
Los datos se utilizan únicamente para la operación de la clínica: atención odontológica, gestión administrativa, facturación y auditoría interna. No se comparten con terceros ni se usan con fines publicitarios.

## 3. Aislamiento por clínica
Cada clínica opera en su propio espacio de datos. Los filtros de clínica activa impiden que un usuario de una clínica acceda a los datos de otra. El rol Dueño administra la plataforma y solo accede a una clínica a la vez, la que elige en el selector de sesión.

## 4. Seguridad
- Comunicación cifrada mediante HTTPS y HSTS en producción.
- Contraseñas con política mínima y bloqueo por intentos fallidos.
- Segundo factor obligatorio para el rol Dueño.
- Archivos clínicos almacenados fuera de la carpeta pública, servidos únicamente tras verificar permisos y clínica.
- Respaldos cifrados con AES-256 cuando se activa `-Cifrar` en el script de respaldo (la tarea que crea `programar-respaldo.ps1` siempre lo usa). La contraseña de cifrado la custodia el operador del servidor.
- Los respaldos son responsabilidad del operador del servidor, que debe restringir su acceso.
- Auditoría de operaciones de creación, modificación y eliminación.

## 5. Retención
Los datos clínicos se conservan mientras la clínica mantenga su suscripción activa y según la normativa local aplicable. Los registros de auditoría se conservan para trazabilidad. La eliminación definitiva de una clínica y sus datos requiere intervención del Dueño.

## 6. Derechos del titular
El paciente puede solicitar consulta, corrección o eliminación de sus datos a través de la clínica que lo atiende. La clínica es responsable de gestionar esas solicitudes dentro de la aplicación.

## 7. Contacto
Para consultas sobre privacidad, contacte al administrador de su clínica o al Dueño de la plataforma.
