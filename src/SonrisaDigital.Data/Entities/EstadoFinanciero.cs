namespace SonrisaDigital.Data.Entities;

/// <summary>
/// Estado financiero de un tratamiento. No se guarda como columna en la base de datos:
/// se calcula a partir del precio del tratamiento y de la suma de sus pagos.
/// </summary>
public enum EstadoFinanciero
{
    Pendiente,
    PagoParcial,
    Pagado
}
