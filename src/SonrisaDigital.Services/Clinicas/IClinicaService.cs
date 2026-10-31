using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Clinicas.DTOs;

namespace SonrisaDigital.Services.Clinicas;

/// <summary>Panel de clínicas. Solo lo usa el Dueño del sistema (permisos Clinicas.*).</summary>
public interface IClinicaService
{
    Task<IReadOnlyList<ClinicaListItemDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<ClinicaFormDto?> ObtenerFormularioAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CrearAsync(ClinicaFormDto model, CancellationToken cancellationToken = default);

    /// <summary>Actualiza los datos de identidad. El código de la clínica no cambia después de creada.</summary>
    Task ActualizarAsync(ClinicaFormDto model, CancellationToken cancellationToken = default);

    /// <summary>Marca la clínica como activa o suspendida. El bloqueo real de sus usuarios llega en la fase 7.</summary>
    Task CambiarEstadoAsync(int id, EstadoClinica estado, CancellationToken cancellationToken = default);
}
