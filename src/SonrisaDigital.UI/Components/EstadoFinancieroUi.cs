using SonrisaDigital.Services.Pagos;

namespace SonrisaDigital.UI.Components;

/// <summary>
/// Texto y color compartidos para la etiqueta del estado de pago. Se usa igual en
/// Tratamientos y en Pagos para que la marca visual sea idéntica en ambos módulos.
/// </summary>
public static class EstadoFinancieroUi
{
    public static string Texto(EstadoFinanciero estado) => estado switch
    {
        EstadoFinanciero.Pagado => "Pagado",
        EstadoFinanciero.PagoParcial => "Pago parcial",
        _ => "Pendiente"
    };

    public static string Clase(EstadoFinanciero estado) => estado switch
    {
        EstadoFinanciero.Pagado => "estado-pagado",
        EstadoFinanciero.PagoParcial => "estado-parcial",
        _ => "estado-pendiente"
    };
}
