namespace SonrisaDigital.Data.Common;

/// <summary>
/// Resultado paginado de una consulta. Lo usan los repositorios para devolver
/// una página de datos junto con el total de registros que cumplen el filtro.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize <= 0
        ? 1
        : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int pageSize) => new([], 0, 1, pageSize);
}
