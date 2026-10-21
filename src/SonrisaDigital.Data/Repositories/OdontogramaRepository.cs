using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

/// <summary>
/// Capa de datos del módulo de Odontograma. Es la única pieza que habla con la base de datos
/// (a través de Entity Framework, que traduce el código C# a SQL).
///
/// Recordatorio de la arquitectura:
///  - El Repository solo "trae y guarda" datos (consultas SQL, sin reglas de negocio).
///  - El Service (OdontogramaService) es el que decide qué hacer con esos datos.
///
/// Las dos tablas que se usan aquí:
///  - "PiezasDentales": el catálogo fijo de las 32 piezas (11, 12, 13... 48). Nunca cambia.
///  - "RegistrosOdontograma": una fila por cada pieza que se le ha tocado a un paciente
///    (guarda el estado, las observaciones, el odontólogo y la fecha del cambio).
///
/// Truco de Entity Framework que se usa aquí:
///  - "AsNoTracking()": para consultas de solo lectura; hace la consulta más rápida.
///
/// Orden de lectura del archivo:
///  1) Búsqueda y consultas  → GetActivePatientsAsync, GetPiecesAsync, GetRecordsAsync,
///                             GetOdontologoIdByUserAsync, GetRecordAsync
///  2) Escritura             → AddAsync, SaveChangesWithAuditAsync
/// </summary>
public sealed class OdontogramaRepository(ApplicationDbContext dbContext) : IOdontogramaRepository
{
    // =========================================================================================
    // 1) CONSULTAS DE LECTURA  →  no cambian nada, solo traen datos
    // =========================================================================================

    /// <summary>
    /// Pacientes activos para el panel de búsqueda, ordenados alfabéticamente por nombre y
    /// después por apellido. Si se recibe un texto, filtra por nombre o apellido.
    /// Solo se ofrecen pacientes activos: el odontograma es una herramienta de trabajo clínico.
    /// </summary>
    public async Task<IReadOnlyList<Paciente>> GetActivePatientsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: partir de la tabla de pacientes, ya filtrada a los que están ACTIVOS.
        var query = dbContext.Pacientes.AsNoTracking()
            .Where(patient => patient.Estado == EstadoRegistro.Activo);

        // Paso 2: si el usuario escribió algo, agregar el filtro de nombre o apellido.
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(patient =>
                patient.Nombres.Contains(term) ||
                patient.Apellidos.Contains(term));
        }

        // Paso 3: ordenar alfabéticamente y ejecutar la consulta.
        return await query
            .OrderBy(patient => patient.Nombres)
            .ThenBy(patient => patient.Apellidos)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// El catálogo completo de piezas dentales (las 32), ordenado por su número FDI.
    /// Estas piezas son las mismas para todos los pacientes.
    /// </summary>
    public Task<IReadOnlyList<PiezaDental>> GetPiecesAsync(CancellationToken cancellationToken = default) =>
        dbContext.PiezasDentales.AsNoTracking()
            .OrderBy(piece => piece.NumeroFDI)
            .ToListAsync(cancellationToken)
            // ToListAsync devuelve List<PiezaDental> y el método promete IReadOnlyList; esto
            // solo convierte el tipo de la lista ya terminada (no cambia los datos).
            .ContinueWith(task => (IReadOnlyList<PiezaDental>)task.Result, cancellationToken);

    /// <summary>
    /// Los registros del odontograma de un paciente: solo las piezas que se le han tocado,
    /// con su pieza dental y el odontólogo que hizo el cambio.
    /// </summary>
    public Task<IReadOnlyList<RegistroOdontograma>> GetRecordsAsync(int pacienteId, CancellationToken cancellationToken = default) =>
        dbContext.RegistrosOdontograma.AsNoTracking()
            // Include: traer también la pieza y el odontólogo relacionados (si no, vendrían null).
            .Include(record => record.PiezaDental)
            .Include(record => record.Odontologo)
            .Where(record => record.PacienteId == pacienteId)
            .ToListAsync(cancellationToken)
            .ContinueWith(task => (IReadOnlyList<RegistroOdontograma>)task.Result, cancellationToken);

    /// <summary>
    /// El id del odontólogo que le corresponde a un usuario del sistema (para saber quién
    /// está haciendo el cambio). Devuelve null si ese usuario no es un odontólogo.
    /// </summary>
    public Task<int?> GetOdontologoIdByUserAsync(string userId, CancellationToken cancellationToken = default) =>
        dbContext.Odontologos.AsNoTracking()
            .Where(dentist => dentist.UsuarioId == userId)
            // El "(int?)" es para que, si no encuentra a nadie, el resultado sea null en vez
            // de 0 (así se distingue "no es odontólogo" de "odontólogo con id 0").
            .Select(dentist => (int?)dentist.Id)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Busca el registro de UNA pieza concreta de un paciente. Devuelve null si esa pieza
    /// todavía no tiene registro (es decir, nunca se ha tocado).
    /// </summary>
    public Task<RegistroOdontograma?> GetRecordAsync(int pacienteId, int piezaDentalId, CancellationToken cancellationToken = default) =>
        // Aquí NO se usa AsNoTracking porque el Service puede necesitar modificar esta fila.
        dbContext.RegistrosOdontograma
            .FirstOrDefaultAsync(record => record.PacienteId == pacienteId && record.PiezaDentalId == piezaDentalId, cancellationToken);

    // =========================================================================================
    // 2) OPERACIONES DE ESCRITURA  →  guardan cambios en la base de datos
    // =========================================================================================

    /// <summary>
    /// Prepara un registro de odontograma nuevo para insertarlo en la base de datos.
    /// </summary>
    public async Task AddAsync(RegistroOdontograma record, CancellationToken cancellationToken = default) =>
        // La fila queda "en cola" para insertar; se graba de verdad en SaveChangesWithAuditAsync.
        await dbContext.RegistrosOdontograma.AddAsync(record, cancellationToken);

    /// <summary>
    /// Guarda el cambio de la pieza y su registro de auditoría como UNA SOLA operación.
    ///
    /// ¿Por qué una transacción? Porque son dos escrituras (el registro odontograma + la
    /// auditoría) que deben ocurrir juntas: si una falla, la otra se deshace. Así nunca queda
    /// un cambio clínico sin su rastro en la auditoría.
    /// </summary>
    public async Task SaveChangesWithAuditAsync(
        RegistroOdontograma record,
        RegistroAuditoria audit,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: abrir la transacción (el "todo o nada").
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Paso 2: guardar el cambio de la pieza dental. Después de este punto, una
        // fila nueva ya tiene su Id autogenerado.
        await dbContext.SaveChangesAsync(cancellationToken);

        // Paso 3: completar el Id real antes de insertar la auditoría. Antes se
        // tomaba record.Id antes de guardar y una pieza nueva quedaba con Id 0.
        audit.EntidadId = record.Id;

        // Paso 4: agregar el registro de auditoría y guardarlo también.
        dbContext.RegistrosAuditoria.Add(audit);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Paso 5: confirmar la transacción. Si algo hubiera fallado antes de este punto,
        // la transacción se deshace sola y la base de datos queda como estaba.
        await transaction.CommitAsync(cancellationToken);
    }
}
