namespace SonrisaDigital.Services.Pagos;

/// <summary>
/// Estado financiero de un tratamiento. No se persiste: se calcula a partir del
/// precio del tratamiento y del total de pagos registrados.
/// </summary>
public enum EstadoFinanciero
{
    Pendiente,
    PagoParcial,
    Pagado
}
