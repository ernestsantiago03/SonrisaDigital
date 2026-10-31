using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Clinicas.DTOs;

namespace SonrisaDigital.Services.Clinicas;

/// <summary>
/// Lista de clínicas para el selector del menú del Dueño. Usa su propio DbContext: el del circuito lo
/// usa la página que se está cargando, y un DbContext no admite dos consultas a la vez.
/// </summary>
public interface ISelectorClinicasService
{
    Task<IReadOnlyList<ClinicaListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
}

public sealed class SelectorClinicasService(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : ISelectorClinicasService
{
    public async Task<IReadOnlyList<ClinicaListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Clinicas.Ver", "No tienes permiso para consultar clínicas.");

        await using var db = new ApplicationDbContext(options, SinClinica.Instancia);
        return await db.Clinicas
            .AsNoTracking()
            .OrderBy(clinic => clinic.Nombre)
            .ThenBy(clinic => clinic.Id)
            .Select(clinic => new ClinicaListItemDto(clinic.Id, clinic.Nombre, clinic.Codigo, clinic.Estado, clinic.FechaAlta))
            .ToListAsync(cancellationToken);
    }
}
