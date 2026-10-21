using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.HistoriasClinicas.DTOs;

namespace SonrisaDigital.Services.HistoriasClinicas;

/// <summary>
/// Lógica (el "cerebro") del módulo de Historia Clínica.
///
/// ¿Qué hace este archivo?
/// Atiende los pedidos que le manda la pantalla de Historia Clínica —buscar pacientes, abrir
/// la historia de uno, guardar los datos clínicos y manejar sus consultas— y conversa con la
/// base de datos. Es la capa del medio de la aplicación:
///
///     Pantalla (UI)  →  Service (este archivo)  →  Repository  →  Base de datos
///
/// Reglas que se cumplen aquí:
///  - Los datos clínicos (alergias, enfermedades, antecedentes, hábitos...) se guardan en la
///    tabla "HistoriasClinicas". Los datos administrativos del paciente (nombre, teléfono,
///    tipo de sangre...) se leen de la tabla "Pacientes" y no se duplican.
///  - Nunca se crea una historia vacía: la fila se crea solo cuando hay algún dato que guardar.
///    (Excepción: registrar una consulta sí crea la historia, porque la consulta ya es un dato
///    clínico y no puede existir sin ella.)
///  - Si el usuario guarda sin cambiar nada, no se marca la fecha de modificación ni se
///    escribe en la auditoría.
///  - Una consulta nace Abierta y se completa después; una vez Cerrada queda de solo lectura.
///
/// Orden de lectura del archivo:
///  1) Buscar/listar pacientes    → GetPatientsAsync
///  2) Abrir la historia de uno   → GetByPatientAsync
///  3) Guardar los datos clínicos → SaveAsync
///  4) Manejar las consultas      → GetOdontologosAsync, GetOdontologoVinculadoAsync,
///                                  CreateConsultaAsync, UpdateConsultaAsync,
///                                  RegistrarConsultaDeCitaAsync (puente con Citas), CerrarConsultaAsync
///  5) Ayudantes privados         → ObtenerOCrearHistoriaAsync, EstaVacia, Apply, Normalize, ValidarConsulta
/// </summary>
public sealed class HistoriaClinicaService(
    IHistoriaClinicaRepository repository,
    IAuditoriaService auditoriaService) : IHistoriaClinicaService
{
    // =========================================================================================
    // 1) BUSCAR / LISTAR PACIENTES  →  alimenta el panel de búsqueda de la izquierda
    // =========================================================================================

    /// <summary>
    /// Lista de pacientes para el panel de búsqueda de la Historia Clínica. Con un texto de
    /// búsqueda filtra por nombre o apellido; sin texto devuelve todos los pacientes.
    /// Incluye también a los pacientes inactivos, porque su historia debe seguir consultable.
    /// </summary>
    public async Task<IReadOnlyList<HistoriaClinicaPacienteDto>> GetPatientsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: pedirle los pacientes al repositorio. Él es el único que habla con SQL y ya
        // devuelve la lista ordenada alfabéticamente por nombre y apellido.
        var patients = await repository.GetPatientsAsync(searchTerm, cancellationToken);

        // Paso 2: convertir cada paciente de la base de datos al objeto liviano que usa la
        // pantalla (DTO). Solo se mandan los 4 datos que el panel necesita mostrar.
        return patients
            .Select(patient => new HistoriaClinicaPacienteDto(
                patient.Id,                                        // para saber a quién abrir al hacer clic
                $"{patient.Nombres} {patient.Apellidos}".Trim(),   // nombre completo visible
                patient.Estado == EstadoRegistro.Activo,           // ¿activo o inactivo? (pinta la etiqueta)
                patient.Identificacion))                           // cédula / documento
            .ToList();
    }

    // =========================================================================================
    // 2) ABRIR LA HISTORIA DE UN PACIENTE  →  junta datos personales + clínicos + consultas
    // =========================================================================================

    /// <summary>
    /// Arma la historia clínica completa de un paciente: sus datos generales (que vienen de la
    /// tabla de pacientes) más los datos clínicos y la lista de consultas atendidas.
    /// Devuelve null si el paciente no existe.
    /// </summary>
    public async Task<HistoriaClinicaDto?> GetByPatientAsync(int pacienteId, CancellationToken cancellationToken = default)
    {
        // Paso 1: confirmar que el paciente existe. Si no existe se devuelve null y la pantalla
        // muestra el mensaje de "paciente no encontrado".
        var patient = await repository.GetPatientAsync(pacienteId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        // Paso 2: traer las dos partes de la historia:
        //   - la fila clínica (puede ser null si el paciente todavía no tiene historia), y
        //   - la lista de consultas que se le han atendido.
        var historia = await repository.GetByPatientAsync(pacienteId, cancellationToken);
        var consultas = await repository.GetConsultasAsync(pacienteId, cancellationToken);

        // Paso 3: armar el paquete final (DTO) que la pantalla va a pintar.
        return new HistoriaClinicaDto
        {
            // --- Datos del paciente (vienen de la tabla Pacientes) ---
            PacienteId = patient.Id,
            PacienteNombre = $"{patient.Nombres} {patient.Apellidos}".Trim(),
            Identificacion = patient.Identificacion,
            FechaNacimiento = patient.FechaNacimiento,
            Telefono = patient.Telefono,
            TipoSangre = patient.TipoSangre,
            PacienteActivo = patient.Estado == EstadoRegistro.Activo,

            // --- Datos de la historia (vienen de la tabla HistoriasClinicas) ---
            // El "?." significa: si no hay historia, deja el campo vacío en vez de fallar.
            TieneHistoria = historia is not null,
            FechaCreacion = historia?.FechaCreacion,
            FechaModificacion = historia?.FechaModificacion,
            Alergias = historia?.Alergias,
            Enfermedades = historia?.Enfermedades,
            Medicamentos = historia?.Medicamentos,
            AntecedentesMedicos = historia?.AntecedentesMedicos,
            AntecedentesOdontologicos = historia?.AntecedentesOdontologicos,
            Habitos = historia?.Habitos,
            Observaciones = historia?.Observaciones,

            // --- Consultas atendidas (tabla ConsultasClinicas) ---
            Consultas = consultas
                .Select(consulta => new ConsultaClinicaItemDto(
                    consulta.Id,
                    consulta.Fecha,
                    consulta.OdontologoId,
                    $"{consulta.Odontologo.Nombres} {consulta.Odontologo.Apellidos}".Trim(),
                    consulta.Motivo,
                    consulta.Diagnostico,
                    consulta.PlanTratamiento,
                    consulta.Observaciones,
                    consulta.CitaId,
                    consulta.Estado))
                .ToList()
        };
    }

    // =========================================================================================
    // 3) GUARDAR LOS DATOS CLÍNICOS  →  el botón "Guardar historia clínica"
    // =========================================================================================

    /// <summary>
    /// Guarda los datos clínicos de la historia (alergias, enfermedades, antecedentes, etc.).
    /// Si el paciente todavía no tenía historia, la crea únicamente cuando hay algún dato que
    /// guardar (no se crean expedientes vacíos) y deja el registro en la auditoría.
    /// </summary>
    public async Task SaveAsync(HistoriaClinicaDto model, CancellationToken cancellationToken = default)
    {
        // Paso 1: verificar que el paciente existe. Si no, se corta aquí con un mensaje claro.
        var patient = await repository.GetPatientAsync(model.PacienteId, cancellationToken)
            ?? throw new InvalidOperationException("El paciente no existe.");

        // Paso 2: ver si este paciente ya tenía historia clínica o es la primera vez.
        var historia = await repository.GetByPatientAsync(model.PacienteId, cancellationToken);
        var esNueva = historia is null;

        // Paso 3: si es nueva, preparar la fila.
        if (historia is null)
        {
            // Igual que en el formulario de pacientes: la historia clínica solo se crea
            // cuando hay un dato clínico que registrar (nada de expedientes vacíos).
            if (EstaVacia(model))
            {
                return;
            }

            historia = new HistoriaClinica
            {
                PacienteId = model.PacienteId,
                FechaCreacion = DateTime.UtcNow
            };

            // Se marca la fila para insertarla cuando se guarde (todavía no se ha grabado).
            await repository.AddAsync(historia, cancellationToken);
        }

        // Paso 4: copiar los datos del formulario a la fila. "Apply" dice si algo cambió
        // de verdad comparando dato por dato.
        var huboCambios = Apply(historia, model);

        // Si la historia ya existía y el usuario no cambió nada, se termina sin tocar la
        // base de datos (ni fecha de modificación, ni auditoría, ni escritura).
        if (!esNueva && !huboCambios)
        {
            return;
        }

        // Paso 5: sellar la fecha de modificación y grabar definitivamente en la base de datos.
        historia.FechaModificacion = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        // Paso 6: dejar el rastro en la auditoría (quién hizo qué y cuándo).
        // El verbo cambia según sea la primera vez ("Agregó") o una actualización ("Modificó").
        var nombrePaciente = $"{patient.Nombres} {patient.Apellidos}".Trim();
        await auditoriaService.RegistrarAsync(
            esNueva ? "Agregó" : "Modificó",
            "Historia Clínica",
            nameof(HistoriaClinica),
            historia.Id,
            esNueva
                ? $"Registró la historia clínica del paciente: {nombrePaciente}"
                : $"Actualizó la historia clínica del paciente: {nombrePaciente}",
            cancellationToken);
    }

    // =========================================================================================
    // 4) CONSULTAS CLÍNICAS  →  el botón "+ Nueva consulta" y el botón "Cerrar"
    // =========================================================================================

    /// <summary>
    /// Odontólogos activos para elegir quién atendió al paciente (lista desplegable de
    /// "+ Nueva consulta"), ordenados por nombre.
    /// </summary>
    public async Task<IReadOnlyList<OdontologoOptionDto>> GetOdontologosAsync(CancellationToken cancellationToken = default)
    {
        // Paso 1: traer los odontólogos activos desde el repositorio.
        var odontologos = await repository.GetOdontologosAsync(cancellationToken);

        // Paso 2: convertirlos al DTO pequeño que usa la lista (id + nombre completo).
        return odontologos
            .Select(dentist => new OdontologoOptionDto(
                dentist.Id,
                $"{dentist.Nombres} {dentist.Apellidos}".Trim()))
            .ToList();
    }

    /// <summary>
    /// El odontólogo ligado al usuario de la sesión, o null si su usuario no es odontólogo.
    /// Sirve para preseleccionar al profesional en "+ Nueva consulta".
    /// </summary>
    public async Task<int?> GetOdontologoVinculadoAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Sin usuario identificado no hay nada que preseleccionar.
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await repository.GetOdontologoIdByUserAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Registra una consulta clínica del paciente (fecha, motivo, diagnóstico, plan de
    /// tratamiento y observaciones). La firma el odontólogo dueño de la sesión.
    ///
    /// Detalle importante: la tabla de consultas necesita una historia clínica a la que
    /// pertenecer (su columna HistoriaClinicaId es obligatoria). Por eso, si el paciente
    /// todavía no tiene historia, aquí SÍ se crea una: la consulta ya es un dato clínico,
    /// no es una historia "vacía" inventada.
    /// </summary>
    public async Task CreateConsultaAsync(
        ConsultaClinicaFormDto model,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: comprobar que el paciente existe. Si no, se corta con un mensaje claro.
        var paciente = await repository.GetPatientAsync(model.PacienteId, cancellationToken)
            ?? throw new InvalidOperationException("El paciente no existe.");

        // Paso 2: comprobar el odontólogo elegido como tratante. El permiso del rol es lo que da
        // acceso a registrar consultas; el profesional se elige en la ventana (la consulta queda
        // firmada por él), así que aquí solo se valida que exista y siga activo.
        var odontologo = await repository.GetOdontologoAsync(model.OdontologoId, cancellationToken)
            ?? throw new InvalidOperationException("El odontólogo seleccionado no existe.");

        if (odontologo.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("El odontólogo seleccionado está inactivo.");
        }

        // Paso 3: validar los datos (por si llegan saltándose el formulario).
        ValidarConsulta(model);

        // Paso 4: conseguir la historia clínica del paciente. Si no tiene, se crea ahora,
        // porque la consulta no puede existir sin ella (HistoriaClinicaId es obligatorio).
        var historia = await ObtenerOCrearHistoriaAsync(model.PacienteId, cancellationToken);

        // Paso 5: armar la consulta con los datos ya limpios. Nace en estado "Abierta".
        var consulta = new ConsultaClinica
        {
            PacienteId = model.PacienteId,
            HistoriaClinicaId = historia.Id,
            OdontologoId = odontologo.Id,
            // La fecha de una consulta es un día de calendario, no una hora exacta.
            Fecha = model.Fecha.Date,
            Motivo = Normalize(model.Motivo),
            Diagnostico = Normalize(model.Diagnostico),
            PlanTratamiento = Normalize(model.PlanTratamiento),
            Observaciones = Normalize(model.Observaciones),
            Estado = EstadoConsulta.Abierta
        };

        // Paso 6: guardar la consulta en la base de datos.
        await repository.AddConsultaAsync(consulta, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // Paso 7: dejar el rastro en la auditoría (quién registró la consulta y de quién).
        var nombrePaciente = $"{paciente.Nombres} {paciente.Apellidos}".Trim();
        await auditoriaService.RegistrarAsync(
            "Agregó",
            "Historia Clínica",
            nameof(ConsultaClinica),
            consulta.Id,
            $"Registró una consulta clínica del paciente: {nombrePaciente}",
            cancellationToken);
    }

    /// <summary>
    /// Completa o corrige una consulta que todavía está Abierta (por ejemplo, la que se creó
    /// automáticamente al atender una cita y a la que le falta el diagnóstico).
    /// Una consulta cerrada ya es de solo lectura y el servicio la rechaza. Deja el cambio
    /// anotado en la auditoría.
    /// </summary>
    public async Task UpdateConsultaAsync(
        ConsultaClinicaFormDto model,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: buscar la consulta que se quiere modificar. Si no existe, se avisa.
        var consulta = await repository.GetConsultaAsync(model.Id, cancellationToken)
            ?? throw new InvalidOperationException("La consulta no existe.");

        // Paso 2: solo se puede modificar mientras siga Abierta. El diseño del módulo dice que
        // una consulta terminada (cerrada) queda de solo lectura.
        if (consulta.Estado != EstadoConsulta.Abierta)
        {
            throw new InvalidOperationException("Una consulta cerrada ya no se puede modificar.");
        }

        // Paso 3: comprobar el odontólogo elegido como tratante (puede corregirse).
        var odontologo = await repository.GetOdontologoAsync(model.OdontologoId, cancellationToken)
            ?? throw new InvalidOperationException("El odontólogo seleccionado no existe.");

        if (odontologo.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("El odontólogo seleccionado está inactivo.");
        }

        // Paso 4: validar los datos (por si llegan saltándose el formulario).
        ValidarConsulta(model);

        // Paso 5: copiar los datos limpios a la fila que Entity Framework ya está vigilando.
        consulta.OdontologoId = odontologo.Id;
        consulta.Fecha = model.Fecha.Date;
        consulta.Motivo = Normalize(model.Motivo);
        consulta.Diagnostico = Normalize(model.Diagnostico);
        consulta.PlanTratamiento = Normalize(model.PlanTratamiento);
        consulta.Observaciones = Normalize(model.Observaciones);

        // Paso 6: guardar.
        await repository.SaveChangesAsync(cancellationToken);

        // Paso 7: dejar el rastro en la auditoría.
        var paciente = await repository.GetPatientAsync(consulta.PacienteId, cancellationToken);
        var nombrePaciente = paciente is null
            ? "paciente"
            : $"{paciente.Nombres} {paciente.Apellidos}".Trim();

        await auditoriaService.RegistrarAsync(
            "Modificó",
            "Historia Clínica",
            nameof(ConsultaClinica),
            consulta.Id,
            $"Completó o corrigió una consulta clínica del paciente: {nombrePaciente}",
            cancellationToken);
    }

    /// <summary>
    /// Puente entre Citas e Historia Clínica: registra la consulta clínica de una cita que se
    /// acaba de marcar como Atendida.
    ///
    /// La consulta hereda los datos de la cita (paciente, odontólogo, fecha y motivo) y nace en
    /// estado Abierta: los campos clínicos (diagnóstico y plan) los completa después el
    /// odontólogo desde la pantalla de Historia Clínica.
    ///
    /// Nunca hay dos consultas para la misma cita: si ya existía, se devuelve la que estaba.
    /// Devuelve el id de la consulta.
    /// </summary>
    public async Task<int> RegistrarConsultaDeCitaAsync(
        Cita cita,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: si esa cita ya tiene su consulta, se devuelve y no se crea otra (así, si el
        // usuario vuelve a marcar la misma cita como atendida, no se duplica nada).
        var existente = await repository.GetConsultaPorCitaAsync(cita.Id, cancellationToken);
        if (existente is not null)
        {
            return existente.Id;
        }

        // Paso 2: conseguir la historia clínica del paciente (se crea si todavía no tiene:
        // la consulta necesita una historia a la que pertenecer).
        var historia = await ObtenerOCrearHistoriaAsync(cita.PacienteId, cancellationToken);

        // Paso 3: armar la consulta con los datos de la cita. Como la cita puede no tener motivo,
        // el campo se guarda tal como venga (vacío) y el odontólogo lo completa después.
        var consulta = new ConsultaClinica
        {
            PacienteId = cita.PacienteId,
            HistoriaClinicaId = historia.Id,
            OdontologoId = cita.OdontologoId,
            CitaId = cita.Id,
            // La cita guarda un día de calendario y la consulta guarda fecha con hora:
            // se convierte al comienzo de ese día.
            Fecha = cita.Fecha.ToDateTime(TimeOnly.MinValue),
            Motivo = Normalize(cita.Motivo),
            Estado = EstadoConsulta.Abierta
        };

        // Paso 4: guardar la consulta.
        await repository.AddConsultaAsync(consulta, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // Paso 5: dejar el rastro en la auditoría.
        var nombrePaciente = $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}".Trim();
        await auditoriaService.RegistrarAsync(
            "Agregó",
            "Historia Clínica",
            nameof(ConsultaClinica),
            consulta.Id,
            $"Registró la consulta clínica de la cita atendida del paciente: {nombrePaciente}",
            cancellationToken);

        return consulta.Id;
    }

    /// <summary>
    /// Cierra una consulta clínica: pasa de Abierta a Cerrada y a partir de ahí queda de solo
    /// lectura (así lo pide el diseño del módulo). Deja el cambio anotado en la auditoría.
    /// </summary>
    public async Task CerrarConsultaAsync(int consultaId, CancellationToken cancellationToken = default)
    {
        // Paso 1: buscar la consulta. Si no existe, se avisa.
        var consulta = await repository.GetConsultaAsync(consultaId, cancellationToken)
            ?? throw new InvalidOperationException("La consulta no existe.");

        // Paso 2: si ya estaba cerrada, no hay nada que hacer (y no se ensucia la auditoría).
        if (consulta.Estado == EstadoConsulta.Cerrada)
        {
            return;
        }

        // Paso 3: cambiar el estado y guardar.
        consulta.Estado = EstadoConsulta.Cerrada;
        await repository.SaveChangesAsync(cancellationToken);

        // Paso 4: dejar el rastro en la auditoría.
        await auditoriaService.RegistrarAsync(
            "Modificó",
            "Historia Clínica",
            nameof(ConsultaClinica),
            consulta.Id,
            "Cerró una consulta clínica.",
            cancellationToken);
    }

    // =========================================================================================
    // 5) AYUDANTES PRIVADOS  →  piezas pequeñas que usa el resto del archivo (el usuario no las ve)
    // =========================================================================================

    /// <summary>
    /// Devuelve la historia clínica del paciente y, si todavía no tiene ninguna, la crea y la
    /// graba en ese momento. Se usa al registrar una consulta (tanto a mano como automáticamente
    /// desde una cita atendida): la consulta necesita una historia a la que pertenecer.
    /// </summary>
    private async Task<HistoriaClinica> ObtenerOCrearHistoriaAsync(int pacienteId, CancellationToken cancellationToken)
    {
        // Paso 1: buscar si el paciente ya tiene historia clínica.
        var historia = await repository.GetByPatientAsync(pacienteId, cancellationToken);

        // Paso 2: si ya existe, se devuelve tal cual.
        if (historia is not null)
        {
            return historia;
        }

        // Paso 3: si no existe, se prepara la fila y se graba de una vez. Se graba aquí mismo
        // para que la historia ya tenga Id (la consulta lo necesita para apuntarle con su
        // clave foránea).
        historia = new HistoriaClinica
        {
            PacienteId = pacienteId,
            FechaCreacion = DateTime.UtcNow
        };
        await repository.AddAsync(historia, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return historia;
    }

    /// <summary>
    /// Indica si el formulario clínico viene completamente vacío. Se usa para no crear una
    /// fila de historia clínica cuando todavía no hay ningún dato que registrar.
    /// </summary>
    private static bool EstaVacia(HistoriaClinicaDto model) =>
        // Si TODOS estos campos están vacíos, el formulario está vacío.
        // IsNullOrWhiteSpace trata igual "" (vacío) y "   " (solo espacios).
        string.IsNullOrWhiteSpace(model.Alergias)
        && string.IsNullOrWhiteSpace(model.Enfermedades)
        && string.IsNullOrWhiteSpace(model.Medicamentos)
        && string.IsNullOrWhiteSpace(model.AntecedentesMedicos)
        && string.IsNullOrWhiteSpace(model.AntecedentesOdontologicos)
        && string.IsNullOrWhiteSpace(model.Habitos)
        && string.IsNullOrWhiteSpace(model.Observaciones);

    /// <summary>
    /// Copia los datos del formulario a la entidad que se guarda en la base de datos y
    /// devuelve true si algo cambió de verdad. Así, si el usuario guarda sin cambiar nada,
    /// no se marca la fecha de modificación ni se escribe en la auditoría.
    /// </summary>
    private static bool Apply(HistoriaClinica historia, HistoriaClinicaDto model)
    {
        // Paso 1: limpiar lo que llegó del formulario (quitar espacios y convertir "" en null).
        var alergias = Normalize(model.Alergias);
        var enfermedades = Normalize(model.Enfermedades);
        var medicamentos = Normalize(model.Medicamentos);
        var antecedentesMedicos = Normalize(model.AntecedentesMedicos);
        var antecedentesOdontologicos = Normalize(model.AntecedentesOdontologicos);
        var habitos = Normalize(model.Habitos);
        var observaciones = Normalize(model.Observaciones);

        // Paso 2: comparar lo nuevo contra lo que ya estaba guardado para saber si hubo
        // algún cambio real (antes de sobrescribir nada).
        var huboCambios =
            alergias != historia.Alergias
            || enfermedades != historia.Enfermedades
            || medicamentos != historia.Medicamentos
            || antecedentesMedicos != historia.AntecedentesMedicos
            || antecedentesOdontologicos != historia.AntecedentesOdontologicos
            || habitos != historia.Habitos
            || observaciones != historia.Observaciones;

        // Paso 3: ahora sí, escribir los valores limpios en la entidad.
        historia.Alergias = alergias;
        historia.Enfermedades = enfermedades;
        historia.Medicamentos = medicamentos;
        historia.AntecedentesMedicos = antecedentesMedicos;
        historia.AntecedentesOdontologicos = antecedentesOdontologicos;
        historia.Habitos = habitos;
        historia.Observaciones = observaciones;

        return huboCambios;
    }

    /// <summary>
    /// Limpia un texto antes de guardarlo: le quita los espacios de los extremos y convierte
    /// el texto vacío en null (así "sin dato" queda realmente vacío en la base de datos).
    /// </summary>
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Revisa lo esencial de una consulta antes de guardarla: que tenga motivo y diagnóstico,
    /// y que la fecha no sea futura (una consulta registra una atención que ya ocurrió).
    /// Se valida aquí, y no solo en el formulario, porque las reglas de negocio deben vivir
    /// fuera de la interfaz.
    /// </summary>
    private static void ValidarConsulta(ConsultaClinicaFormDto model)
    {
        if (string.IsNullOrWhiteSpace(model.Motivo))
        {
            throw new InvalidOperationException("El motivo de la consulta es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(model.Diagnostico))
        {
            throw new InvalidOperationException("El diagnóstico de la consulta es obligatorio.");
        }

        if (model.Fecha.Date > DateTime.Today)
        {
            throw new InvalidOperationException("La fecha de la consulta no puede ser futura.");
        }
    }
}
