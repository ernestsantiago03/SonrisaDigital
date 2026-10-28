using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Citas;

/// <summary>
/// Reglas únicas para cambiar el estado de una cita. La pantalla puede usar estas
/// mismas reglas para mostrar acciones, pero el servicio las vuelve a validar antes
/// de guardar para que no dependan de lo que envíe el cliente.
/// </summary>
public static class CitaStateMachine
{
    /// <summary>
    /// Atendida, Cancelada y No asistió son estados finales: la cita queda cerrada.
    /// </summary>
    public static bool EsEstadoFinal(EstadoCita estado) =>
        estado is EstadoCita.Atendida or EstadoCita.Cancelada or EstadoCita.NoAsistio;

    /// <summary>
    /// Transiciones que permite el flujo actual de la clínica.
    /// Programada se confirma o se cierra; Confirmada solo puede pasar a Atendida.
    /// </summary>
    public static IReadOnlyList<EstadoCita> TransicionesPermitidas(EstadoCita actual) => actual switch
    {
        EstadoCita.Programada =>
            [EstadoCita.Confirmada, EstadoCita.Cancelada, EstadoCita.NoAsistio],
        EstadoCita.Confirmada =>
            [EstadoCita.Atendida],
        _ => []
    };

    /// <summary>
    /// Permiso necesario para una transición concreta. Devuelve null si la
    /// transición no está permitida o si el estado no cambia.
    /// </summary>
    public static string? PermisoRequerido(EstadoCita actual, EstadoCita nuevo) => (actual, nuevo) switch
    {
        (EstadoCita.Programada, EstadoCita.Confirmada) => "Citas.Editar",
        (EstadoCita.Programada, EstadoCita.Cancelada) => "Citas.Cancelar",
        (EstadoCita.Programada, EstadoCita.NoAsistio) => "Citas.Cancelar",
        (EstadoCita.Confirmada, EstadoCita.Atendida) => "Citas.Atender",
        _ => null
    };

    public static bool EsTransicionPermitida(EstadoCita actual, EstadoCita nuevo) =>
        actual == nuevo || PermisoRequerido(actual, nuevo) is not null;

    /// <summary>
    /// Valida la transición y lanza un mensaje apto para el usuario si no está permitida.
    /// </summary>
    public static void ValidarTransicion(EstadoCita actual, EstadoCita nuevo)
    {
        if (EsTransicionPermitida(actual, nuevo))
        {
            return;
        }

        throw new InvalidOperationException(
            EsEstadoFinal(actual)
                ? $"La cita ya está cerrada ({NombreEstado(actual)}) y no puede cambiar a {NombreEstado(nuevo)}."
                : $"No se puede cambiar una cita de {NombreEstado(actual)} a {NombreEstado(nuevo)}.");
    }

    public static string NombreEstado(EstadoCita estado) => estado switch
    {
        EstadoCita.Programada => "Programada",
        EstadoCita.Confirmada => "Confirmada",
        EstadoCita.Atendida => "Atendida",
        EstadoCita.Cancelada => "Cancelada",
        EstadoCita.NoAsistio => "No asistió",
        _ => estado.ToString()
    };
}
