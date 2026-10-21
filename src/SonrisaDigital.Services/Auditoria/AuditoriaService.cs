using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria.DTOs;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.Services.Auditoria;

public sealed class AuditoriaService(
    IAuditoriaRepository repository,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IAuditoriaService
{
    // Nombre de la política registrada en DependencyInjection a partir de PermissionCatalog.
    private const string PermisoVerAuditoria = "Auditoria.Ver";

    public async Task RegistrarAsync(
        string accion,
        string modulo,
        string entidad,
        int? entidadId,
        string descripcion,
        CancellationToken cancellationToken = default)
    {
        // Se intenta obtener el id desde el servicio de sesión y, como respaldo,
        // desde el principal. Así la auditoría no depende de que MainLayout haya
        // ejecutado antes que el Service.
        var usuarioId = currentUserService.UsuarioId
            ?? currentUserService.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            // La FK UsuarioId es obligatoria. Si no hay usuario, la operación no puede
            // quedar sin trazabilidad: se falla de forma explícita (ARQ-05).
            throw new InvalidOperationException(
                "No hay un usuario autenticado para registrar la auditoría.");
        }

        await repository.AddAsync(new RegistroAuditoria
        {
            UsuarioId = usuarioId,
            FechaHora = DateTime.UtcNow,
            Accion = accion,
            Modulo = modulo,
            Entidad = entidad,
            EntidadId = entidadId,
            Descripcion = descripcion
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ActividadRecienteDto>> GetRecientesAsync(
        int cantidad = 20,
        CancellationToken cancellationToken = default)
    {
        // El resumen del Dashboard también es información de auditoría: se protege
        // igual que la página completa para que no se filtre a otros roles.
        await EnsurePuedeConsultarAsync();

        var limite = Math.Clamp(cantidad, 1, 100);
        var (registros, _, _) = await repository.SearchAsync(
            texto: null,
            modulo: null,
            accion: null,
            desdeUtc: null,
            hastaUtcExclusivo: null,
            pagina: 1,
            tamanoPagina: limite,
            cancellationToken: cancellationToken);

        return registros.Select(Map).ToList();
    }

    public async Task<AuditoriaPaginaDto> SearchAsync(
        AuditoriaFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        await EnsurePuedeConsultarAsync();

        // La página y el tamaño se normalizan aquí para que ningún llamador pueda
        // solicitar páginas negativas o volúmenes arbitrariamente grandes.
        var pagina = Math.Max(1, filtro.Pagina);
        var tamanoPagina = Math.Clamp(filtro.TamanoPagina, 10, 100);

        var (registros, total, paginaActual) = await repository.SearchAsync(
            texto: filtro.Texto,
            modulo: filtro.Modulo,
            accion: filtro.Accion,
            desdeUtc: filtro.DesdeUtc,
            hastaUtcExclusivo: filtro.HastaUtcExclusivo,
            pagina: pagina,
            tamanoPagina: tamanoPagina,
            cancellationToken: cancellationToken);

        return new AuditoriaPaginaDto(
            registros.Select(Map).ToList(),
            total,
            paginaActual,
            tamanoPagina);
    }

    public async Task<AuditoriaOpcionesDto> GetOpcionesAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePuedeConsultarAsync();

        var (modulos, acciones) = await repository.GetOpcionesAsync(cancellationToken);
        return new AuditoriaOpcionesDto(modulos, acciones);
    }

    /// <summary>
    /// Comprueba que la sesión actual tenga el permiso de auditoría antes de leer.
    /// La página ya tiene [Authorize], pero el Service no debe confiar solo en la UI.
    /// </summary>
    private async Task EnsurePuedeConsultarAsync()
    {
        var principal = currentUserService.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException(
                "No hay una sesión autenticada para consultar la auditoría.");
        }

        var resultado = await authorizationService.AuthorizeAsync(principal, PermisoVerAuditoria);
        if (!resultado.Succeeded)
        {
            throw new UnauthorizedAccessException(
                "No tienes permiso para consultar la auditoría.");
        }
    }

    /// <summary>Traduce la entidad de base de datos al DTO que consume la interfaz.</summary>
    private static ActividadRecienteDto Map(RegistroAuditoria registro) =>
        new(
            registro.Id,
            ObtenerNombreActor(registro.Usuario),
            registro.Accion,
            registro.Modulo,
            registro.Entidad,
            registro.EntidadId,
            registro.Descripcion,
            registro.FechaHora);

    private static string ObtenerNombreActor(Usuario? usuario)
    {
        if (usuario is null)
        {
            return "Sistema";
        }

        if (usuario.Odontologo is not null)
        {
            var nombreCompleto = $"{usuario.Odontologo.Nombres} {usuario.Odontologo.Apellidos}".Trim();
            if (!string.IsNullOrWhiteSpace(nombreCompleto))
            {
                return nombreCompleto;
            }
        }

        return string.IsNullOrWhiteSpace(usuario.Nombre) ? "Sistema" : usuario.Nombre;
    }
}
