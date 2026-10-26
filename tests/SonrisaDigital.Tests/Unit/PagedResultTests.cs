using SonrisaDigital.Data.Common;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Valida los cálculos que usan todas las tablas paginadas para mostrar el
/// total de páginas y habilitar o deshabilitar los botones de navegación.
/// </summary>
public sealed class PagedResultTests
{
    [Fact]
    public void CalculaTotalDePaginasYBotones()
    {
        var pagina = new PagedResult<int>([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], 25, 1, 10);

        Assert.Equal(3, pagina.TotalPages);
        Assert.False(pagina.HasPrevious);
        Assert.True(pagina.HasNext);
    }

    [Fact]
    public void UltimaPaginaNoTieneSiguiente()
    {
        var pagina = new PagedResult<int>([21, 22, 23, 24, 25], 25, 3, 10);

        Assert.Equal(3, pagina.TotalPages);
        Assert.True(pagina.HasPrevious);
        Assert.False(pagina.HasNext);
    }

    [Fact]
    public void ResultadoVacioSiempreTieneUnaPagina()
    {
        var pagina = PagedResult<string>.Empty(10);

        Assert.Empty(pagina.Items);
        Assert.Equal(0, pagina.Total);
        Assert.Equal(1, pagina.TotalPages);
        Assert.False(pagina.HasPrevious);
        Assert.False(pagina.HasNext);
    }
}
