using System.ComponentModel.DataAnnotations;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Pagos;

namespace SonrisaDigital.Services.Tratamientos.DTOs;

// ============================================================================
// CATÁLOGO
// ============================================================================

/// <summary>Fila de la tabla "Catálogo".</summary>
public sealed record TratamientoListItemDto(
    int Id,
    string Nombre,
    string? Descripcion,
    decimal PrecioBase,
    EstadoRegistro Estado);

/// <summary>Datos del formulario "Nuevo / Editar tratamiento".</summary>
public sealed class TratamientoFormDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre del tratamiento es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
    public string? Descripcion { get; set; }

    [Range(0.01, 9999999.99, ErrorMessage = "El precio base debe ser mayor que cero.")]
    public decimal PrecioBase { get; set; }

    public EstadoRegistro Estado { get; set; } = EstadoRegistro.Activo;
}

/// <summary>Opción para el desplegable de tratamientos del formulario de realizados.</summary>
public sealed record TratamientoOptionDto(
    int Id,
    string Nombre,
    decimal PrecioBase,
    bool Activo);

// ============================================================================
// TRATAMIENTOS REALIZADOS
// ============================================================================

/// <summary>Fila de la tabla "Realizados".</summary>
public sealed record TratamientoRealizadoListItemDto(
    int Id,
    int PacienteId,
    string PacienteNombre,
    string TratamientoNombre,
    string OdontologoNombre,
    DateTime Fecha,
    string? Pieza,
    decimal Precio,
    decimal Pagado,
    decimal Saldo,
    EstadoFinanciero EstadoPago,
    EstadoTratamiento Estado);

/// <summary>Detalle que se abre al pulsar el ojo de una fila de "Realizados".</summary>
public sealed record TratamientoRealizadoDetalleDto(
    int Id,
    int PacienteId,
    string PacienteNombre,
    string TratamientoNombre,
    string OdontologoNombre,
    DateTime Fecha,
    string? Pieza,
    decimal Precio,
    EstadoTratamiento Estado,
    string? Observaciones,
    decimal Pagado,
    decimal Saldo,
    EstadoFinanciero EstadoPago,
    int CantidadPagos);

/// <summary>Datos del formulario "Registrar / Editar tratamiento realizado".</summary>
public sealed class TratamientoRealizadoFormDto
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un paciente.")]
    public int PacienteId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un tratamiento.")]
    public int TratamientoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un odontólogo.")]
    public int OdontologoId { get; set; }

    public int? PiezaDentalId { get; set; }

    public DateTime Fecha { get; set; } = DateTime.Today;

    [Range(0.01, 9999999.99, ErrorMessage = "El precio debe ser mayor que cero.")]
    public decimal Precio { get; set; }

    public EstadoTratamiento Estado { get; set; } = EstadoTratamiento.EnProceso;

    [StringLength(1000, ErrorMessage = "Las observaciones no pueden superar los 1000 caracteres.")]
    public string? Observaciones { get; set; }
}

// ============================================================================
// OPCIONES DE LOS DESPLEGABLES
// ============================================================================

public sealed record PacienteOptionDto(int Id, string Nombre, bool Activo);

public sealed record OdontologoOptionDto(int Id, string Nombre, bool Activo);

public sealed record PiezaDentalOptionDto(int Id, string NumeroFDI, string Nombre);
