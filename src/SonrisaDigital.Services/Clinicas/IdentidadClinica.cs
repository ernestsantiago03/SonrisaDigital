using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Validacion;

namespace SonrisaDigital.Services.Clinicas;

/// <summary>Datos de la página "Acerca De" de la clínica de la sesión.</summary>
public sealed record IdentidadClinicaDto(
    string Nombre,
    string? Rnc,
    string? Eslogan,
    string? Telefono,
    string? Correo,
    string? Horario,
    string? Direccion);

public sealed class IdentidadClinicaFormDto
{
    [Required(ErrorMessage = "El nombre comercial es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "El RNC no puede superar los 30 caracteres.")]
    public string? Rnc { get; set; }

    [StringLength(300, ErrorMessage = "El eslogan no puede superar los 300 caracteres.")]
    public string? Eslogan { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "Introduzca un correo válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
    public string? Correo { get; set; }

    [StringLength(150, ErrorMessage = "El horario no puede superar los 150 caracteres.")]
    public string? Horario { get; set; }

    [StringLength(300, ErrorMessage = "La dirección no puede superar los 300 caracteres.")]
    public string? Direccion { get; set; }
}

public interface IIdentidadClinicaService
{
    /// <summary>Datos de la clínica de la sesión. Nulo si la sesión no tiene clínica.</summary>
    Task<IdentidadClinicaDto?> ObtenerAsync(CancellationToken cancellationToken = default);

    /// <summary>Actualiza la identidad de la clínica de la sesión. Requiere el permiso AcercaDe.Editar.</summary>
    Task ActualizarAsync(IdentidadClinicaFormDto model, CancellationToken cancellationToken = default);
}

public sealed class IdentidadClinicaService(
    ApplicationDbContext dbContext,
    ITenantProvider tenant,
    IAuditoriaService auditoriaService,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IIdentidadClinicaService
{
    public async Task<IdentidadClinicaDto?> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        if (tenant.ClinicaId is not int clinicaId)
        {
            return null;
        }

        return await dbContext.Clinicas
            .AsNoTracking()
            .Where(clinic => clinic.Id == clinicaId)
            .Select(clinic => new IdentidadClinicaDto(
                clinic.Nombre, clinic.Rnc, clinic.Eslogan, clinic.Telefono,
                clinic.Correo, clinic.Horario, clinic.Direccion))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task ActualizarAsync(IdentidadClinicaFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "AcercaDe.Editar", "No tienes permiso para editar la información de la clínica.");
        ArgumentNullException.ThrowIfNull(model);
        ValidadorDto.PrepararYValidar(model);

        if (tenant.ClinicaId is not int clinicaId)
        {
            throw new InvalidOperationException("No hay clínica en la sesión.");
        }

        var clinica = await dbContext.Clinicas.SingleOrDefaultAsync(clinic => clinic.Id == clinicaId, cancellationToken)
            ?? throw new InvalidOperationException("La clínica no existe.");

        clinica.Nombre = model.Nombre;
        clinica.Rnc = Normalizar(model.Rnc);
        clinica.Eslogan = Normalizar(model.Eslogan);
        clinica.Telefono = Normalizar(model.Telefono);
        clinica.Correo = Normalizar(model.Correo);
        clinica.Horario = Normalizar(model.Horario);
        clinica.Direccion = Normalizar(model.Direccion);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Modificó", "Acerca De", nameof(SonrisaDigital.Data.Entities.Clinica), clinicaId,
            $"Modificó la información de la clínica {clinica.Nombre}.", cancellationToken, clinicaId);
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
