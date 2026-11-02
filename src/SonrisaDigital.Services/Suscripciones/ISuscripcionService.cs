using SonrisaDigital.Services.Suscripciones.DTOs;

namespace SonrisaDigital.Services.Suscripciones;

/// <summary>
/// Suscripción de las clínicas: prueba, vencimiento y pagos. Solo el Dueño
/// (permisos Clinicas.*) usa las operaciones de lectura y escritura.
/// </summary>
public interface ISuscripcionService
{
    /// <summary>Resumen de la suscripción con su historial de pagos.</summary>
    Task<ResumenSuscripcionDto?> ObtenerAsync(int clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Registra una mensualidad y extiende el vencimiento.</summary>
    Task RegistrarPagoAsync(RegistrarPagoSuscripcionDto model, CancellationToken cancellationToken = default);

    /// <summary>Extiende días de forma manual para un caso especial, con motivo auditado.</summary>
    Task ExtenderAsync(ExtenderSuscripcionDto model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia la prueba de 30 días en el primer ingreso de la clínica. Se llama
    /// desde el login, antes de emitir la cookie, y no exige sesión previa.
    /// Devuelve <c>true</c> cuando la prueba se inició en esta llamada.
    /// </summary>
    Task<bool> IniciarPruebaSiCorrespondeAsync(int clinicaId, CancellationToken cancellationToken = default);
}
