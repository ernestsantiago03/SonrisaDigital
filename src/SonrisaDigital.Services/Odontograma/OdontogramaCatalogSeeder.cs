using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Catalogos;

namespace SonrisaDigital.Services.Odontograma;

public static class OdontogramaCatalogSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext dbContext,
        ICatalogoCache? catalogoCache = null,
        CancellationToken cancellationToken = default)
    {
        var existingNumbers = await dbContext.PiezasDentales
            .Select(piece => piece.NumeroFDI)
            .ToHashSetAsync(cancellationToken);

        var pieces = GetFdiNumbers()
            .Where(number => !existingNumbers.Contains(number))
            .Select(number => new PiezaDental
            {
                NumeroFDI = number,
                Nombre = $"Pieza dental {number}"
            })
            .ToList();

        if (pieces.Count == 0) return;
        await dbContext.PiezasDentales.AddRangeAsync(pieces, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Si el catálogo dental se sembró después de que alguien ya lo hubiera
        // consultado, se descarta la copia anterior para incluir las piezas nuevas.
        catalogoCache?.InvalidarPiezas();
    }

    private static IEnumerable<string> GetFdiNumbers()
    {
        foreach (var quadrant in new[] { 1, 2, 3, 4 })
        {
            for (var tooth = 1; tooth <= 8; tooth++)
            {
                yield return $"{quadrant}{tooth}";
            }
        }
    }
}