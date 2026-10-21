namespace SonrisaDigital.UI.Pages.Odontograma;

/// <summary>
/// Opción de un <see cref="NiceSelect{TValue}"/>: valor y texto a mostrar.
/// </summary>
public sealed record NiceSelectItem<TValue>(TValue Value, string Label);
