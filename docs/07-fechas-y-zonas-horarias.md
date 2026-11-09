# Fechas y zonas horarias

Sonrisa Digital distingue dos tipos de dato temporal. Mezclarlos es la causa más
común de que un pago o una consulta aparezcan en el día equivocado.

## 1. Fecha de calendario (`DateOnly`)

Representa un día completo, sin hora: “el pago fue el 4 de noviembre”. No tiene
zona horaria y no se convierte a UTC. En SQL Server se guarda como `date`.

| Dato | Columna |
|---|---|
| Cita | `Citas.Fecha` |
| Consulta clínica | `ConsultasClinicas.Fecha` |
| Pago de tratamiento | `Pagos.Fecha` |
| Tratamiento realizado | `TratamientosRealizados.Fecha` |
| Pago de suscripción | `PagosSuscripcion.FechaPago`, `PeriodoDesde`, `PeriodoHasta` |
| Nacimiento del paciente | `Pacientes.FechaNacimiento` |
| Fecha del estudio | `ArchivosPaciente.FechaEstudio` |

Los reportes comparan estas columnas contra el rango elegido tal cual
(`fecha >= desde AND fecha < hastaExclusivo`). No hay conversión de zona que
pueda mover el registro al día anterior o siguiente.

## 2. Instante real (`DateTime` en UTC)

Representa un momento exacto y sí debe guardarse en UTC:

- `Paciente.FechaRegistro`
- `HistoriaClinica.FechaCreacion` y `FechaModificacion`
- `ArchivoPaciente.FechaSubida`
- `RegistroAuditoria.FechaHora`
- `PagoSuscripcion.FechaRegistro`

Para contar estos datos por día del negocio, el rango local se convierte a UTC
con `RelojNegocio.AUtc`. Por ejemplo, en República Dominicana (UTC-4), el día
local 5 de noviembre abarca desde `2026-11-05T04:00:00Z` hasta
`2026-11-06T04:00:00Z` (extremo excluido).

## 3. `RelojNegocio`

`RelojNegocio` es la única fuente de la fecha actual del negocio:

- `Ahora()`: momento actual en la zona del negocio.
- `Hoy()` y `HoyDateOnly()`: fecha actual sin hora.
- `AUtc(...)`: interpreta una fecha sin marca como hora local del negocio.
- `DeUtc(...)`: convierte un instante UTC a la hora local para mostrarlo.

La zona se configura al arrancar:

```json
"RelojNegocio": {
  "ZonaHoraria": "America/Santo_Domingo"
}
```

También acepta el identificador de Windows (`SA Western Standard Time`);
`RelojNegocio` traduce automáticamente entre ambos formatos. Si el servidor no
reconoce el valor, la aplicación **no arranca** y muestra el error en consola.
No se cae a UTC en silencio, porque eso guardaría fechas con el día equivocado.

Para pruebas existen sobrecargas que reciben un `TimeProvider`:

```csharp
var reloj = new RelojFijo(DateTimeOffset.Parse("2026-11-05T02:30:00Z"));
var diaLocal = RelojNegocio.HoyDateOnly(reloj); // 2026-11-04 en Santo Domingo
```

## 4. Validaciones de negocio

- Un pago de tratamiento no puede tener fecha futura.
- Una consulta clínica no puede tener fecha futura.
- Un reporte no puede abarcar más de **366 días** de diferencia entre la fecha
  inicial y la final.
- Un pago de suscripción toma como fecha de pago el día local del negocio; el
  período se calcula con `DateOnly` y solo al guardar en `Clinica` se convierte
  al inicio de ese día.

## 5. Verificación en una base ya desplegada

```sql
-- Las columnas deben ser de tipo date, no datetime2.
SELECT c.name AS columna, t.name AS tipo
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id IN (OBJECT_ID('Pagos'), OBJECT_ID('ConsultasClinicas'),
                      OBJECT_ID('PagosSuscripcion'))
  AND c.name IN ('Fecha', 'FechaPago', 'PeriodoDesde', 'PeriodoHasta');

-- Debe aparecer la migración aplicada.
SELECT MigrationId
FROM __EFMigrationsHistory
WHERE MigrationId LIKE '%FechasDateOnlyConsultaYPagoSuscripcion%';
```

## 6. Pruebas de regresión

- `RelojNegocioTests` fija conversiones UTC, cambios de día y configuración
  inválida.
- `ReporteFechasTests` comprueba pagos en los límites del rango, pacientes
  nuevos en la medianoche local y el límite máximo de 366 días.
- `PagoServiceTests` rechaza pagos con fecha futura.
- El CI ejecuta toda la suite dos veces: con `TZ=UTC` y con
  `TZ=America/Santo_Domingo`, para detectar cualquier dependencia de la zona
  horaria del servidor.
