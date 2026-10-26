using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data.Repositories;

public interface IOdontologoRepository
{
    Task<IReadOnlyList<Odontologo>> SearchAsync(string? searchTerm, EstadoRegistro? estado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve una página de odontólogos y el total que cumple el filtro. El conteo
    /// y el recorte se hacen en SQL, sin cargar todos los registros en memoria.
    /// </summary>
    Task<PagedResult<Odontologo>> SearchPagedAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta odontólogos sin traer las filas completas. El filtro por fecha usa la fecha
    /// de registro en UTC, que es como se guarda en la base de datos.
    /// </summary>
    /// <param name="estado">Si se indica, cuenta solo los odontólogos en ese estado.</param>
    /// <param name="registradosDesdeUtc">Si se indica, cuenta solo los registrados desde esa fecha UTC.</param>
    /// <param name="cancellationToken">Token para cancelar la consulta.</param>
    Task<int> CountAsync(
        EstadoRegistro? estado = null,
        DateTime? registradosDesdeUtc = null,
        CancellationToken cancellationToken = default);

    Task<Odontologo?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Odontologo odontologo, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
