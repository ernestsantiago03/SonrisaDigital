using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface ITratamientoRepository
{
    // ---------------------------------------------------------------------
    // Catálogo
    // ---------------------------------------------------------------------
    Task<PagedResult<Tratamiento>> SearchCatalogoAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Tratamiento?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Tratamiento?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsNombreAsync(
        string nombre,
        int? excludedId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tratamiento>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tratamiento>> GetActivosAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Tratamiento tratamiento, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------------
    // Tratamientos realizados
    // ---------------------------------------------------------------------
    Task<PagedResult<TratamientoRealizado>> SearchRealizadosAsync(
        string? searchTerm,
        EstadoTratamiento? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<TratamientoRealizado?> GetRealizadoAsync(int id, CancellationToken cancellationToken = default);

    Task<TratamientoRealizado?> GetRealizadoForUpdateAsync(int id, CancellationToken cancellationToken = default);

    Task AddRealizadoAsync(TratamientoRealizado tratamiento, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------------
    // Catálogos de apoyo para los formularios
    // ---------------------------------------------------------------------
    Task<IReadOnlyList<Paciente>> GetPacientesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Odontologo>> GetOdontologosAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PiezaDental>> GetPiezasAsync(CancellationToken cancellationToken = default);

    Task<Paciente?> GetPacienteAsync(int id, CancellationToken cancellationToken = default);

    Task<Odontologo?> GetOdontologoAsync(int id, CancellationToken cancellationToken = default);

    Task<PiezaDental?> GetPiezaAsync(int id, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
