using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
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

    public async Task<IReadOnlyList<CitaListItemDto>> SearchAsync(
        DateOnly? fecha = null,
        int? pacienteId = null,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Citas.Ver", "No tienes permiso para consultar citas.");

        var citas = await repository.SearchAsync(fecha, pacienteId, odontologoId, estado, cancellationToken);

        return citas
            .Select(cita => new CitaListItemDto(
                cita.Id,
                $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}",
                $"{cita.Odontologo.Nombres} {cita.Odontologo.Apellidos}",
                cita.Fecha,
                cita.HoraInicio,
                cita.HoraFin,
                cita.Motivo,
                cita.Estado))
            .ToList();
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
        ValidarDatosCita(model);

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

        if (model.Id <= 0)
            throw new InvalidOperationException("La cita no es válida.");

        ValidarDatosCita(model);

        try
        {
            await dbContext.ExecuteInTransactionAsync(async () =>
            {
                var cita = await repository.GetByIdAsync(model.Id, cancellationToken)
                    ?? throw new InvalidOperationException("La cita no existe.");

                if (cita.Estado is EstadoCita.Atendida or EstadoCita.Cancelada or EstadoCita.NoAsistio)
                    throw new InvalidOperationException("Esta cita ya está cerrada y no se puede editar.");

                var paciente = await pacienteRepository.GetByIdAsync(model.PacienteId, cancellationToken)
                    ?? throw new InvalidOperationException("El paciente seleccionado no existe.");

                if (paciente.Estado != EstadoRegistro.Activo)
                    throw new InvalidOperationException("El paciente debe estar activo para agendar citas.");

                var odontologo = await odontologoRepository.GetByIdAsync(model.OdontologoId, cancellationToken)
                    ?? throw new InvalidOperationException("El odontólogo seleccionado no existe.");

                if (odontologo.Estado != EstadoRegistro.Activo)
                    throw new InvalidOperationException("El odontólogo debe estar activo para agendar citas.");

                // Cancelar o marcar una inasistencia libera el horario; en esos casos no se
                // bloquea la edición por un choque con otra cita.
                if (model.Estado is not (EstadoCita.Cancelada or EstadoCita.NoAsistio))
                {
                    var conflicto = await repository.BuscarConflictoAsync(
                        model.OdontologoId,
                        model.PacienteId,
                        model.Fecha,
                        model.HoraInicio,
                        model.HoraFin,
                        model.Id,
                        cancellationToken);

                    ValidarConflicto(conflicto);
                }

                cita.PacienteId = model.PacienteId;
                cita.OdontologoId = model.OdontologoId;
                cita.Fecha = model.Fecha;
                cita.HoraInicio = model.HoraInicio;
                cita.HoraFin = model.HoraFin;
                cita.Motivo = NormalizarTexto(model.Motivo);
                cita.Estado = model.Estado;

                // Si al editar la cita se la deja en estado Atendida, también se registra su
                // consulta clínica, igual que al pulsar "Marcar como atendida".
                await AsegurarConsultaDeAtencionAsync(cita, cancellationToken);

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
        // Permiso según la acción que representa el nuevo estado.
        var permiso = estado switch
        {
            EstadoCita.Cancelada => "Citas.Cancelar",
            EstadoCita.Atendida => "Citas.Atender",
            _ => "Citas.Editar"
        };
        await authorizationService.AsegurarPermisoAsync(currentUserService, permiso, "No tienes permiso para cambiar el estado de la cita.");

        try
        {
            await dbContext.ExecuteInTransactionAsync(async () =>
            {
                var cita = await repository.GetByIdAsync(id, cancellationToken)
                    ?? throw new InvalidOperationException("La cita no existe.");

                cita.Estado = estado;

                // Si la cita queda Atendida, la consulta clínica y el cambio de estado se
                // guardan juntos. Así no queda una cita atendida sin su consulta.
                await AsegurarConsultaDeAtencionAsync(cita, cancellationToken);

                await repository.UpdateAsync(cita, cancellationToken);

                await auditoriaService.RegistrarAsync(
                    "Cambió", "Citas", nameof(Cita), cita.Id,
                    $"Cambió la cita de {cita.Paciente.Nombres} {cita.Paciente.Apellidos} a estado '{NombreEstado(estado)}'",
                    cancellationToken);
            }, cancellationToken);
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

        if (model.Fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            throw new InvalidOperationException("Las citas solo pueden programarse de lunes a viernes.");

        if (model.HoraInicio < HoraApertura || model.HoraFin > HoraCierre)
            throw new InvalidOperationException("El horario de atención es de 8:00 a.m. a 6:00 p.m.");

        if (model.HoraInicio < AlmuerzoInicio && model.HoraFin > AlmuerzoInicio)
            throw new InvalidOperationException("La cita no puede cruzar el horario de almuerzo (12:00 a 2:00 p.m.).");

        if (model.HoraInicio >= AlmuerzoInicio && model.HoraInicio < AlmuerzoFin)
            throw new InvalidOperationException("No se agendan citas durante el almuerzo (12:00 a 2:00 p.m.).");
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

    private static string NombreEstado(EstadoCita estado) => estado switch
    {
        EstadoCita.Programada => "Programada",
        EstadoCita.Confirmada => "Confirmada",
        EstadoCita.Atendida => "Atendida",
        EstadoCita.Cancelada => "Cancelada",
        EstadoCita.NoAsistio => "No asistió",
        _ => estado.ToString()
    };

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
            Estado = cita.Estado
        };
}
