using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

/// <summary>
/// Capa de datos del módulo de Pagos. Es la única pieza que habla con la base de datos
/// (a través de Entity Framework, que traduce el código C# a SQL).
///
/// Recordatorio de la arquitectura:
///  - El Repository solo "trae y guarda" datos (consultas SQL, sin reglas de negocio).
///  - El Service (PagoService) es el que calcula montos, valida y audita.
///
/// Detalle importante: un "tratamiento realizado" es lo que se cobra (tiene precio). Cada
/// pago es una cuota de ese tratamiento. Por eso casi todas las consultas de arriba traen
/// el tratamiento con sus "Pagos" incluidos: los montos se calculan sumando esa lista.
///
/// Trucos de Entity Framework que se usan aquí:
///  - "Include(...)": trae también los datos relacionados en la misma consulta (paciente,
///    tratamiento, pagos...). Sin esto, esos datos vendrían en null.
///  - "ThenInclude(...)": sigue la cadena desde lo último incluido (por ejemplo, de cada pago
///    traer también su usuario).
///  - "AsNoTracking()": para consultas de solo lectura; hace la consulta más rápida.
///
/// Orden de lectura del archivo:
///  1) Consultas de lectura  → GetTratamientosAsync, GetDetalleAsync, GetByPacienteAsync, GetHistorialAsync
///  2) Antes de escribir     → GetTratamientoAsync (esta sí rastrea cambios)
///  3) Escritura             → AddAsync, SaveChangesAsync
/// </summary>
public sealed class PagoRepository(ApplicationDbContext dbContext) : IPagoRepository
{
    // =========================================================================================
    // 1) CONSULTAS DE LECTURA  →  no cambian nada, solo traen datos
    // =========================================================================================

    /// <summary>
    /// Trae los tratamientos que pueden generar pagos (solo los que tienen precio), con su
    /// paciente, su tratamiento y sus pagos ya cargados. Si recibe un texto, busca por nombre
    /// del paciente o por nombre del tratamiento.
    /// </summary>
    public async Task<IReadOnlyList<TratamientoRealizado>> GetTratamientosAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: armar la consulta.
        // Solo se consideran tratamientos con precio, que son los que pueden generar pagos.
        // Se incluyen el paciente, el tratamiento y TODOS los pagos, porque el Service necesita
        // esas listas para calcular cuánto se ha pagado y cuánto falta.
        var query = dbContext.TratamientosRealizados
            .Include(treatment => treatment.Paciente)
            .Include(treatment => treatment.Tratamiento)
            .Include(treatment => treatment.Pagos)
            .AsNoTracking()
            .Where(treatment => treatment.Precio > 0);

        // Paso 2: si hay texto de búsqueda, filtrar por nombre/apellido del paciente o por
        // nombre del tratamiento (como el LIKE '%texto%' de SQL).
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(treatment =>
                treatment.Paciente.Nombres.Contains(term) ||
                treatment.Paciente.Apellidos.Contains(term) ||
                treatment.Tratamiento.Nombre.Contains(term));
        }

        // Paso 3: ordenar por apellido y nombre del paciente, y ejecutar la consulta.
        return await query
            .OrderBy(treatment => treatment.Paciente.Apellidos)
            .ThenBy(treatment => treatment.Paciente.Nombres)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Trae un tratamiento para registrarle un pago (versión que sí rastrea los cambios,
    /// es decir, sin AsNoTracking).
    /// </summary>
    public Task<TratamientoRealizado?> GetTratamientoAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default) =>
        // Aquí NO se usa AsNoTracking porque el Service necesita que Entity Framework siga
        // vigilando este objeto (por ejemplo, para leer la lista de pagos actualizada).
        dbContext.TratamientosRealizados
            .Include(treatment => treatment.Paciente)
            .Include(treatment => treatment.Tratamiento)
            .Include(treatment => treatment.Pagos)
            .FirstOrDefaultAsync(treatment => treatment.Id == tratamientoRealizadoId, cancellationToken);

    /// <summary>
    /// Trae un tratamiento con todo lo que necesita la pantalla de detalle: paciente, tratamiento,
    /// odontólogo, pieza dental y pagos, incluyendo el usuario que registró cada pago.
    /// </summary>
    public Task<TratamientoRealizado?> GetDetalleAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default) =>
        dbContext.TratamientosRealizados
            .AsNoTracking()
            .Include(treatment => treatment.Paciente)
            .Include(treatment => treatment.Tratamiento)
            .Include(treatment => treatment.Odontologo)
            // La pieza dental puede ser null (hay tratamientos generales, sin pieza concreta).
            .Include(treatment => treatment.PiezaDental)
            .Include(treatment => treatment.Pagos)
                // ThenInclude: de cada pago, traer también el usuario que lo registró.
                .ThenInclude(payment => payment.Usuario)
            .FirstOrDefaultAsync(treatment => treatment.Id == tratamientoRealizadoId, cancellationToken);

    /// <summary>
    /// Tratamientos de un paciente, del más reciente al más antiguo, con sus pagos.
    /// Es lo que usa la pestaña "Pagos" de la ficha del paciente.
    /// </summary>
    public async Task<IReadOnlyList<TratamientoRealizado>> GetByPacienteAsync(
        int pacienteId,
        CancellationToken cancellationToken = default) =>
        await dbContext.TratamientosRealizados
            .AsNoTracking()
            .Include(treatment => treatment.Paciente)
            .Include(treatment => treatment.Tratamiento)
            .Include(treatment => treatment.Odontologo)
            .Include(treatment => treatment.PiezaDental)
            .Include(treatment => treatment.Pagos)
                .ThenInclude(payment => payment.Usuario)
            // Solo los tratamientos de ESTE paciente.
            .Where(treatment => treatment.PacienteId == pacienteId)
            // Los más nuevos primero (si empatan en fecha, gana el Id mayor).
            .OrderByDescending(treatment => treatment.Fecha)
            .ThenByDescending(treatment => treatment.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Últimos pagos registrados (con su tratamiento, paciente y usuario), del más reciente
    /// al más antiguo.
    /// </summary>
    public async Task<IReadOnlyList<Pago>> GetHistorialAsync(
        int cantidad,
        CancellationToken cancellationToken = default) =>
        await dbContext.Pagos
            .AsNoTracking()
            // De cada pago se sube hasta su tratamiento, y de ahí hasta el paciente.
            .Include(payment => payment.TratamientoRealizado)
                .ThenInclude(treatment => treatment.Paciente)
            // Y también el nombre del tratamiento realizado.
            .Include(payment => payment.TratamientoRealizado)
                .ThenInclude(treatment => treatment.Tratamiento)
            .Include(payment => payment.Usuario)
            .OrderByDescending(payment => payment.Fecha)
            .ThenByDescending(payment => payment.Id)
            // Take(cantidad) = "los primeros N de esta lista ya ordenada" (SQL: TOP N).
            .Take(cantidad)
            .ToListAsync(cancellationToken);

    // =========================================================================================
    // 2) OPERACIONES DE ESCRITURA  →  guardan cambios en la base de datos
    // =========================================================================================

    /// <summary>
    /// Prepara un pago nuevo para insertarlo en la base de datos.
    /// </summary>
    public async Task AddAsync(Pago pago, CancellationToken cancellationToken = default)
    {
        // La fila queda "en cola" para insertar; todavía no viaja a la base de datos.
        await dbContext.Pagos.AddAsync(pago, cancellationToken);
    }

    /// <summary>
    /// Aplica en la base de datos todos los cambios que quedaron pendientes.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        // Aquí es donde de verdad se ejecutan los INSERT / UPDATE acumulados.
        dbContext.SaveChangesAsync(cancellationToken);
}
