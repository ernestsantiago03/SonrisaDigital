using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Odontograma.DTOs;

public sealed record OdontogramaPacienteDto(int Id, string NombreCompleto);

/// <summary>
/// Odontólogo activo que puede quedar como responsable de un cambio del odontograma.
/// </summary>
public sealed record OdontogramaOdontologoDto(int Id, string Nombre);

/// <summary>
/// Contexto de edición del odontograma: indica si el usuario puede guardar, quién
/// firma sus cambios y, cuando es un administrador sin perfil, qué odontólogos
/// activos puede elegir.
/// </summary>
public sealed class OdontogramaEditContextDto
{
    /// <summary>El usuario tiene el permiso Odontograma.Editar.</summary>
    public bool TienePermisoEditar { get; init; }

    /// <summary>El usuario tiene permiso y además una firma válida para guardar.</summary>
    public bool PuedeEditar { get; init; }

    /// <summary>Es un administrador sin perfil y debe elegir un odontólogo activo.</summary>
    public bool RequiereSeleccionarOdontologo { get; init; }

    /// <summary>Odontólogo vinculado al usuario, si tiene perfil.</summary>
    public int? OdontologoVinculadoId { get; init; }

    /// <summary>Nombre del odontólogo vinculado, para mostrarlo como firma fija.</summary>
    public string? OdontologoVinculadoNombre { get; init; }

    /// <summary>Explicación cuando el usuario tiene permiso pero no puede editar.</summary>
    public string? MotivoSinEdicion { get; init; }

    /// <summary>Odontólogos activos disponibles para elegir como firmante.</summary>
    public IReadOnlyList<OdontogramaOdontologoDto> OdontologosDisponibles { get; init; } = [];
}

public sealed class OdontogramaPiezaDto
{
    public int Id { get; init; }
    public string NumeroFDI { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public EstadoOdontograma Estado { get; set; } = EstadoOdontograma.Sano;
    public string? Observaciones { get; set; }
    public DateTime? FechaModificacion { get; init; }
    public string? Odontologo { get; init; }
}