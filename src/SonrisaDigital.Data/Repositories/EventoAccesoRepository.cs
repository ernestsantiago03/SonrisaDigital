using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public sealed class EventoAccesoRepository(ApplicationDbContext dbContext) : IEventoAccesoRepository
{
    public async Task AddAsync(EventoAcceso evento, CancellationToken cancellationToken = default)
    {
        await dbContext.EventosAcceso.AddAsync(evento, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<EventoAcceso> Eventos, int Total, int Pagina)> SearchAsync(
        string? texto,
        ResultadoAcceso? resultado,
        DateTime? desdeUtc,
        DateTime? hastaUtcExclusivo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        // La bitácora es de solo lectura desde la aplicación.
        var query = dbContext.EventosAcceso.AsNoTracking();

        // 1. Texto libre: cuenta, nombre del usuario, IP, navegador o detalle.
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termino = texto.Trim();
            query = query.Where(evento =>
                (evento.Correo != null && evento.Correo.Contains(termino))
                || (evento.Usuario != null && evento.Usuario.Nombre.Contains(termino))
                || (evento.Usuario != null
                    && evento.Usuario.Odontologo != null
                    && (evento.Usuario.Odontologo.Nombres.Contains(termino)
                        || evento.Usuario.Odontologo.Apellidos.Contains(termino)))
                || (evento.DireccionIp != null && evento.DireccionIp.Contains(termino))
                || (evento.Agente != null && evento.Agente.Contains(termino))
                || (evento.Detalle != null && evento.Detalle.Contains(termino)));
        }

        // 2. Resultado exacto.
        if (resultado.HasValue)
        {
            query = query.Where(evento => evento.Resultado == resultado.Value);
        }

        // 3. Rango de fechas en UTC. El límite superior es exclusivo para incluir
        // completo el día elegido sin depender de la hora exacta.
        if (desdeUtc.HasValue)
        {
            query = query.Where(evento => evento.FechaHora >= desdeUtc.Value);
        }

        if (hastaUtcExclusivo.HasValue)
        {
            query = query.Where(evento => evento.FechaHora < hastaUtcExclusivo.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var tamano = Math.Clamp(tamanoPagina, 1, 100);
        var totalPaginas = total == 0
            ? 1
            : (int)Math.Ceiling(total / (double)tamano);

        // Si la página solicitada ya no existe (por ejemplo después de aplicar un
        // filtro), se devuelve la última página válida.
        var paginaActual = Math.Clamp(pagina, 1, totalPaginas);

        var eventos = await query
            .Include(evento => evento.Usuario)
                .ThenInclude(usuario => usuario!.Odontologo)
            .OrderByDescending(evento => evento.FechaHora)
            .ThenByDescending(evento => evento.Id)
            .Skip((paginaActual - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancellationToken);

        return (eventos, total, paginaActual);
    }
}
