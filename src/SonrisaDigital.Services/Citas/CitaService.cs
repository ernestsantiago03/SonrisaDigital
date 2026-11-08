using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Validacion;
using SonrisaDigital.Services.Citas.DTOs;
using SonrisaDigital.Services.HistoriasClinicas;

namespace SonrisaDigital.Services.Citas;

public sealed class CitaService(
    ICitaRepository repository,
    IPacienteRepository pacienteRepository,
    IOdontologoRepository odontologoRepository,
    IHistoriaClinicaService historiaClinicaService,
    IAuditoriaService auditoriaService,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService,
    ApplicationDbContext dbContext,
    ILogger<CitaService> logger) : ICitaService
{
    // Horario de atención que también se valida en el servicio, no solo en la pantalla.
    private static readonly TimeOnly HoraApertura = new(8, 0);
    private static readonly TimeOnly HoraCierre = new(18, 0);
    private static readonly TimeOnly AlmuerzoInicio = new(12, 0);
    private static readonly TimeOnly AlmuerzoFin = new(14, 0);

    // Límite de filas por página que acepta el servicio, aunque la pantalla pida más.
    private const int MaxPageSize = 50;

    public async Task<IReadOnlyList<CitaListItemDto>> SearchAsync(
        DateOnly? fecha = null,
        int? pacienteId = null,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Citas.Ver", "No tienes permiso para consultar citas.");

        var citas = await repository.SearchAsync(fecha, pacienteId, odontologoId, estado, cancellationToken);

        return citas.Select(ToListItem).ToList();
    }

    public async Task<IReadOnlyList<CitaListItemDto>> SearchRangoAsync(
        DateOnly desde,
        DateOnly hasta,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Citas.Ver", "No tienes permiso para consultar citas.");

        var citas = await repository.SearchRangoAsync(desde, hasta, odontologoId, estado, cancellationToken);

        return citas.Select(ToListItem).ToList();
    }

    /// <summary>
    /// Devuelve una página de citas para la tabla principal. El repositorio hace el
    /// conteo y el recorte en SQL; aquí solo se convierten las filas al DTO.
    /// </summary>
    public async Task<PagedResult<CitaListItemDto>> SearchPagedAsync(
        DateOnly? fecha = null,
        int? pacienteId = null,
        int? odontologoId = null,
        EstadoCita? estado = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Citas.Ver", "No tienes permiso para consultar citas.");

        var (pagina, tamano) = NormalizarPagina(page, pageSize);
        var result = await repository.SearchPagedAsync(
            fecha,
            pacienteId,
            odontologoId,
            estado,
            pagina,
            tamano,
            cancellationToken);

        return new PagedResult<CitaListItemDto>(
            result.Items.Select(ToListItem).ToList(),
            result.Total,
            result.Page,
            result.PageSize);
    }

    public async Task<CitaDetalleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Citas.Ver", "No tienes permiso para consultar citas.");

        var cita = await repository.GetByIdAsync(id, cancellationToken);
        return cita is null ? null : ToDetail(cita);
    }

    public async Task<CitaFormDto?> GetFormAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Citas.Ver", "No tienes permiso para consultar citas.");

        var cita = await repository.GetByIdAsync(id, cancellationToken);
        return cita is null ? null : ToForm(cita);
    }

    public async Task<int> CreateAsync(CitaFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Citas.Crear", "No tienes permiso para crear citas.");
        ArgumentNullException.ThrowIfNull(model);
        ValidadorDto.PrepararYValidar(model);
        ValidarDatosCita(model);
        ValidarNoEsPasada(model, RelojNegocio.Ahora());

        var citaId = 0;

        try
        {
            // La comprobación de choque y el guardado van en la misma transacción para que
            // dos recepcionistas no reserven el mismo hueco al mismo tiempo.
            await dbContext.ExecuteInTransactionAsync(async () =>
            {
                var paciente = await pacienteRepository.GetByIdAsync(model.PacienteId, cancellationToken);
                if (paciente is null)
                    throw new InvalidOperationException("El paciente seleccionado no existe.");

                if (paciente.Estado != EstadoRegistro.Activo)
                    throw new InvalidOperationException("El paciente debe estar activo para agendar citas.");

                var odontologo = await odontologoRepository.GetByIdAsync(model.OdontologoId, cancellationToken);
                if (odontologo is null)
                    throw new InvalidOperationException("El odontólogo seleccionado no existe.");

                if (odontologo.Estado != EstadoRegistro.Activo)
                    throw new InvalidOperationException("El odontólogo debe estar activo para agendar citas.");

                var conflicto = await repository.BuscarConflictoAsync(
                    model.OdontologoId,
                    model.PacienteId,
                    model.Fecha,
                    model.HoraInicio,
                    model.HoraFin,
                    cancellationToken: cancellationToken);

                ValidarConflicto(conflicto);

                var cita = new Cita
                {
                    PacienteId = model.PacienteId,
                    OdontologoId = model.OdontologoId,
                    Fecha = model.Fecha,
                    HoraInicio = model.HoraInicio,
                    HoraFin = model.HoraFin,
                    Motivo = NormalizarTexto(model.Motivo),
                    Estado = EstadoCita.Programada
                };

                await repository.AddAsync(cita, cancellationToken);

                await auditoriaService.RegistrarAsync(
                    "Agendó", "Citas", nameof(Cita), cita.Id,
                    $"Agendó una cita para {paciente.Nombres} {paciente.Apellidos} con {odontologo.Nombres} {odontologo.Apellidos} el {cita.Fecha:dd/MM/yyyy} a las {cita.HoraInicio:HH:mm}",
                    cancellationToken);

                citaId = cita.Id;
            }, IsolationLevel.Serializable, cancellationToken);

            return citaId;
        }
        catch (InvalidOperationException)
        {
            // Los mensajes de validación ya son aptos para el usuario.
            throw;
        }
        catch (Exception ex)
        {
            // El detalle técnico queda en el log; al usuario se le da un mensaje genérico.
            logger.LogError(ex, "Error al guardar la cita en la base de datos.");
            throw new InvalidOperationException("No se pudo guardar la cita. Intenta de nuevo o contacta soporte.");
        }
    }

    public async Task UpdateAsync(CitaFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Citas.Editar", "No tienes permiso para editar citas.");
        ArgumentNullException.ThrowIfNull(model);
        ValidadorDto.PrepararYValidar(model);

        if (model.Id <= 0)
            throw new InvalidOperationException("La cita no es válida.");

        ValidarDatosCita(model);

        try
        {
            await dbContext.ExecuteInTransactionAsync(async () =>
            {
                var cita = await repository.GetByIdAsync(model.Id, cancellationToken)
                    ?? throw new InvalidOperationException("La cita no existe.");

                if (CitaStateMachine.EsEstadoFinal(cita.Estado))
                    throw new InvalidOperationException("Esta cita ya está cerrada y no se puede editar.");

                // El recepcionista solo puede editar mientras la cita está Programada.
                // El servicio repite esta regla para que no dependa de la pantalla.
                if (cita.Estado == EstadoCita.Confirmada &&
                    currentUserService.Principal?.IsInRole(RolesSonrisaDigital.Recepcionista) == true)
                {
                    throw new InvalidOperationException(
                        "Esta cita ya está confirmada y no puede editarse. El odontólogo es quien la marca como atendida.");
                }

                // Una cita no cambia de paciente, fecha ni motivo después de crearse.
                // Para reprogramarla se crea una cita nueva desde el detalle.
                if (model.PacienteId != cita.PacienteId)
                    throw new InvalidOperationException("El paciente de una cita no se puede cambiar.");

                if (model.Fecha != cita.Fecha)
                    throw new InvalidOperationException(
                        "La fecha de una cita no se puede cambiar. Crea una cita nueva para reprogramarla.");

                if (!string.Equals(
                        NormalizarTexto(model.Motivo),
                        NormalizarTexto(cita.Motivo),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("El motivo de una cita no se puede cambiar.");
                }

                var paciente = await pacienteRepository.GetByIdAsync(cita.PacienteId, cancellationToken)
                    ?? throw new InvalidOperationException("El paciente seleccionado no existe.");

                if (paciente.Estado != EstadoRegistro.Activo)
                    throw new InvalidOperationException("El paciente debe estar activo para agendar citas.");

                var odontologo = await odontologoRepository.GetByIdAsync(model.OdontologoId, cancellationToken)
                    ?? throw new InvalidOperationException("El odontólogo seleccionado no existe.");

                if (odontologo.Estado != EstadoRegistro.Activo)
                    throw new InvalidOperationException("El odontólogo debe estar activo para agendar citas.");

                var conflicto = await repository.BuscarConflictoAsync(
                    model.OdontologoId,
                    cita.PacienteId,
                    cita.Fecha,
                    model.HoraInicio,
                    model.HoraFin,
                    model.Id,
                    cancellationToken);

                ValidarConflicto(conflicto);

                // Solo se modifican los datos que la pantalla permite cambiar.
                // El estado se cambia exclusivamente con SetEstadoAsync.
                cita.OdontologoId = model.OdontologoId;
                cita.HoraInicio = model.HoraInicio;
                cita.HoraFin = model.HoraFin;

                await repository.UpdateAsync(cita, cancellationToken);

                await auditoriaService.RegistrarAsync(
                    "Modificó", "Citas", nameof(Cita), cita.Id,
                    $"Modificó la cita de {paciente.Nombres} {paciente.Apellidos} el {cita.Fecha:dd/MM/yyyy} a las {cita.HoraInicio:HH:mm}",
                    cancellationToken);
            }, IsolationLevel.Serializable, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al actualizar la cita en la base de datos.");
            throw new InvalidOperationException("No se pudo actualizar la cita. Intenta de nuevo o contacta soporte.");
        }
    }

    public async Task SetEstadoAsync(int id, EstadoCita estado, CancellationToken cancellationToken = default)
    {
        // El permiso se comprueba antes de leer la cita para no revelar si existe.
        // La transición concreta se valida después contra el estado guardado.
        var permiso = CitaStateMachine.PermisoRequeridoParaEstado(estado);
        await authorizationService.AsegurarPermisoAsync(
            currentUserService,
            permiso,
            "No tienes permiso para cambiar el estado de la cita.");

        try
        {
            await dbContext.ExecuteInTransactionAsync(async () =>
            {
                var cita = await repository.GetByIdAsync(id, cancellationToken)
                    ?? throw new InvalidOperationException("La cita no existe.");

                // Si la cita ya está en ese estado no hay nada que guardar ni auditar.
                if (cita.Estado == estado)
                {
                    return;
                }

                // La transición se valida contra el estado real de la base, no contra
                // el estado que la pantalla tenía cargado.
                CitaStateMachine.ValidarTransicion(cita.Estado, estado);

                cita.Estado = estado;

                // Si la cita queda Atendida, la consulta clínica y el cambio de estado se
                // guardan juntos. Así no queda una cita atendida sin su consulta.
                await AsegurarConsultaDeAtencionAsync(cita, cancellationToken);

                await repository.UpdateAsync(cita, cancellationToken);

                await auditoriaService.RegistrarAsync(
                    "Cambió", "Citas", nameof(Cita), cita.Id,
                    $"Cambió la cita de {cita.Paciente.Nombres} {cita.Paciente.Apellidos} a estado '{CitaStateMachine.NombreEstado(estado)}'",
                    cancellationToken);
            }, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            // La Historia Clínica también puede negar el permiso al registrar la
            // consulta de una cita atendida; se reenvía el mensaje tal cual.
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al cambiar el estado de la cita {CitaId}.", id);
            throw new InvalidOperationException("No se pudo cambiar el estado de la cita. Intenta de nuevo o contacta soporte.");
        }
    }

    /// <summary>
    /// Reglas de agenda que deben cumplirse aunque la cita llegue desde otro cliente, no solo
    /// desde el formulario. Se validan antes de abrir la transacción.
    /// </summary>
    private static void ValidarDatosCita(CitaFormDto model)
    {
        if (model.PacienteId <= 0)
            throw new InvalidOperationException("El paciente es obligatorio.");

        if (model.OdontologoId <= 0)
            throw new InvalidOperationException("El odontólogo es obligatorio.");

        if (model.Fecha == default)
            throw new InvalidOperationException("La fecha no es válida.");

        if (model.HoraInicio == default || model.HoraFin == default)
            throw new InvalidOperationException("Las horas no son válidas.");

        if (model.HoraFin <= model.HoraInicio)
            throw new InvalidOperationException("La hora de fin debe ser posterior a la hora de inicio.");

        if (model.Fecha.DayOfWeek is DayOfWeek.Sunday)
            throw new InvalidOperationException("Las citas solo pueden programarse de lunes a sábado.");

        if (model.HoraInicio < HoraApertura || model.HoraFin > HoraCierre)
            throw new InvalidOperationException("El horario de atención es de 8:00 a.m. a 6:00 p.m.");

        if (model.HoraInicio < AlmuerzoInicio && model.HoraFin > AlmuerzoInicio)
            throw new InvalidOperationException("La cita no puede cruzar el horario de almuerzo (12:00 a 2:00 p.m.).");

        if (model.HoraInicio >= AlmuerzoInicio && model.HoraInicio < AlmuerzoFin)
            throw new InvalidOperationException("No se agendan citas durante el almuerzo (12:00 a 2:00 p.m.).");
    }

    /// <summary>
    /// Una cita nueva no puede quedar en una fecha ni en una hora que ya pasó, según la hora de la clínica.
    /// </summary>
    public static void ValidarNoEsPasada(CitaFormDto model, DateTime ahora)
    {
        var hoy = DateOnly.FromDateTime(ahora);

        if (model.Fecha < hoy)
            throw new InvalidOperationException("No se pueden agendar citas en fechas pasadas.");

        if (model.Fecha == hoy && model.HoraInicio <= TimeOnly.FromDateTime(ahora))
            throw new InvalidOperationException("No se pueden agendar citas en una hora que ya pasó.");
    }

    /// <summary>
    /// Convierte el resultado de la consulta de choque en un mensaje claro para el usuario.
    /// </summary>
    private static void ValidarConflicto(CitaConflicto conflicto)
    {
        if (conflicto.OdontologoOcupado && conflicto.PacienteOcupado)
            throw new InvalidOperationException("El odontólogo y el paciente ya tienen una cita en ese horario.");

        if (conflicto.OdontologoOcupado)
            throw new InvalidOperationException("El odontólogo ya tiene una cita en ese horario. Elige otra hora.");

        if (conflicto.PacienteOcupado)
            throw new InvalidOperationException("El paciente ya tiene una cita en ese horario. Elige otra hora.");
    }

    private static string? NormalizarTexto(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>
    /// Puente entre los módulos de Citas y de Historia Clínica: cuando una cita queda en estado
    /// Atendida, se registra su consulta clínica (una sola por cita). Con cualquier otro estado
    /// no hace nada.
    /// </summary>
    private async Task AsegurarConsultaDeAtencionAsync(Cita cita, CancellationToken cancellationToken)
    {
        if (cita.Estado != EstadoCita.Atendida)
        {
            return;
        }

        await historiaClinicaService.RegistrarConsultaDeCitaAsync(cita, cancellationToken);
    }

    // Convierte la entidad de base de datos en la fila que muestra la tabla.
    private static CitaListItemDto ToListItem(Cita cita) => new(
        cita.Id,
        $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}",
        $"{cita.Odontologo.Nombres} {cita.Odontologo.Apellidos}",
        cita.Fecha,
        cita.HoraInicio,
        cita.HoraFin,
        cita.Motivo,
        cita.Estado);

    // Página mínima 1 y tamaño dentro del límite permitido por el servicio.
    private static (int Page, int PageSize) NormalizarPagina(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

    private static CitaDetalleDto ToDetail(Cita cita) =>
        new(
            cita.Id,
            $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}",
            cita.Paciente.Id,
            $"{cita.Odontologo.Nombres} {cita.Odontologo.Apellidos}",
            cita.Odontologo.Id,
            cita.Fecha,
            cita.HoraInicio,
            cita.HoraFin,
            cita.Motivo,
            cita.Estado,
            cita.FechaCreacion,
            // La consulta clínica de la cita (null mientras la cita no se haya atendido).
            cita.ConsultaClinica?.Id);

    private static CitaFormDto ToForm(Cita cita) =>
        new()
        {
            Id = cita.Id,
            PacienteId = cita.PacienteId,
            OdontologoId = cita.OdontologoId,
            Fecha = cita.Fecha,
            HoraInicio = cita.HoraInicio,
            HoraFin = cita.HoraFin,
            Motivo = cita.Motivo,
            EstadoActual = cita.Estado
        };
}
