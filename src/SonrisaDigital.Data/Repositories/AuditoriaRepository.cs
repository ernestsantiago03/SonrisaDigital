using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public sealed class AuditoriaRepository(ApplicationDbContext dbContext) : IAuditoriaRepository
{
    public async Task AddAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default)
    {
        // El registro se agrega y se confirma de inmediato. Las operaciones que
        // necesitan atomicidad entre cambio y auditoría usan una transacción propia.
        await dbContext.RegistrosAuditoria.AddAsync(registro, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RegistroAuditoria>> GetRecientesAsync(int cantidad, CancellationToken cancellationToken = default)
    {
        // Se reutiliza la búsqueda paginada para que el orden y las relaciones
        // (usuario y odontólogo) sean exactamente los mismos que en la página.
        var (registros, _, _) = await SearchAsync(
            texto: null,
            modulo: null,
            accion: null,
            desdeUtc: null,
            hastaUtcExclusivo: null,
            pagina: 1,
            tamanoPagina: Math.Clamp(cantidad, 1, 100),
            cancellationToken: cancellationToken);

        return registros;
    }

    public async Task<(IReadOnlyList<RegistroAuditoria> Registros, int Total, int Pagina)> SearchAsync(
        string? texto,
        string? modulo,
        string? accion,
        DateTime? desdeUtc,
        DateTime? hastaUtcExclusivo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        // La consulta base queda sin tracking: la auditoría es de solo lectura.
        var query = dbContext.RegistrosAuditoria.AsNoTracking();

        // 1. Texto libre: busca en las columnas visibles y en el nombre del usuario.
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termino = texto.Trim();
            query = query.Where(registro =>
                registro.Accion.Contains(termino)
                || registro.Modulo.Contains(termino)
                || registro.Entidad.Contains(termino)
                || (registro.Descripcion != null && registro.Descripcion.Contains(termino))
                || registro.Usuario.Nombre.Contains(termino)
                // La tabla muestra el nombre del odontólogo cuando el usuario tiene perfil;
                // por eso la búsqueda también debe cubrir nombres y apellidos del perfil.
                || (registro.Usuario.Odontologo != null
                    && (registro.Usuario.Odontologo.Nombres.Contains(termino)
                        || registro.Usuario.Odontologo.Apellidos.Contains(termino))));
        }

        // 2. Filtros exactos por módulo y acción.
        if (!string.IsNullOrWhiteSpace(modulo))
        {
            query = query.Where(registro => registro.Modulo == modulo);
        }

        if (!string.IsNullOrWhiteSpace(accion))
        {
            query = query.Where(registro => registro.Accion == accion);
        }

        // 3. Rango de fechas en UTC. El límite superior es exclusivo para incluir
        // completo el día elegido sin depender de la hora exacta.
        if (desdeUtc.HasValue)
        {
            query = query.Where(registro => registro.FechaHora >= desdeUtc.Value);
        }

        if (hastaUtcExclusivo.HasValue)
        {
            query = query.Where(registro => registro.FechaHora < hastaUtcExclusivo.Value);
        }

        // 4. El total se calcula antes de paginar para que la UI pueda mostrar
        // cuántos resultados hay y cuántas páginas existen.
        var total = await query.CountAsync(cancellationToken);

        var tamano = Math.Clamp(tamanoPagina, 1, 100);
        var totalPaginas = total == 0
            ? 1
            : (int)Math.Ceiling(total / (double)tamano);

        // Si la página solicitada ya no existe (por ejemplo después de aplicar un
        // filtro), se devuelve la última página válida.
        var paginaActual = Math.Clamp(pagina, 1, totalPaginas);

        var registros = await query
            .Include(registro => registro.Usuario)
                .ThenInclude(usuario => usuario!.Odontologo)
            .OrderByDescending(registro => registro.FechaHora)
            .ThenByDescending(registro => registro.Id)
            .Skip((paginaActual - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancellationToken);

        return (registros, total, paginaActual);
    }

    public async Task<(IReadOnlyList<string> Modulos, IReadOnlyList<string> Acciones)> GetOpcionesAsync(
        CancellationToken cancellationToken = default)
    {
        // Los filtros se alimentan de valores reales ya registrados. Así no se
        // muestran opciones vacías ni se mantiene un catálogo duplicado.
        var modulos = await dbContext.RegistrosAuditoria
            .AsNoTracking()
            .Where(registro => registro.Modulo != null && registro.Modulo != "")
            .Select(registro => registro.Modulo)
            .Distinct()
            .OrderBy(modulo => modulo)
            .ToListAsync(cancellationToken);

        var acciones = await dbContext.RegistrosAuditoria
            .AsNoTracking()
            .Where(registro => registro.Accion != null && registro.Accion != "")
            .Select(registro => registro.Accion)
            .Distinct()
            .OrderBy(accion => accion)
            .ToListAsync(cancellationToken);

        return (modulos, acciones);
    }
}
