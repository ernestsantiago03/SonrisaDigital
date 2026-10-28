using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Pacientes.DTOs;

namespace SonrisaDigital.Services.Pacientes;

public sealed class PacienteService(
    IPacienteRepository repository,
    IAuditoriaService auditoriaService,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IPacienteService
{
    // Límite de filas por página que acepta el servicio, aunque la pantalla pida más.
    private const int MaxPageSize = 50;

    public async Task<IReadOnlyList<PacienteListItemDto>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pacientes.Ver", "No tienes permiso para consultar pacientes.");

        var pacientes = await repository.SearchAsync(searchTerm, estado, cancellationToken);

        return pacientes.Select(ToListItem).ToList();
    }

    /// <summary>
    /// Devuelve una página de pacientes para la tabla principal. El repositorio hace
    /// el conteo y el recorte en SQL; aquí solo se convierten las filas al DTO.
    /// </summary>
    public async Task<PagedResult<PacienteListItemDto>> SearchPagedAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pacientes.Ver", "No tienes permiso para consultar pacientes.");

        var (pagina, tamano) = NormalizarPagina(page, pageSize);
        var result = await repository.SearchPagedAsync(searchTerm, estado, pagina, tamano, cancellationToken);

        return new PagedResult<PacienteListItemDto>(
            result.Items.Select(ToListItem).ToList(),
            result.Total,
            result.Page,
            result.PageSize);
    }

    public async Task<PacienteDetalleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pacientes.Ver", "No tienes permiso para consultar pacientes.");

        var patient = await repository.GetByIdAsync(id, cancellationToken);
        return patient is null ? null : ToDetail(patient);
    }

    public async Task<PacienteFormDto?> GetFormAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pacientes.Ver", "No tienes permiso para consultar pacientes.");

        var patient = await repository.GetByIdAsync(id, cancellationToken);
        return patient is null ? null : ToForm(patient);
    }

    /// <summary>
    /// Los datos clínicos se leen aparte del formulario administrativo. Así un usuario
    /// con acceso a pacientes no recibe alergias si no tiene permiso de historia clínica.
    /// </summary>
    public async Task<PacienteClinicoDto?> GetClinicoAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "HistoriaClinica.Ver", "No tienes permiso para consultar información clínica.");

        var patient = await repository.GetByIdAsync(id, cancellationToken);
        return patient is null ? null : ToClinico(patient);
    }

    public async Task<int> CreateAsync(
        PacienteFormDto model,
        PacienteClinicoDto? clinico = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pacientes.Crear", "No tienes permiso para registrar pacientes.");

        // El bloque clínico solo exige permiso clínico cuando realmente trae un dato.
        // Un formulario administrativo sin alergias no crea una historia vacía.
        if (TieneDatosClinicos(clinico))
        {
            await authorizationService.AsegurarPermisoAsync(currentUserService, "HistoriaClinica.Crear", "No tienes permiso para registrar información clínica.");
        }

        ValidarDatosClinicos(clinico);

        var normalizedIdentification = Normalize(model.Identificacion);
        await EnsureIdentificationIsAvailableAsync(normalizedIdentification, null, cancellationToken);

        var patient = new Paciente();
        ApplyForm(patient, model, normalizedIdentification);
        var alergiasRegistradas = ApplyAlergias(patient, clinico);
        patient.FechaRegistro = DateTime.UtcNow;
        patient.Estado = EstadoRegistro.Activo;

        await repository.AddAsync(patient, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Agregó", "Pacientes", nameof(Paciente), patient.Id,
            $"Agregó un paciente: {patient.Nombres} {patient.Apellidos}{SufijoAlergias(alergiasRegistradas)}", cancellationToken);

        return patient.Id;
    }

    public async Task UpdateAsync(
        PacienteFormDto model,
        PacienteClinicoDto? clinico = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pacientes.Editar", "No tienes permiso para editar pacientes.");

        var patient = await repository.GetByIdAsync(model.Id, cancellationToken)
            ?? throw new InvalidOperationException("El paciente no existe.");

        ValidarDatosClinicos(clinico);

        var normalizedIdentification = Normalize(model.Identificacion);
        await EnsureIdentificationIsAvailableAsync(normalizedIdentification, model.Id, cancellationToken);

        // Solo se pide el permiso clínico si el bloque recibido cambia las alergias.
        // El estado del paciente no se toca aquí: se cambia únicamente con SetStatusAsync.
        var alergiasRegistradas = false;
        if (clinico is not null && CambianAlergias(patient, clinico))
        {
            await authorizationService.AsegurarPermisoAsync(currentUserService, "HistoriaClinica.Editar", "No tienes permiso para editar información clínica.");
            alergiasRegistradas = ApplyAlergias(patient, clinico);
        }

        ApplyForm(patient, model, normalizedIdentification);
        await repository.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Modificó", "Pacientes", nameof(Paciente), patient.Id,
            $"Modificó el paciente: {patient.Nombres} {patient.Apellidos}{SufijoAlergias(alergiasRegistradas)}", cancellationToken);
    }

    public async Task SetStatusAsync(int id, EstadoRegistro estado, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pacientes.Desactivar", "No tienes permiso para activar o desactivar pacientes.");

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
    private static bool ApplyAlergias(Paciente patient, PacienteClinicoDto? model)
    {
        if (model is null) return false;

        var valor = ObtenerValorAlergias(model);

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

    /// <summary>
    /// Indica si el bloque clínico recibido cambiaría las alergias actuales del paciente.
    /// Se usa para no exigir permisos clínicos cuando el dato no cambia.
    /// </summary>
    private static bool CambianAlergias(Paciente patient, PacienteClinicoDto model) =>
        patient.HistoriaClinica?.Alergias != ObtenerValorAlergias(model);

    private static string? ObtenerValorAlergias(PacienteClinicoDto model) =>
        model.NoRefiereAlergias ? NoRefiereAlergiasTexto : Normalize(model.Alergias);

    private static bool TieneDatosClinicos(PacienteClinicoDto? model) =>
        model is not null && (model.NoRefiereAlergias || !string.IsNullOrWhiteSpace(model.Alergias));

    private static void ValidarDatosClinicos(PacienteClinicoDto? model)
    {
        if (model?.Alergias?.Length > 1000)
        {
            throw new InvalidOperationException("El detalle de alergias no puede superar los 1000 caracteres.");
        }
    }

    private static string SufijoAlergias(bool registradas) => registradas ? " (registró alergias)" : string.Empty;

    private static PacienteFormDto ToForm(Paciente patient) => new()
    {
        Id = patient.Id,
        Nombres = patient.Nombres,
        Apellidos = patient.Apellidos,
        Identificacion = patient.Identificacion,
        FechaNacimiento = patient.FechaNacimiento,
        Sexo = patient.Sexo,
        TipoSangre = patient.TipoSangre,
        Telefono = patient.Telefono,
        Correo = patient.Correo,
        Direccion = patient.Direccion,
        ContactoEmergenciaNombre = patient.ContactoEmergenciaNombre,
        ContactoEmergenciaTelefono = patient.ContactoEmergenciaTelefono,
        ContactoEmergenciaRelacion = patient.ContactoEmergenciaRelacion
    };

    private static PacienteClinicoDto ToClinico(Paciente patient) => new()
    {
        PacienteId = patient.Id,
        Alergias = patient.HistoriaClinica?.Alergias == NoRefiereAlergiasTexto
            ? null
            : patient.HistoriaClinica?.Alergias,
        NoRefiereAlergias = patient.HistoriaClinica?.Alergias == NoRefiereAlergiasTexto
    };

    // Convierte la entidad de base de datos en la fila que muestra la tabla.
    private static PacienteListItemDto ToListItem(Paciente patient) => new(
        patient.Id,
        $"{patient.Nombres} {patient.Apellidos}",
        patient.Identificacion,
        patient.TipoSangre,
        patient.Telefono,
        patient.Correo,
        patient.FechaRegistro,
        patient.Estado);

    // Página mínima 1 y tamaño dentro del límite permitido por el servicio.
    private static (int Page, int PageSize) NormalizarPagina(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

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