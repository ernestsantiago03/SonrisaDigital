using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Common;
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
    public async Task<PagedResult<Paciente>> SearchPatientsPagedAsync(
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

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

        // Paso 3: contar en SQL y ajustar la página si el filtro dejó menos resultados.
        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        // Paso 4: ordenar alfabéticamente (el Id desempata) y traer solo la página pedida.
        var items = await query
            .OrderBy(patient => patient.Nombres)
            .ThenBy(patient => patient.Apellidos)
            .ThenBy(patient => patient.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Paciente>(items, total, page, pageSize);
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
            .Include(consultation => consultation.PlanTratamientoItems)
                .ThenInclude(item => item.Tratamiento)
            .Include(consultation => consultation.PlanTratamientoItems)
                .ThenInclude(item => item.PiezaDental)
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
            .Include(consultation => consultation.PlanTratamientoItems)
            .FirstOrDefaultAsync(consultation => consultation.Id == consultaId, cancellationToken);

    /// <summary>
    /// Un ítem del plan de tratamiento, con su consulta. Se trae rastreado para poder marcarlo
    /// o cancelarlo.
    /// </summary>
    public Task<PlanTratamientoItem?> GetPlanItemAsync(int itemId, CancellationToken cancellationToken = default) =>
        dbContext.PlanesTratamiento
            .Include(item => item.ConsultaClinica)
            .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);

    /// <summary>Tratamientos del catálogo activos, para proponerlos en un plan.</summary>
    public async Task<IReadOnlyList<Tratamiento>> GetTratamientosActivosAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Tratamientos.AsNoTracking()
            .Where(treatment => treatment.Estado == EstadoRegistro.Activo)
            .OrderBy(treatment => treatment.Nombre)
            .ToListAsync(cancellationToken);

    /// <summary>Un tratamiento del catálogo activo por su id, o null si no existe o está inactivo.</summary>
    public Task<Tratamiento?> GetTratamientoActivoAsync(int tratamientoId, CancellationToken cancellationToken = default) =>
        dbContext.Tratamientos.AsNoTracking()
            .FirstOrDefaultAsync(treatment => treatment.Id == tratamientoId && treatment.Estado == EstadoRegistro.Activo, cancellationToken);

    /// <summary>Piezas dentales, ordenadas por su número FDI.</summary>
    public async Task<IReadOnlyList<PiezaDental>> GetPiezasAsync(CancellationToken cancellationToken = default) =>
        await dbContext.PiezasDentales.AsNoTracking()
            .OrderBy(tooth => tooth.NumeroFDI)
            .ToListAsync(cancellationToken);

    /// <summary>Una pieza dental por su id (o null si no existe).</summary>
    public Task<PiezaDental?> GetPiezaAsync(int piezaId, CancellationToken cancellationToken = default) =>
        dbContext.PiezasDentales.AsNoTracking()
            .FirstOrDefaultAsync(tooth => tooth.Id == piezaId, cancellationToken);

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

    /// <summary>Prepara un ítem nuevo del plan de tratamiento para insertarlo.</summary>
    public async Task AddPlanItemAsync(PlanTratamientoItem item, CancellationToken cancellationToken = default) =>
        await dbContext.PlanesTratamiento.AddAsync(item, cancellationToken);

    /// <summary>
    /// Aplica en la base de datos todos los cambios que quedaron pendientes.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        // Aquí es donde de verdad se ejecutan los INSERT / UPDATE acumulados.
        dbContext.SaveChangesAsync(cancellationToken);
}
