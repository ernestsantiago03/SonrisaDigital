using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Accesos.DTOs;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.Services.Accesos;

public sealed class AccesoService(
    IEventoAccesoRepository repository,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService,
    ILogger<AccesoService> logger) : IAccesoService
{
    private const string PermisoVerAuditoria = "Auditoria.Ver";

    public async Task RegistrarAsync(
        RegistroAccesoDto registro,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.AddAsync(new EventoAcceso
            {
                UsuarioId = string.IsNullOrWhiteSpace(registro.UsuarioId) ? null : registro.UsuarioId,
                Correo = Truncar(registro.Correo?.Trim(), 256),
                Resultado = registro.Resultado,
                FechaHora = DateTime.UtcNow,
                DireccionIp = Truncar(registro.DireccionIp?.Trim(), 45),
                Agente = Truncar(registro.Agente?.Trim(), 300),
                ClinicaId = registro.ClinicaId,
                Detalle = Truncar(registro.Detalle?.Trim(), 500)
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // La petición se canceló (por ejemplo, el navegador cerró la conexión).
            // No hay nada que registrar y el acceso no debe fallar por ello.
        }
        catch (Exception exception)
        {
            // La auditoría es best-effort: se deja constancia en el log del servidor
            // y el inicio de sesión continúa con normalidad.
            logger.LogError(
                exception,
                "No se pudo registrar el evento de acceso {Resultado} para {Correo}.",
                registro.Resultado,
                registro.Correo);
        }
    }

    public async Task<AccesoPaginaDto> SearchAsync(
        AccesoFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        await EnsurePuedeConsultarAsync();

        // Se normalizan página y tamaño para que ningún llamador pueda pedir
        // páginas negativas o volúmenes arbitrariamente grandes.
        var pagina = Math.Max(1, filtro.Pagina);
        var tamanoPagina = Math.Clamp(filtro.TamanoPagina, 10, 100);

        var (eventos, total, paginaActual) = await repository.SearchAsync(
            texto: filtro.Texto,
            resultado: filtro.Resultado,
            desdeUtc: filtro.DesdeUtc,
            hastaUtcExclusivo: filtro.HastaUtcExclusivo,
            pagina: pagina,
            tamanoPagina: tamanoPagina,
            cancellationToken: cancellationToken);

        return new AccesoPaginaDto(
            eventos.Select(Map).ToList(),
            total,
            paginaActual,
            tamanoPagina);
    }

    private async Task EnsurePuedeConsultarAsync()
    {
        var principal = currentUserService.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException(
                "No hay una sesión autenticada para consultar los accesos.");
        }

        var resultado = await authorizationService.AuthorizeAsync(principal, PermisoVerAuditoria);
        if (!resultado.Succeeded)
        {
            throw new UnauthorizedAccessException(
                "No tienes permiso para consultar los accesos.");
        }
    }

    private static EventoAccesoDto Map(EventoAcceso evento) =>
        new(
            evento.Id,
            evento.Correo,
            ObtenerNombreActor(evento.Usuario),
            evento.Resultado,
            evento.FechaHora,
            evento.DireccionIp,
            evento.Agente,
            evento.Detalle);

    private static string ObtenerNombreActor(Usuario? usuario)
    {
        if (usuario is null)
        {
            return "Sin usuario";
        }

        if (usuario.Odontologo is not null)
        {
            var nombreCompleto = $"{usuario.Odontologo.Nombres} {usuario.Odontologo.Apellidos}".Trim();
            if (!string.IsNullOrWhiteSpace(nombreCompleto))
            {
                return nombreCompleto;
            }
        }

        return string.IsNullOrWhiteSpace(usuario.Nombre) ? "Sin usuario" : usuario.Nombre;
    }

    private static string? Truncar(string? valor, int longitudMaxima) =>
        string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Length <= longitudMaxima
                ? valor
                : valor[..longitudMaxima];
}
