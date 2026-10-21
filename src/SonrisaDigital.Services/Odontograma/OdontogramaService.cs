using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Odontograma.DTOs;

namespace SonrisaDigital.Services.Odontograma;

/// <summary>
/// Lógica (el "cerebro") del módulo de Odontograma.
///
/// ¿Qué hace este archivo?
/// Atiende los pedidos de la pantalla del odontograma: buscar pacientes para el panel,
/// leer las 32 piezas dentales de un paciente con su estado, y guardar el estado de una pieza.
/// Es la capa del medio de la aplicación:
///
///     Pantalla (UI)  →  Service (este archivo)  →  Repository  →  Base de datos
///
/// Idea clave para entender el odontograma:
///  - Las 32 piezas existen SIEMPRE en la tabla "PiezasDentales" (es un catálogo fijo).
///  - Lo que cambia por paciente es la tabla "RegistrosOdontograma": una fila por cada pieza
///    que alguien ha tocado (con su estado: sano, caries, etc.).
///  - Al mostrar el odontograma se cruzan las dos: para cada pieza se busca si el paciente
///    tiene registro; si no lo tiene, la pieza se pinta como sana y sin datos.
///
/// Orden de lectura del archivo:
///  1) Buscar pacientes                     → GetActivePatientsAsync
///  2) Leer las 32 piezas de un paciente    → GetByPatientAsync
///  3) Guardar el estado de una pieza       → SaveAsync
/// </summary>
public sealed class OdontogramaService(
    IOdontogramaRepository repository,
    IPacienteRepository pacienteRepository) : IOdontogramaService
{
    // =========================================================================================
    // 1) BUSCAR PACIENTES  →  alimenta el panel de búsqueda de la pantalla del odontograma
    // =========================================================================================

    /// <summary>
    /// Pacientes activos para el panel de búsqueda del odontograma. Con un texto de búsqueda
    /// filtra por nombre o apellido; sin texto devuelve todos, en orden alfabético.
    /// </summary>
    public async Task<IReadOnlyList<OdontogramaPacienteDto>> GetActivePatientsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: pedirle al repositorio los pacientes ACTIVOS que coincidan con el texto.
        // (Solo activos: a un paciente desactivado no se le sigue trabajando el odontograma.)
        var patients = await repository.GetActivePatientsAsync(searchTerm, cancellationToken);

        // Paso 2: quedarse solo con lo que la pantalla necesita: id y nombre completo.
        return patients.Select(patient => new OdontogramaPacienteDto(patient.Id, $"{patient.Nombres} {patient.Apellidos}")).ToList();
    }

    // =========================================================================================
    // 2) LEER LAS 32 PIEZAS DE UN PACIENTE  →  dibuja el odontograma completo
    // =========================================================================================

    /// <summary>
    /// Las 32 piezas dentales con el estado que tienen para ese paciente. Las piezas que el
    /// paciente todavía no tiene registradas vienen "sanas" y sin observaciones.
    /// </summary>
    public async Task<IReadOnlyList<OdontogramaPiezaDto>> GetByPatientAsync(int pacienteId, CancellationToken cancellationToken = default)
    {
        // Paso 1: traer las dos listas por separado.
        //   pieces  = el catálogo de las 32 piezas dentales (siempre las mismas).
        //   records = los registros que ESTE paciente tiene (solo las piezas que se han tocado).
        var pieces = await repository.GetPiecesAsync(cancellationToken);
        var records = (await repository.GetRecordsAsync(pacienteId, cancellationToken)).ToDictionary(record => record.PiezaDentalId);

        // Paso 2: cruzar las dos listas. Se recorre el catálogo de las 32 piezas y, para cada
        // una, se busca su registro con el diccionario (que es rápido porque está indexado
        // por el id de la pieza).
        return pieces.Select(piece => records.TryGetValue(piece.Id, out var record)
            // Caso A: el paciente SÍ tiene registro de esta pieza → se usa su estado real.
            ? new OdontogramaPiezaDto
            {
                Id = piece.Id,
                NumeroFDI = piece.NumeroFDI,
                Nombre = piece.Nombre,
                Estado = record.Estado,
                Observaciones = record.Observaciones,
                FechaModificacion = record.FechaModificacion,
                Odontologo = $"{record.Odontologo.Nombres} {record.Odontologo.Apellidos}"
            }
            // Caso B: no hay registro → la pieza se muestra sin estado (sana, sin datos).
            : new OdontogramaPiezaDto { Id = piece.Id, NumeroFDI = piece.NumeroFDI, Nombre = piece.Nombre })
            .ToList();
    }

    // =========================================================================================
    // 3) GUARDAR EL ESTADO DE UNA PIEZA  →  cuando el odontólogo hace clic en un diente
    // =========================================================================================

    /// <summary>
    /// Cambia el estado de una pieza dental del paciente (sano, caries, restauración, etc.).
    /// Si es la primera vez que se toca esa pieza, crea el registro; si ya existía, lo actualiza.
    /// Deja el movimiento anotado en la auditoría.
    /// </summary>
    public async Task SaveAsync(int pacienteId, int piezaDentalId, EstadoOdontograma estado, string? observaciones, string userId, CancellationToken cancellationToken = default)
    {
        // Paso 1: saber QUÉ odontólogo es el usuario que está en sesión (queda guardado como
        // el responsable del cambio). Si su usuario no está ligado a un odontólogo, se corta.
        var odontologoId = await repository.GetOdontologoIdByUserAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("El usuario actual no está asociado a un odontólogo.");

        // Paso 2: buscar si ya existe un registro de esa pieza para ese paciente.
        var record = await repository.GetRecordAsync(pacienteId, piezaDentalId, cancellationToken);

        // Paso 3: si no existe, se crea la fila nueva (primera vez que se toca la pieza).
        if (record is null)
        {
            record = new RegistroOdontograma
            {
                PacienteId = pacienteId,
                PiezaDentalId = piezaDentalId,
                OdontologoId = odontologoId
            };
            await repository.AddAsync(record, cancellationToken);
        }

        // Paso 4: escribir los datos nuevos en la fila (sirve igual para la nueva y para la
        // que ya existía). Las observaciones vacías se guardan como null.
        record.Estado = estado;
        record.Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
        record.OdontologoId = odontologoId;
        record.FechaModificacion = DateTime.UtcNow;

        // Paso 5: conseguir los nombres para el texto de la auditoría (paciente y pieza).
        var paciente = await pacienteRepository.GetByIdAsync(pacienteId, cancellationToken);
        var pieza = (await repository.GetPiecesAsync(cancellationToken)).FirstOrDefault(item => item.Id == piezaDentalId);

        // Si por alguna razón no se encuentra el nombre, se usa el id para que el texto
        // de auditoría nunca quede vacío.
        var nombrePaciente = paciente is null
            ? $"paciente {pacienteId}"
            : $"{paciente.Nombres} {paciente.Apellidos}".Trim();
        var numeroPieza = string.IsNullOrWhiteSpace(pieza?.NumeroFDI) ? piezaDentalId.ToString() : pieza!.NumeroFDI;

        // Paso 6: guardar el registro y la auditoría JUNTOS en una sola operación.
        // Así queda la garantía de que, si algo falla, no se guarda el cambio sin su rastro.
        await repository.SaveChangesWithAuditAsync(new RegistroAuditoria
        {
            UsuarioId = userId,
            Accion = "Modificó",
            Modulo = "Odontograma",
            Entidad = nameof(RegistroOdontograma),
            EntidadId = record.Id,
            Descripcion = $"Actualizó la pieza dental {numeroPieza} del paciente {nombrePaciente}."
        }, cancellationToken);
    }
}
