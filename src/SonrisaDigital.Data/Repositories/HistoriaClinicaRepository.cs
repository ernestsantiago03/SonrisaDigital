using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

/// <summary>
/// Capa de datos de la Historia Clínica. Es la única pieza que habla directamente con la
/// base de datos (a través de Entity Framework, que traduce el código C# a SQL).
///
/// Diferencia con el Service:
///  - El Repository solo "trae y guarda" datos (consultas SQL, sin reglas de negocio).
///  - El Service decide qué hacer con esos datos (validar, calcular, auditar).
///
/// Trucos de Entity Framework que se usan aquí:
///  - "AsNoTracking()": se usa cuando solo vamos a LEER. Le dice a la base de datos que no
///    vigile esos objetos para cambios, así las consultas son más rápidas.
///  - "Include(...)": trae también los datos relacionados (por ejemplo, el odontólogo de la
///    consulta) en la misma consulta, en lugar de dejarlos en null.
///
/// Orden de lectura del archivo:
///  1) Consultas de lectura  → GetPatientsAsync, GetPatientAsync, GetByPatientAsync,
///                             GetConsultasAsync, GetOdontologosAsync (lista para la ventana)
///  2) Escritura             → AddAsync, SaveChangesAsync
/// </summary>
public sealed class HistoriaClinicaRepository(ApplicationDbContext dbContext) : IHistoriaClinicaRepository
{
    // =========================================================================================
    // 1) CONSULTAS DE LECTURA  →  no cambian nada, solo traen datos
    // =========================================================================================

    /// <summary>
    /// Lista de pacientes para la búsqueda de la Historia Clínica, ordenada alfabéticamente por
    /// nombre y después por apellido. Si se recibe un texto, filtra por nombre o apellido.
    /// Se incluyen los pacientes inactivos: su historia clínica debe seguir consultable.
    /// </summary>
    public async Task<IReadOnlyList<Paciente>> GetPatientsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: empezar con la tabla de pacientes, en modo "solo lectura".
        // La variable "query" todavía NO es una consulta ejecutada: es la pregunta armada,
        // que se va completando con los pasos siguientes.
        var query = dbContext.Pacientes.AsNoTracking();

        // Paso 2: si el usuario escribió algo en el buscador, agregar el filtro.
        // Contains(...) se traduce a un SQL tipo: WHERE Nombres LIKE '%texto%'.
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(patient =>
                patient.Nombres.Contains(term) ||
                patient.Apellidos.Contains(term));
        }

        // Paso 3: ordenar alfabéticamente (primero por nombre y, a igual nombre, por apellido)
        // y ejecutar la consulta en la base de datos con ToListAsync.
        return await query
            .OrderBy(patient => patient.Nombres)
            .ThenBy(patient => patient.Apellidos)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Busca los datos generales de un paciente por su id (consulta de solo lectura).
    /// </summary>
    public Task<Paciente?> GetPatientAsync(int pacienteId, CancellationToken cancellationToken = default) =>
        // FirstOrDefaultAsync devuelve el primer paciente que coincida o null si no hay ninguno.
        dbContext.Pacientes.AsNoTracking()
            .FirstOrDefaultAsync(patient => patient.Id == pacienteId, cancellationToken);

    /// <summary>
    /// Busca la historia clínica de un paciente. Devuelve null si todavía no tiene historia.
    /// </summary>
    public Task<HistoriaClinica?> GetByPatientAsync(int pacienteId, CancellationToken cancellationToken = default) =>
        // Ojo: aquí NO se usa AsNoTracking porque este método también se usa antes de guardar
        // (el Service necesita vigilar la fila para poder modificarla).
        dbContext.HistoriasClinicas
            .FirstOrDefaultAsync(history => history.PacienteId == pacienteId, cancellationToken);

    /// <summary>
    /// Consultas clínicas del paciente, de la más reciente a la más antigua, incluyendo
    /// al odontólogo que la atendió.
    /// </summary>
    public Task<IReadOnlyList<ConsultaClinica>> GetConsultasAsync(int pacienteId, CancellationToken cancellationToken = default) =>
        dbContext.ConsultasClinicas.AsNoTracking()
            // Include: trae también la fila del odontólogo relacionado, para poder mostrar su
            // nombre sin hacer una consulta extra por cada consulta.
            .Include(consultation => consultation.Odontologo)
            .Where(consultation => consultation.PacienteId == pacienteId)
            // Las más nuevas primero; si dos son del mismo día, la de Id mayor (la última
            // registrada) va primero.
            .OrderByDescending(consultation => consultation.Fecha)
            .ThenByDescending(consultation => consultation.Id)
            .ToListAsync(cancellationToken)
            // ToListAsync devuelve una List<ConsultaClinica> y el método promete
            // IReadOnlyList<ConsultaClinica>; esto solo convierte el tipo de la lista
            // ya terminada (no cambia los datos).
            .ContinueWith(task => (IReadOnlyList<ConsultaClinica>)task.Result, cancellationToken);

    /// <summary>
    /// Una consulta clínica por su id. Aquí NO se usa AsNoTracking porque el método se usa
    /// para cerrar la consulta (el Service necesita que Entity Framework vigile la fila).
    /// </summary>
    public Task<ConsultaClinica?> GetConsultaAsync(int consultaId, CancellationToken cancellationToken = default) =>
        dbContext.ConsultasClinicas
            .FirstOrDefaultAsync(consultation => consultation.Id == consultaId, cancellationToken);

    /// <summary>
    /// La consulta ligada a una cita (la que se creó al marcarla como atendida). Como solo se
    /// usa para leer su id y saber si ya existe, va con AsNoTracking (no se va a modificar aquí).
    /// </summary>
    public Task<ConsultaClinica?> GetConsultaPorCitaAsync(int citaId, CancellationToken cancellationToken = default) =>
        dbContext.ConsultasClinicas.AsNoTracking()
            .FirstOrDefaultAsync(consultation => consultation.CitaId == citaId, cancellationToken);

    /// <summary>
    /// El id del odontólogo que corresponde a un usuario del sistema. El "(int?)" hace que,
    /// si el usuario no es odontólogo, el resultado sea null en vez de 0 (así se distingue
    /// "no es odontólogo" de "odontólogo con id 0").
    /// </summary>
    public Task<int?> GetOdontologoIdByUserAsync(string userId, CancellationToken cancellationToken = default) =>
        dbContext.Odontologos.AsNoTracking()
            .Where(dentist => dentist.UsuarioId == userId)
            .Select(dentist => (int?)dentist.Id)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Odontólogos ACTIVOS para elegir quién atendió al paciente. Se ordenan por apellidos y
    /// luego por nombres para que la lista salga alfabética.
    /// </summary>
    public async Task<IReadOnlyList<Odontologo>> GetOdontologosAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Odontologos.AsNoTracking()
            .Where(dentist => dentist.Estado == EstadoRegistro.Activo)
            .OrderBy(dentist => dentist.Apellidos)
            .ThenBy(dentist => dentist.Nombres)
            .ToListAsync(cancellationToken);

    /// <summary>Un odontólogo por su id (solo lectura).</summary>
    public Task<Odontologo?> GetOdontologoAsync(int odontologoId, CancellationToken cancellationToken = default) =>
        dbContext.Odontologos.AsNoTracking()
            .FirstOrDefaultAsync(dentist => dentist.Id == odontologoId, cancellationToken);

    // =========================================================================================
    // 2) OPERACIONES DE ESCRITURA  →  guardan cambios en la base de datos
    // =========================================================================================

    /// <summary>
    /// Prepara una historia clínica nueva para insertarla en la base de datos.
    /// El guardado real ocurre después, en SaveChangesAsync.
    /// </summary>
    public async Task AddAsync(HistoriaClinica historia, CancellationToken cancellationToken = default) =>
        // AddAsync deja la fila "en cola" para insertar; todavía no viaja a la base de datos.
        await dbContext.HistoriasClinicas.AddAsync(historia, cancellationToken);

    /// <summary>
    /// Prepara una consulta clínica nueva para insertarla en la base de datos.
    /// </summary>
    public async Task AddConsultaAsync(ConsultaClinica consulta, CancellationToken cancellationToken = default) =>
        // Igual que AddAsync: la fila queda "en cola" hasta el SaveChangesAsync.
        await dbContext.ConsultasClinicas.AddAsync(consulta, cancellationToken);

    /// <summary>
    /// Aplica en la base de datos todos los cambios que quedaron pendientes.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        // Aquí es donde de verdad se ejecutan los INSERT / UPDATE acumulados.
        dbContext.SaveChangesAsync(cancellationToken);
}
