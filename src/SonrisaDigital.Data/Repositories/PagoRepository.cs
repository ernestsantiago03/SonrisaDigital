using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Common;
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
///  1) Consultas de lectura  → GetTotalesAsync, GetTratamientosAsync, GetDetalleAsync,
///                              GetByPacienteAsync, GetHistorialAsync
///  2) Antes de escribir     → GetTratamientoAsync (esta sí rastrea cambios)
///  3) Escritura             → AddAsync, SaveChangesAsync
/// </summary>
public sealed class PagoRepository(ApplicationDbContext dbContext) : IPagoRepository
{
    // =========================================================================================
    // 1) CONSULTAS DE LECTURA  →  no cambian nada, solo traen datos
    // =========================================================================================

    /// <summary>
    /// Calcula los totales del módulo de pagos con consultas agregadas. Solo se consideran
    /// tratamientos con precio, igual que en la lista de pagos.
    /// </summary>
    public async Task<PagoTotales> GetTotalesAsync(CancellationToken cancellationToken = default)
    {
        // Total facturado: suma de los precios de los tratamientos cobrables.
        var totalFacturado = await dbContext.TratamientosRealizados
            .AsNoTracking()
            .Where(treatment => treatment.Precio > 0)
            .SumAsync(treatment => (decimal?)treatment.Precio, cancellationToken) ?? 0m;

        // Total cobrado: suma de los pagos que pertenecen a esos mismos tratamientos.
        var totalCobrado = await dbContext.Pagos
            .AsNoTracking()
            .Where(payment => payment.TratamientoRealizado.Precio > 0)
            .SumAsync(payment => (decimal?)payment.Monto, cancellationToken) ?? 0m;

        // Un tratamiento sigue con saldo cuando su precio supera la suma de sus pagos.
        // La suma se hace como subconsulta dentro de la misma consulta SQL.
        var tratamientosConSaldo = await dbContext.TratamientosRealizados
            .AsNoTracking()
            .Where(treatment => treatment.Precio > 0)
            .CountAsync(
                treatment => treatment.Precio >
                    (treatment.Pagos.Sum(payment => (decimal?)payment.Monto) ?? 0m),
                cancellationToken);

        return new PagoTotales(totalFacturado, totalCobrado, tratamientosConSaldo);
    }

    /// <summary>
    /// Página de tratamientos que pueden generar pagos. Filtra en SQL por texto, por
    /// estado financiero y por paciente. Primero cuenta todos los que cumplen el filtro
    /// y después trae solamente los de la página, con sus pagos incluidos.
    /// </summary>
    public async Task<PagedResult<TratamientoRealizado>> SearchTratamientosPagedAsync(
        string? searchTerm,
        EstadoFinanciero? estado,
        int? pacienteId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Paso 1: armar la consulta base. Solo se consideran tratamientos con precio,
        // que son los que pueden generar pagos.
        IQueryable<TratamientoRealizado> query = dbContext.TratamientosRealizados
            .AsNoTracking()
            .Where(treatment => treatment.Precio > 0);

        // Paso 2: filtro por paciente, por ejemplo cuando se llega desde /pagos?paciente=12.
        if (pacienteId.HasValue)
        {
            query = query.Where(treatment => treatment.PacienteId == pacienteId.Value);
        }

        // Paso 3: filtro por texto del paciente o del tratamiento.
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(treatment =>
                treatment.Paciente.Nombres.Contains(term) ||
                treatment.Paciente.Apellidos.Contains(term) ||
                treatment.Tratamiento.Nombre.Contains(term));
        }

        // Paso 4: filtro por estado financiero. El estado no es una columna: se calcula
        // comparando el precio con la suma de los pagos.
        if (estado.HasValue)
        {
            query = AplicarFiltroFinanciero(query, estado.Value);
        }

        // Paso 5: contar en SQL y ajustar la página si el filtro dejó menos resultados.
        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        // Paso 6: traer solo la página, con el paciente, el tratamiento y los pagos
        // necesarios para calcular los montos y el último pago. AsSplitQuery evita que
        // los pagos se dupliquen en una sola consulta grande.
        var items = await query
            .Include(treatment => treatment.Paciente)
            .Include(treatment => treatment.Tratamiento)
            .Include(treatment => treatment.Pagos)
            .AsSplitQuery()
            .OrderByDescending(treatment =>
                treatment.Precio - (treatment.Pagos.Sum(payment => (decimal?)payment.Monto) ?? 0m))
            .ThenBy(treatment => treatment.Paciente.Nombres)
            .ThenBy(treatment => treatment.Paciente.Apellidos)
            .ThenBy(treatment => treatment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TratamientoRealizado>(items, total, page, pageSize);
    }

    /// <summary>
    /// Traduce el estado financiero a una condición SQL usando la suma de los pagos
    /// del tratamiento. Se usa una subconsulta correlacionada, no una carga en memoria.
    /// </summary>
    private static IQueryable<TratamientoRealizado> AplicarFiltroFinanciero(
        IQueryable<TratamientoRealizado> query,
        EstadoFinanciero estado) => estado switch
    {
        EstadoFinanciero.Pagado => query.Where(treatment =>
            treatment.Precio <= (treatment.Pagos.Sum(payment => (decimal?)payment.Monto) ?? 0m)),
        EstadoFinanciero.PagoParcial => query.Where(treatment =>
            treatment.Precio > (treatment.Pagos.Sum(payment => (decimal?)payment.Monto) ?? 0m) &&
            treatment.Pagos.Any()),
        EstadoFinanciero.Pendiente => query.Where(treatment => !treatment.Pagos.Any()),
        _ => query
    };

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
    /// Trae el tratamiento para cobrar y bloquea su fila hasta que termine la transacción.
    /// El bloqueo evita que dos cajas calculen el saldo al mismo tiempo y acepten cobros
    /// que juntos superen el precio del tratamiento.
    /// </summary>
    public async Task<TratamientoRealizado?> GetTratamientoParaPagoAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default)
    {
        // La consulta de EF aplica el filtro de clínica activa. Si el tratamiento no es de
        // esta clínica, no se bloquea nada ni se revela su existencia.
        var clinicaId = await dbContext.TratamientosRealizados
            .AsNoTracking()
            .Where(treatment => treatment.Id == tratamientoRealizadoId)
            .Select(treatment => (int?)treatment.ClinicaId)
            .SingleOrDefaultAsync(cancellationToken);

        if (clinicaId is null)
        {
            return null;
        }

        // UPDLOCK reserva la fila para esta transacción; HOLDLOCK mantiene el bloqueo
        // hasta el commit o rollback. Se incluye ClinicaId para que el bloqueo nunca
        // recaiga sobre filas de otra clínica.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM TratamientosRealizados WITH (UPDLOCK, HOLDLOCK) WHERE Id = {tratamientoRealizadoId} AND ClinicaId = {clinicaId.Value}",
            cancellationToken);

        return await GetTratamientoAsync(tratamientoRealizadoId, cancellationToken);
    }

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
