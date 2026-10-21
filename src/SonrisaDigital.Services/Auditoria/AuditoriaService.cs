using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria.DTOs;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.Services.Auditoria;

public sealed class AuditoriaService(
    IAuditoriaRepository repository,
    ICurrentUserService currentUserService) : IAuditoriaService
{
    public async Task RegistrarAsync(
        string accion,
        string modulo,
        string entidad,
        int? entidadId,
        string descripcion,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = currentUserService.UsuarioId;
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            // Sin usuario identificado no se puede registrar (la FK UsuarioId es obligatoria).
            return;
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
        var registros = await repository.GetRecientesAsync(cantidad, cancellationToken);

        return registros
            .Select(registro => new ActividadRecienteDto(
                registro.Id,
                ObtenerNombreActor(registro.Usuario),
                registro.Accion,
                registro.Modulo,
                registro.Entidad,
                registro.EntidadId,
                registro.Descripcion,
                registro.FechaHora))
            .ToList();
    }

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
