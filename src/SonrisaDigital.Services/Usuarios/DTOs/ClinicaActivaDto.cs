using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Usuarios.DTOs;

/// <summary>
/// Clínica con la que opera la sesión actual. El formulario de usuarios la muestra
/// como dato fijo y el servicio la vuelve a consultar antes de crear una cuenta.
/// </summary>
public sealed record ClinicaActivaDto(int Id, string Nombre, EstadoClinica Estado, int LimiteUsuarios);
