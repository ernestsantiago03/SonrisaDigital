namespace SonrisaDigital.Data.Repositories;

/// <summary>
/// Resultado de revisar si un horario ya está ocupado. Se separan las dos
/// posibilidades para que el servicio pueda explicar cuál de las dos agendas choca.
/// </summary>
public sealed record CitaConflicto(bool OdontologoOcupado, bool PacienteOcupado);
