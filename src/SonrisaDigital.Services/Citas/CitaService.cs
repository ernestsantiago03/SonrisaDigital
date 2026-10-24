using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
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
    ILogger<CitaService> logger) : ICitaService
{
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

        if (model.PacienteId <= 0)
            throw new InvalidOperationException("El ID del paciente no es válido.");

        if (model.OdontologoId <= 0)
            throw new InvalidOperationException("El ID del odontólogo no es válido.");

        if (model.Fecha == default)
            throw new InvalidOperationException("La fecha no es válida.");

        if (model.HoraInicio == default || model.HoraFin == default)
            throw new InvalidOperationException("Las horas no son válidas.");

        if (model.HoraFin <= model.HoraInicio)
            throw new InvalidOperationException("La hora de fin debe ser posterior a la hora de inicio.");

        var paciente = await pacienteRepository.GetByIdAsync(model.PacienteId, cancellationToken);
        if (paciente is null)
            throw new InvalidOperationException("El paciente con ID " + model.PacienteId + " no existe.");

        if (paciente.Estado != EstadoRegistro.Activo)
            throw new InvalidOperationException("El paciente debe estar activo para agendar citas.");

        var odontologo = await odontologoRepository.GetByIdAsync(model.OdontologoId, cancellationToken);
        if (odontologo is null)
            throw new InvalidOperationException("El odontólogo con ID " + model.OdontologoId + " no existe.");

        if (odontologo.Estado != EstadoRegistro.Activo)
            throw new InvalidOperationException("El odontólogo debe estar activo para agendar citas.");

        var cita = new Cita
        {
            PacienteId = model.PacienteId,
            OdontologoId = model.OdontologoId,
            Fecha = model.Fecha,
            HoraInicio = model.HoraInicio,
            HoraFin = model.HoraFin,
            Motivo = model.Motivo,
            Estado = EstadoCita.Programada
        };

        try
        {
            await repository.AddAsync(cita, cancellationToken);

            await auditoriaService.RegistrarAsync(
                "Agendó", "Citas", nameof(Cita), cita.Id,
                $"Agendó una cita para {paciente.Nombres} {paciente.Apellidos} con {odontologo.Nombres} {odontologo.Apellidos} el {cita.Fecha:dd/MM/yyyy} a las {cita.HoraInicio:HH:mm}",
                cancellationToken);

            return cita.Id;
        }
        catch (Exception ex)
        {
            // No se expone el detalle técnico al usuario (Fase 1 - Parte F).
            // El error real se registra para diagnóstico del soporte.
            logger.LogError(ex, "Error al guardar la cita en la base de datos.");
            throw new InvalidOperationException("No se pudo guardar la cita. Intenta de nuevo o contacta soporte.");
        }
    }

    public async Task UpdateAsync(CitaFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Citas.Editar", "No tienes permiso para editar citas.");

        var cita = await repository.GetByIdAsync(model.Id, cancellationToken);
        if (cita is null)
            throw new InvalidOperationException("La cita no existe.");

        var paciente = await pacienteRepository.GetByIdAsync(model.PacienteId, cancellationToken);
        if (paciente is null)
            throw new InvalidOperationException("El paciente no existe.");

        var odontologo = await odontologoRepository.GetByIdAsync(model.OdontologoId, cancellationToken);
        if (odontologo is null)
            throw new InvalidOperationException("El odontólogo no existe.");

        if (model.HoraFin <= model.HoraInicio)
            throw new InvalidOperationException("La hora de fin debe ser posterior a la hora de inicio.");

        cita.PacienteId = model.PacienteId;
        cita.OdontologoId = model.OdontologoId;
        cita.Fecha = model.Fecha;
        cita.HoraInicio = model.HoraInicio;
        cita.HoraFin = model.HoraFin;
        cita.Motivo = model.Motivo;
        cita.Estado = model.Estado;

        // Si al editar la cita se la deja en estado Atendida, también se registra su consulta
        // clínica (el puente con Historia Clínica), igual que al pulsar "Marcar como atendida".
        await AsegurarConsultaDeAtencionAsync(cita, cancellationToken);

        await repository.UpdateAsync(cita, cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Modificó", "Citas", nameof(Cita), cita.Id,
            $"Modificó la cita de {paciente.Nombres} {paciente.Apellidos} el {cita.Fecha:dd/MM/yyyy} a las {cita.HoraInicio:HH:mm}",
            cancellationToken);
    }

    public async Task SetEstadoAsync(int id, EstadoCita estado, CancellationToken cancellationToken = default)
    {
        // Permiso según la acción que representa el nuevo estado (Fase 1 - Parte E).
        var permiso = estado switch
        {
            EstadoCita.Cancelada => "Citas.Cancelar",
            EstadoCita.Atendida => "Citas.Atender",
            _ => "Citas.Editar"
        };
        await authorizationService.AsegurarPermisoAsync(currentUserService, permiso, "No tienes permiso para cambiar el estado de la cita.");

        var cita = await repository.GetByIdAsync(id, cancellationToken);
        if (cita is null)
            throw new InvalidOperationException("La cita no existe.");

        cita.Estado = estado;

        // Puente con Historia Clínica: si la cita queda Atendida, se registra su consulta clínica.
        // Se hace ANTES de guardar el estado a propósito: si algo falla, la cita sigue como estaba
        // y se puede volver a intentar. Al revés, la cita quedaría "Atendida" sin consulta y ya no
        // habría botón para reintentar.
        await AsegurarConsultaDeAtencionAsync(cita, cancellationToken);

        await repository.UpdateAsync(cita, cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Cambió", "Citas", nameof(Cita), cita.Id,
            $"Cambió la cita de {cita.Paciente.Nombres} {cita.Paciente.Apellidos} a estado '{NombreEstado(estado)}'",
            cancellationToken);
    }

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
