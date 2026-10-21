using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Odontologos.DTOs;

namespace SonrisaDigital.Services.Odontologos;

public sealed class OdontologoService(
    IOdontologoRepository repository,
    IAuditoriaService auditoriaService) : IOdontologoService
{
    public async Task<IReadOnlyList<OdontologoListItemDto>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default)
    {
        var odontologos = await repository.SearchAsync(searchTerm, estado, cancellationToken);

        return odontologos
            .Select(odontologo => new OdontologoListItemDto(
                odontologo.Id,
                $"{odontologo.Nombres} {odontologo.Apellidos}",
                odontologo.NumeroLicencia,
                odontologo.Especialidad,
                odontologo.Telefono,
                odontologo.Correo,
                odontologo.Estado))
            .ToList();
    }

    public async Task<OdontologoDetalleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var odontologo = await repository.GetByIdAsync(id, cancellationToken);
        return odontologo is null ? null : ToDetail(odontologo);
    }

    public async Task<OdontologoFormDto?> GetFormAsync(int id, CancellationToken cancellationToken = default)
    {
        var odontologo = await repository.GetByIdAsync(id, cancellationToken);
        return odontologo is null ? null : ToForm(odontologo);
    }

    public async Task<int> CreateAsync(OdontologoFormDto model, CancellationToken cancellationToken = default)
    {
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
        var odontologo = await repository.GetByIdAsync(model.Id, cancellationToken)
            ?? throw new InvalidOperationException("El odontólogo no existe.");

        ApplyForm(odontologo, model);
        odontologo.Estado = model.Estado;
        await repository.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Modificó", "Odontólogos", nameof(Odontologo), odontologo.Id,
            $"Modificó el odontólogo: {odontologo.Nombres} {odontologo.Apellidos}", cancellationToken);
    }

    public async Task SetStatusAsync(int id, EstadoRegistro estado, CancellationToken cancellationToken = default)
    {
        var odontologo = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("El odontólogo no existe.");

        odontologo.Estado = estado;
        await repository.SaveChangesAsync(cancellationToken);

        var verbo = estado == EstadoRegistro.Activo ? "Activó" : "Desactivó";
        await auditoriaService.RegistrarAsync(
            verbo, "Odontólogos", nameof(Odontologo), odontologo.Id,
            $"{verbo} el odontólogo: {odontologo.Nombres} {odontologo.Apellidos}", cancellationToken);
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
