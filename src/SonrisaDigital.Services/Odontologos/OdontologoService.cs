using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Validacion;
using SonrisaDigital.Services.Odontologos.DTOs;

namespace SonrisaDigital.Services.Odontologos;

/// <summary>
/// Operaciones de odontólogos. Las escrituras validan el permiso del usuario
/// actual antes de modificar datos; ocultar acciones en la UI es solo la primera capa.
/// </summary>
public sealed class OdontologoService(
    IOdontologoRepository repository,
    IAuditoriaService auditoriaService,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IOdontologoService
{
    // Límite de filas por página que acepta el servicio, aunque la pantalla pida más.
    private const int MaxPageSize = 50;

    public async Task<IReadOnlyList<OdontologoListItemDto>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontologos.Ver", "No tienes permiso para consultar odontólogos.");

        var odontologos = await repository.SearchAsync(searchTerm, estado, cancellationToken);

        return odontologos.Select(ToListItem).ToList();
    }

    /// <summary>
    /// Devuelve una página de odontólogos para la tabla principal. El repositorio hace
    /// el conteo y el recorte en SQL; aquí solo se convierten las filas al DTO.
    /// </summary>
    public async Task<PagedResult<OdontologoListItemDto>> SearchPagedAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontologos.Ver", "No tienes permiso para consultar odontólogos.");

        var (pagina, tamano) = NormalizarPagina(page, pageSize);
        var result = await repository.SearchPagedAsync(searchTerm, estado, pagina, tamano, cancellationToken);

        return new PagedResult<OdontologoListItemDto>(
            result.Items.Select(ToListItem).ToList(),
            result.Total,
            result.Page,
            result.PageSize);
    }

    public async Task<OdontologoDetalleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontologos.Ver", "No tienes permiso para consultar odontólogos.");

        var odontologo = await repository.GetByIdAsync(id, cancellationToken);
        return odontologo is null ? null : ToDetail(odontologo);
    }

    public async Task<OdontologoFormDto?> GetFormAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontologos.Ver", "No tienes permiso para consultar odontólogos.");

        var odontologo = await repository.GetByIdAsync(id, cancellationToken);
        return odontologo is null ? null : ToForm(odontologo);
    }

    public async Task<int> CreateAsync(OdontologoFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontologos.Crear", "No tienes permiso para registrar odontólogos.");

        ValidadorDto.PrepararYValidar(model);

        var odontologo = new Odontologo();
        ApplyForm(odontologo, model);
        odontologo.Estado = EstadoRegistro.Activo;

        await repository.AddAsync(odontologo, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Agregó", "Odontólogos", nameof(Odontologo), odontologo.Id,
            $"Agregó un odontólogo: {odontologo.Nombres} {odontologo.Apellidos}", cancellationToken);

        return odontologo.Id;
    }

    public async Task UpdateAsync(OdontologoFormDto model, CancellationToken cancellationToken = default)
    {
        // Defensa en el servicio: ocultar el botón en la UI no impide una llamada
        // directa al servicio. La autorización se comprueba antes de tocar datos.
        await AsegurarPermisoAsync(
            "Odontologos.Editar",
            "No tienes permiso para editar odontólogos.");

        ValidadorDto.PrepararYValidar(model);

        var odontologo = await repository.GetByIdAsync(model.Id, cancellationToken)
            ?? throw new InvalidOperationException("El odontólogo no existe.");

        // Editar el perfil no cambia el estado. Activar o desactivar es una
        // operación aparte y exige el permiso Odontologos.Desactivar.
        ApplyForm(odontologo, model);
        await repository.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Modificó", "Odontólogos", nameof(Odontologo), odontologo.Id,
            $"Modificó el odontólogo: {odontologo.Nombres} {odontologo.Apellidos}", cancellationToken);
    }

    public async Task SetStatusAsync(int id, EstadoRegistro estado, CancellationToken cancellationToken = default)
    {
        // Activar o desactivar es una operación sensible: se exige el permiso
        // específico, no solo el permiso de consulta de odontólogos.
        await AsegurarPermisoAsync(
            "Odontologos.Desactivar",
            "No tienes permiso para activar o desactivar odontólogos.");

        var odontologo = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("El odontólogo no existe.");

        odontologo.Estado = estado;
        await repository.SaveChangesAsync(cancellationToken);

        var verbo = estado == EstadoRegistro.Activo ? "Activó" : "Desactivó";
        await auditoriaService.RegistrarAsync(
            verbo, "Odontólogos", nameof(Odontologo), odontologo.Id,
            $"{verbo} el odontólogo: {odontologo.Nombres} {odontologo.Apellidos}", cancellationToken);
    }

    /// <summary>
    /// Comprueba una política de permisos contra el usuario actual.
    /// Si no hay un principal o la política no se cumple, lanza
    /// <see cref="UnauthorizedAccessException"/> para que la UI muestre una
    /// denegación controlada.
    /// </summary>
    private async Task AsegurarPermisoAsync(string policy, string mensaje)
    {
        var principal = currentUserService.Principal;
        if (principal is null)
        {
            // Sin usuario autenticado se deniega por defecto (fail closed).
            throw new UnauthorizedAccessException(mensaje);
        }

        // Se usa el mismo motor de políticas que AuthorizeView y [Authorize].
        var result = await authorizationService.AuthorizeAsync(principal, policy);
        if (!result.Succeeded)
        {
            throw new UnauthorizedAccessException(mensaje);
        }
    }

    private static void ApplyForm(Odontologo odontologo, OdontologoFormDto model)
    {
        odontologo.Nombres = model.Nombres.Trim();
        odontologo.Apellidos = model.Apellidos.Trim();
        odontologo.NumeroLicencia = Normalize(model.NumeroLicencia);
        odontologo.Especialidad = Normalize(model.Especialidad);
        odontologo.Telefono = Normalize(model.Telefono);
        odontologo.Correo = Normalize(model.Correo);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Convierte la entidad de base de datos en la fila que muestra la tabla.
    private static OdontologoListItemDto ToListItem(Odontologo odontologo) => new(
        odontologo.Id,
        $"{odontologo.Nombres} {odontologo.Apellidos}",
        odontologo.NumeroLicencia,
        odontologo.Especialidad,
        odontologo.Telefono,
        odontologo.Correo,
        odontologo.Estado);

    // Página mínima 1 y tamaño dentro del límite permitido por el servicio.
    private static (int Page, int PageSize) NormalizarPagina(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

    private static OdontologoFormDto ToForm(Odontologo odontologo) => new()
    {
        Id = odontologo.Id,
        Estado = odontologo.Estado,
        Nombres = odontologo.Nombres,
        Apellidos = odontologo.Apellidos,
        NumeroLicencia = odontologo.NumeroLicencia ?? string.Empty,
        Especialidad = odontologo.Especialidad,
        Telefono = odontologo.Telefono,
        Correo = odontologo.Correo
    };

    private static OdontologoDetalleDto ToDetail(Odontologo odontologo) => new(
        odontologo.Id,
        odontologo.Nombres,
        odontologo.Apellidos,
        odontologo.NumeroLicencia,
        odontologo.Especialidad,
        odontologo.Telefono,
        odontologo.Correo,
        odontologo.Estado);
}
