using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Pacientes.DTOs;

namespace SonrisaDigital.Services.Pacientes;

public sealed class PacienteService(
    IPacienteRepository repository,
    IAuditoriaService auditoriaService) : IPacienteService
{
    public async Task<IReadOnlyList<PacienteListItemDto>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default)
    {
        var pacientes = await repository.SearchAsync(searchTerm, estado, cancellationToken);

        return pacientes
            .Select(patient => new PacienteListItemDto(
                patient.Id,
                $"{patient.Nombres} {patient.Apellidos}",
                patient.Identificacion,
                patient.TipoSangre,
                patient.Telefono,
                patient.Correo,
                patient.FechaRegistro,
                patient.Estado))
            .ToList();
    }

    public async Task<PacienteDetalleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var patient = await repository.GetByIdAsync(id, cancellationToken);
        return patient is null ? null : ToDetail(patient);
    }

    public async Task<PacienteFormDto?> GetFormAsync(int id, CancellationToken cancellationToken = default)
    {
        var patient = await repository.GetByIdAsync(id, cancellationToken);
        return patient is null ? null : ToForm(patient);
    }

    public async Task<int> CreateAsync(PacienteFormDto model, CancellationToken cancellationToken = default)
    {
        var normalizedIdentification = Normalize(model.Identificacion);
        await EnsureIdentificationIsAvailableAsync(normalizedIdentification, null, cancellationToken);

        var patient = new Paciente();
        ApplyForm(patient, model, normalizedIdentification);
        var alergiasRegistradas = ApplyAlergias(patient, model);
        patient.FechaRegistro = DateTime.UtcNow;
        patient.Estado = EstadoRegistro.Activo;

        await repository.AddAsync(patient, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Agregó", "Pacientes", nameof(Paciente), patient.Id,
            $"Agregó un paciente: {patient.Nombres} {patient.Apellidos}{SufijoAlergias(alergiasRegistradas)}", cancellationToken);

        return patient.Id;
    }

    public async Task UpdateAsync(PacienteFormDto model, CancellationToken cancellationToken = default)
    {
        var patient = await repository.GetByIdAsync(model.Id, cancellationToken)
            ?? throw new InvalidOperationException("El paciente no existe.");

        var normalizedIdentification = Normalize(model.Identificacion);
        await EnsureIdentificationIsAvailableAsync(normalizedIdentification, model.Id, cancellationToken);

        ApplyForm(patient, model, normalizedIdentification);
        var alergiasRegistradas = ApplyAlergias(patient, model);
        patient.Estado = model.Estado;
        await repository.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Modificó", "Pacientes", nameof(Paciente), patient.Id,
            $"Modificó el paciente: {patient.Nombres} {patient.Apellidos}{SufijoAlergias(alergiasRegistradas)}", cancellationToken);
    }

    public async Task SetStatusAsync(int id, EstadoRegistro estado, CancellationToken cancellationToken = default)
    {
        var patient = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("El paciente no existe.");

        patient.Estado = estado;
        await repository.SaveChangesAsync(cancellationToken);

        var verbo = estado == EstadoRegistro.Activo ? "Activó" : "Desactivó";
        await auditoriaService.RegistrarAsync(
            verbo, "Pacientes", nameof(Paciente), patient.Id,
            $"{verbo} el paciente: {patient.Nombres} {patient.Apellidos}", cancellationToken);
    }

    private async Task EnsureIdentificationIsAvailableAsync(string? identification, int? excludedId, CancellationToken cancellationToken)
    {
        if (identification is not null && await repository.ExistsWithIdentificationAsync(identification, excludedId, cancellationToken))
        {
            throw new InvalidOperationException("La identificación ya está registrada para otro paciente.");
        }
    }

    private static void ApplyForm(Paciente patient, PacienteFormDto model, string? identification)
    {
        patient.Nombres = model.Nombres.Trim();
        patient.Apellidos = model.Apellidos.Trim();
        patient.Identificacion = identification;
        patient.FechaNacimiento = model.FechaNacimiento;
        patient.Sexo = Normalize(model.Sexo);
        patient.TipoSangre = Normalize(model.TipoSangre);
        patient.Telefono = Normalize(model.Telefono);
        patient.Correo = Normalize(model.Correo);
        patient.Direccion = Normalize(model.Direccion);
        patient.ContactoEmergenciaNombre = Normalize(model.ContactoEmergenciaNombre);
        patient.ContactoEmergenciaTelefono = Normalize(model.ContactoEmergenciaTelefono);
        patient.ContactoEmergenciaRelacion = Normalize(model.ContactoEmergenciaRelacion);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Texto explicito para dejar constancia de que SI se le pregunto al paciente.
    // Asi "sin registrar" (Alergias nulo) no se confunde con "no refiere alergias".
    private const string NoRefiereAlergiasTexto = "No refiere alergias.";

    /// <summary>
    /// Guarda las alergias en la HistoriaClinica del paciente (creandola solo cuando hay un dato
    /// clinico que registrar). Devuelve true si el dato clinico cambio.
    /// </summary>
    private static bool ApplyAlergias(Paciente patient, PacienteFormDto model)
    {
        var valor = model.NoRefiereAlergias ? NoRefiereAlergiasTexto : Normalize(model.Alergias);

        if (patient.HistoriaClinica is null)
        {
            if (valor is null) return false;
            patient.HistoriaClinica = new HistoriaClinica { Alergias = valor };
            return true;
        }

        if (patient.HistoriaClinica.Alergias == valor) return false;

        patient.HistoriaClinica.Alergias = valor;
        patient.HistoriaClinica.FechaModificacion = DateTime.UtcNow;
        return true;
    }

    private static string SufijoAlergias(bool registradas) => registradas ? " (registró alergias)" : string.Empty;

    private static PacienteFormDto ToForm(Paciente patient) => new()
    {
        Id = patient.Id,
        Estado = patient.Estado,
        Nombres = patient.Nombres,
        Apellidos = patient.Apellidos,
        Identificacion = patient.Identificacion,
        FechaNacimiento = patient.FechaNacimiento,
        Sexo = patient.Sexo,
        TipoSangre = patient.TipoSangre,
        Alergias = patient.HistoriaClinica?.Alergias == NoRefiereAlergiasTexto ? null : patient.HistoriaClinica?.Alergias,
        NoRefiereAlergias = patient.HistoriaClinica?.Alergias == NoRefiereAlergiasTexto,
        Telefono = patient.Telefono,
        Correo = patient.Correo,
        Direccion = patient.Direccion,
        ContactoEmergenciaNombre = patient.ContactoEmergenciaNombre,
        ContactoEmergenciaTelefono = patient.ContactoEmergenciaTelefono,
        ContactoEmergenciaRelacion = patient.ContactoEmergenciaRelacion
    };

    private static PacienteDetalleDto ToDetail(Paciente patient) => new(
        patient.Id,
        patient.Nombres,
        patient.Apellidos,
        patient.Identificacion,
        patient.FechaNacimiento,
        patient.Sexo,
        patient.TipoSangre,
        patient.Telefono,
        patient.Correo,
        patient.Direccion,
        patient.ContactoEmergenciaNombre,
        patient.ContactoEmergenciaTelefono,
        patient.ContactoEmergenciaRelacion,
        patient.FechaRegistro,
        patient.Estado);
}