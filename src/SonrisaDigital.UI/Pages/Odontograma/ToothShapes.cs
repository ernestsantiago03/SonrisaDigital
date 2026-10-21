namespace SonrisaDigital.UI.Pages.Odontograma;

/// <summary>
/// Tipo morfológico de una pieza dental según su posición FDI.
/// </summary>
internal enum ToothKind
{
    Incisor,
    Canine,
    Premolar,
    Molar
}

/// <summary>
/// Geometría SVG de una pieza dental. Las coordenadas están pensadas para el
/// arco superior (raíz hacia arriba, corona hacia abajo); el arco inferior
/// reutiliza la misma figura invertida verticalmente.
/// </summary>
internal sealed record ToothShape(
    IReadOnlyList<string> Roots,
    string Crown,
    string? Detail);

/// <summary>
/// Catálogo de siluetas dentales dibujadas sobre un lienzo de 48 x 72.
/// </summary>
internal static class ToothShapes
{
    private static readonly ToothShape Incisor = new(
        Roots: new[]
        {
            "M 14.5 36 C 14.5 26 16.5 15 19.5 8 C 20.6 5.4 27.4 5.4 28.5 8 C 31.5 15 33.5 26 33.5 36 Z"
        },
        Crown: "M 13 34 C 12 42 11 50 11.5 57 C 11.7 61.5 13.5 65 17 65.5 L 31 65.5 C 34.5 65 36.3 61.5 36.5 57 C 37 50 36 42 35 34 Z",
        Detail: null);

    private static readonly ToothShape Canine = new(
        Roots: new[]
        {
            "M 15.5 36 C 15.5 24 17 12 20 5.5 C 21.1 2.8 26.9 2.8 28 5.5 C 31 12 32.5 24 32.5 36 Z"
        },
        Crown: "M 14 34 C 13 42 12 50 12.5 56 C 13 60 18.5 65.5 24 67 C 29.5 65.5 35 60 35.5 56 C 36 50 35 42 34 34 Z",
        Detail: null);

    private static readonly ToothShape Premolar = new(
        Roots: new[]
        {
            "M 13.5 36 C 13.5 25 15.5 13 18.5 6.5 C 19.6 3.9 28.4 3.9 29.5 6.5 C 32.5 13 34.5 25 34.5 36 Z"
        },
        Crown: "M 11 34 C 10 42 9.5 50 10 56.5 C 10.3 60.5 12.5 63 15.5 63 C 19 63 21 60 24 60 C 27 60 29 63 32.5 63 C 35.5 63 37.7 60.5 38 56.5 C 38.5 50 38 42 37 34 Z",
        Detail: "M 24 39 C 23 45 23 51 24 56");

    private static readonly ToothShape Molar = new(
        Roots: new[]
        {
            "M 9 36 C 9 25 10.5 14 12.5 8 C 13.4 5.4 18.6 5 19.5 7.5 C 20.2 13 19.5 25 18.5 36 Z",
            "M 29.5 36 C 28.5 25 27.8 13 28.5 7.5 C 29.4 5 34.6 5.4 35.5 8 C 37.5 14 39 25 39 36 Z"
        },
        Crown: "M 7 34 C 6 42 5.5 50 6 56.5 C 6.3 60.5 8.5 63 11.5 63 C 15 63 17 60.5 19.5 60.5 C 21.5 60.5 23 62 24 62 C 25 62 26.5 60.5 28.5 60.5 C 31 60.5 33 63 36.5 63 C 39.5 63 41.7 60.5 42 56.5 C 42.5 50 42 42 41 34 Z",
        Detail: "M 24 38 L 24 56 M 15.5 48.5 C 20 51 28 51 32.5 48.5");

    public static ToothShape For(string numeroFdi) => KindOf(numeroFdi) switch
    {
        ToothKind.Canine => Canine,
        ToothKind.Premolar => Premolar,
        ToothKind.Molar => Molar,
        _ => Incisor
    };

    public static ToothKind KindOf(string numeroFdi)
    {
        if (string.IsNullOrWhiteSpace(numeroFdi)) return ToothKind.Incisor;

        return numeroFdi[^1] switch
        {
            '1' or '2' => ToothKind.Incisor,
            '3' => ToothKind.Canine,
            '4' or '5' => ToothKind.Premolar,
            _ => ToothKind.Molar
        };
    }
}
