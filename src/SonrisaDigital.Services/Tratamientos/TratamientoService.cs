using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Pagos;
using SonrisaDigital.Services.Tratamientos.DTOs;

namespace SonrisaDigital.Services.Tratamientos;

/// <summary>
/// Lógica del módulo de Tratamientos: catálogo de servicios y tratamientos realizados.
///
/// Reglas principales:
///  - La UI no toca la base de datos: todo pasa por este servicio.
///  - Cada escritura se guarda junto con su registro de auditoría en una transacción.
///  - Un tratamiento del catálogo inactivo no se puede usar en registros nuevos.
///  - Todo realizado nace en estado EnProceso.
///  - El estado solo cambia por el flujo: se completa al pagar el precio total o se
///    cancela desde la acción dedicada (siempre que no tenga pagos registrados).
///  - No se puede bajar el precio de un realizado por debajo de lo ya pagado.
///  - Los estados terminales (Completado / Cancelado) solo se pueden consultar.
/// </summary>
public sealed class TratamientoService(
    ITratamientoRepository repository,
    IAuditoriaService auditoriaService,
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService,
    ILogger<TratamientoService> logger) : ITratamientoService
{
    private const int MaxPageSize = 50;
    private const decimal PrecioMaximo = 9_999_999.99m;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-DO");

    // =====================================================================
    // Catálogo
    // =====================================================================

    public async Task<PagedResult<TratamientoListItemDto>> SearchCatalogoAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Ver", "No tienes permiso para consultar tratamientos.");

        var (pagina, tamano) = NormalizarPagina(page, pageSize);
        var result = await repository.SearchCatalogoAsync(searchTerm, estado, pagina, tamano, cancellationToken);

        return new PagedResult<TratamientoListItemDto>(
            result.Items.Select(ToListItem).ToList(),
            result.Total,
            result.Page,
            result.PageSize);
    }

    public async Task<TratamientoFormDto?> GetCatalogoFormAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Ver", "No tienes permiso para consultar tratamientos.");
        if (id <= 0)
        {
            return null;
        }

        var tratamiento = await repository.GetByIdAsync(id, cancellationToken);
        if (tratamiento is null)
        {
            return null;
        }

        return new TratamientoFormDto
        {
            Id = tratamiento.Id,
            Nombre = tratamiento.Nombre,
            Descripcion = tratamiento.Descripcion,
            PrecioBase = tratamiento.PrecioBase,
            Estado = tratamiento.Estado
        };
    }

    public async Task<int> CreateCatalogoAsync(TratamientoFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Crear", "No tienes permiso para registrar tratamientos.");
        ArgumentNullException.ThrowIfNull(model);
        ValidateCatalogo(model);

        var nombre = model.Nombre.Trim();
        if (await repository.ExistsNombreAsync(nombre, null, cancellationToken))
        {
            throw new InvalidOperationException("Ya existe un tratamiento con ese nombre.");
        }

        var tratamiento = new Tratamiento
        {
            Nombre = nombre,
            Descripcion = Normalize(model.Descripcion),
            PrecioBase = model.PrecioBase,
            Estado = model.Estado
        };

        try
        {
            await EjecutarEnTransaccionAsync(async () =>
            {
                await repository.AddAsync(tratamiento, cancellationToken);
                await repository.SaveChangesAsync(cancellationToken);

                await auditoriaService.RegistrarAsync(
                    "Creó",
                    "Tratamientos",
                    nameof(Tratamiento),
                    tratamiento.Id,
                    $"Creó el tratamiento '{tratamiento.Nombre}' con precio base {Money(tratamiento.PrecioBase)}.",
                    cancellationToken);
            }, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Error al crear el tratamiento {Nombre}.", nombre);
            throw new InvalidOperationException(
                "No se pudo guardar el tratamiento. Verifique que el nombre no esté repetido.", ex);
        }

        return tratamiento.Id;
    }

    public async Task UpdateCatalogoAsync(TratamientoFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Editar", "No tienes permiso para editar tratamientos.");
        ArgumentNullException.ThrowIfNull(model);
        if (model.Id <= 0)
        {
            throw new InvalidOperationException("El tratamiento no existe.");
        }

        ValidateCatalogo(model);

        var tratamiento = await repository.GetForUpdateAsync(model.Id, cancellationToken)
            ?? throw new InvalidOperationException("El tratamiento no existe.");

        var nombre = model.Nombre.Trim();
        if (await repository.ExistsNombreAsync(nombre, model.Id, cancellationToken))
        {
            throw new InvalidOperationException("Ya existe un tratamiento con ese nombre.");
        }

        var estadoAnterior = tratamiento.Estado;

        tratamiento.Nombre = nombre;
        tratamiento.Descripcion = Normalize(model.Descripcion);
        tratamiento.PrecioBase = model.PrecioBase;
        tratamiento.Estado = model.Estado;

        var verbo = estadoAnterior == tratamiento.Estado
            ? "Modificó"
            : tratamiento.Estado == EstadoRegistro.Activo ? "Activó" : "Desactivó";

        var descripcion = verbo == "Modificó"
            ? $"Modificó el tratamiento '{tratamiento.Nombre}' (precio base {Money(tratamiento.PrecioBase)})."
            : $"{verbo} el tratamiento '{tratamiento.Nombre}'.";

        try
        {
            await EjecutarEnTransaccionAsync(async () =>
            {
                await repository.SaveChangesAsync(cancellationToken);

                await auditoriaService.RegistrarAsync(
                    verbo,
                    "Tratamientos",
                    nameof(Tratamiento),
                    tratamiento.Id,
                    descripcion,
                    cancellationToken);
            }, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Error al actualizar el tratamiento {Id}.", model.Id);
            throw new InvalidOperationException(
                "No se pudo guardar el tratamiento. Verifique que el nombre no esté repetido.", ex);
        }
    }

    // =====================================================================
    // Tratamientos realizados
    // =====================================================================

    public async Task<PagedResult<TratamientoRealizadoListItemDto>> SearchRealizadosAsync(
        string? searchTerm,
        EstadoTratamiento? estado,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Ver", "No tienes permiso para consultar tratamientos.");

        var (pagina, tamano) = NormalizarPagina(page, pageSize);
        var result = await repository.SearchRealizadosAsync(searchTerm, estado, pagina, tamano, cancellationToken);

        return new PagedResult<TratamientoRealizadoListItemDto>(
            result.Items.Select(ToListItem).ToList(),
            result.Total,
            result.Page,
            result.PageSize);
    }

    public async Task<TratamientoRealizadoDetalleDto?> GetRealizadoAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Ver", "No tienes permiso para consultar tratamientos.");

        var realizado = await repository.GetRealizadoAsync(id, cancellationToken);
        if (realizado is null)
        {
            return null;
        }

        var pagado = realizado.Pagos.Sum(pago => pago.Monto);
        var saldo = realizado.Precio - pagado;

        return new TratamientoRealizadoDetalleDto(
            realizado.Id,
            realizado.PacienteId,
            NombreCompleto(realizado.Paciente),
            realizado.Tratamiento.Nombre,
            NombreCompleto(realizado.Odontologo),
            realizado.Fecha,
            realizado.PiezaDental is null
                ? null
                : $"{realizado.PiezaDental.NumeroFDI} — {realizado.PiezaDental.Nombre}",
            realizado.Precio,
            realizado.Estado,
            realizado.Observaciones,
            pagado,
            saldo,
            CalcularEstadoPago(pagado, saldo),
            realizado.Pagos.Count);
    }

    public async Task<TratamientoRealizadoFormDto?> GetRealizadoFormAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Ver", "No tienes permiso para consultar tratamientos.");

        var realizado = await repository.GetRealizadoAsync(id, cancellationToken);
        if (realizado is null)
        {
            return null;
        }

        return new TratamientoRealizadoFormDto
        {
            Id = realizado.Id,
            PacienteId = realizado.PacienteId,
            TratamientoId = realizado.TratamientoId,
            OdontologoId = realizado.OdontologoId,
            PiezaDentalId = realizado.PiezaDentalId,
            Fecha = realizado.Fecha,
            Precio = realizado.Precio,
            Estado = realizado.Estado,
            Observaciones = realizado.Observaciones
        };
    }

    public async Task<int> CreateRealizadoAsync(
        TratamientoRealizadoFormDto model,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Crear", "No tienes permiso para registrar tratamientos.");

        ArgumentNullException.ThrowIfNull(model);
        ValidateRealizado(model);

        var paciente = await repository.GetPacienteAsync(model.PacienteId, cancellationToken)
            ?? throw new InvalidOperationException("El paciente seleccionado no existe.");

        if (paciente.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("El paciente seleccionado está inactivo.");
        }

        var odontologo = await repository.GetOdontologoAsync(model.OdontologoId, cancellationToken)
            ?? throw new InvalidOperationException("El odontólogo seleccionado no existe.");

        if (odontologo.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("El odontólogo seleccionado está inactivo.");
        }

        var tratamiento = await repository.GetByIdAsync(model.TratamientoId, cancellationToken)
            ?? throw new InvalidOperationException("El tratamiento seleccionado no existe.");

        if (tratamiento.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("El tratamiento seleccionado está inactivo.");
        }

        if (model.PiezaDentalId.HasValue)
        {
            await EnsurePiezaExisteAsync(model.PiezaDentalId.Value, cancellationToken);
        }

        var realizado = new TratamientoRealizado
        {
            PacienteId = model.PacienteId,
            TratamientoId = model.TratamientoId,
            OdontologoId = model.OdontologoId,
            PiezaDentalId = model.PiezaDentalId,
            Fecha = model.Fecha.Date,
            Precio = model.Precio,
            Estado = EstadoTratamiento.EnProceso,
            Observaciones = Normalize(model.Observaciones)
        };

        try
        {
            await EjecutarEnTransaccionAsync(async () =>
            {
                await repository.AddRealizadoAsync(realizado, cancellationToken);
                await repository.SaveChangesAsync(cancellationToken);

                await auditoriaService.RegistrarAsync(
                    "Registró",
                    "Tratamientos",
                    nameof(TratamientoRealizado),
                    realizado.Id,
                    $"Registró el tratamiento '{tratamiento.Nombre}' para {NombreCompleto(paciente)} por {Money(realizado.Precio)}.",
                    cancellationToken);
            }, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Error al registrar un tratamiento realizado para el paciente {PacienteId}.", model.PacienteId);
            throw new InvalidOperationException(
                "No se pudo guardar el tratamiento realizado. Verifique los datos seleccionados.", ex);
        }

        return realizado.Id;
    }

    public async Task UpdateRealizadoAsync(
        TratamientoRealizadoFormDto model,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Editar", "No tienes permiso para editar tratamientos.");

        ArgumentNullException.ThrowIfNull(model);
        if (model.Id <= 0)
        {
            throw new InvalidOperationException("El tratamiento realizado no existe.");
        }

        ValidateRealizado(model);

        var realizado = await repository.GetRealizadoForUpdateAsync(model.Id, cancellationToken)
            ?? throw new InvalidOperationException("El tratamiento realizado no existe.");

        if (realizado.Estado is EstadoTratamiento.Completado or EstadoTratamiento.Cancelado)
        {
            throw new InvalidOperationException(
                "Un tratamiento completado o cancelado ya no se puede editar. Solo se puede consultar.");
        }

        var paciente = await repository.GetPacienteAsync(model.PacienteId, cancellationToken)
            ?? throw new InvalidOperationException("El paciente seleccionado no existe.");

        var odontologo = await repository.GetOdontologoAsync(model.OdontologoId, cancellationToken)
            ?? throw new InvalidOperationException("El odontólogo seleccionado no existe.");

        var tratamiento = await repository.GetByIdAsync(model.TratamientoId, cancellationToken)
            ?? throw new InvalidOperationException("El tratamiento seleccionado no existe.");

        var cambiaPaciente = realizado.PacienteId != model.PacienteId;
        var cambiaOdontologo = realizado.OdontologoId != model.OdontologoId;
        var cambiaTratamiento = realizado.TratamientoId != model.TratamientoId;

        if (cambiaPaciente && paciente.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("El paciente seleccionado está inactivo.");
        }

        if (cambiaOdontologo && odontologo.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("El odontólogo seleccionado está inactivo.");
        }

        if (cambiaTratamiento && tratamiento.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("El tratamiento seleccionado está inactivo.");
        }

        if (model.PiezaDentalId.HasValue)
        {
            await EnsurePiezaExisteAsync(model.PiezaDentalId.Value, cancellationToken);
        }

        var pagado = realizado.Pagos.Sum(pago => pago.Monto);

        if (model.Precio < pagado)
        {
            throw new InvalidOperationException(
                $"El precio no puede ser menor que lo ya pagado ({Money(pagado)}).");
        }

        var completaPorPrecio = pagado > 0 && model.Precio == pagado;

        if (pagado > 0 && (cambiaPaciente || cambiaTratamiento))
        {
            throw new InvalidOperationException(
                "No se puede cambiar el paciente ni el tratamiento cuando ya existen pagos registrados.");
        }

        realizado.PacienteId = model.PacienteId;
        realizado.TratamientoId = model.TratamientoId;
        realizado.OdontologoId = model.OdontologoId;
        realizado.PiezaDentalId = model.PiezaDentalId;
        realizado.Fecha = model.Fecha.Date;
        realizado.Precio = model.Precio;
        if (completaPorPrecio)
        {
            realizado.Estado = EstadoTratamiento.Completado;
        }

        realizado.Observaciones = Normalize(model.Observaciones);

        var detalleEstado = completaPorPrecio
            ? " El tratamiento quedó completado porque el precio coincide con lo pagado."
            : string.Empty;

        try
        {
            await EjecutarEnTransaccionAsync(async () =>
            {
                await repository.SaveChangesAsync(cancellationToken);

                await auditoriaService.RegistrarAsync(
                    "Modificó",
                    "Tratamientos",
                    nameof(TratamientoRealizado),
                    realizado.Id,
                    $"Modificó el tratamiento '{tratamiento.Nombre}' de {NombreCompleto(paciente)} (precio {Money(realizado.Precio)})." +
                    detalleEstado,
                    cancellationToken);
            }, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Error al actualizar el tratamiento realizado {Id}.", model.Id);
            throw new InvalidOperationException(
                "No se pudo guardar el tratamiento realizado. Verifique los datos seleccionados.", ex);
        }
    }

    public async Task CancelarRealizadoAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Editar", "No tienes permiso para cancelar tratamientos.");

        if (id <= 0)
        {
            throw new InvalidOperationException("El tratamiento realizado no existe.");
        }

        var realizado = await repository.GetRealizadoForUpdateAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("El tratamiento realizado no existe.");

        if (realizado.Estado == EstadoTratamiento.Cancelado)
        {
            throw new InvalidOperationException("El tratamiento ya está cancelado.");
        }

        if (realizado.Estado == EstadoTratamiento.Completado)
        {
            throw new InvalidOperationException("Un tratamiento completado no se puede cancelar.");
        }

        if (realizado.Pagos.Count > 0)
        {
            throw new InvalidOperationException(
                "No se puede cancelar un tratamiento que tiene pagos registrados.");
        }

        realizado.Estado = EstadoTratamiento.Cancelado;

        try
        {
            await EjecutarEnTransaccionAsync(async () =>
            {
                await repository.SaveChangesAsync(cancellationToken);

                await auditoriaService.RegistrarAsync(
                    "Canceló",
                    "Tratamientos",
                    nameof(TratamientoRealizado),
                    realizado.Id,
                    $"Canceló el tratamiento '{realizado.Tratamiento.Nombre}' de {NombreCompleto(realizado.Paciente)}.",
                    cancellationToken);
            }, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Error al cancelar el tratamiento realizado {Id}.", id);
            throw new InvalidOperationException(
                "No se pudo cancelar el tratamiento. Intente nuevamente.", ex);
        }
    }

    // =====================================================================
    // Opciones de los formularios
    // =====================================================================

    public async Task<IReadOnlyList<TratamientoOptionDto>> GetTratamientosAsync(
        bool soloActivos,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Ver", "No tienes permiso para consultar tratamientos.");

        var tratamientos = soloActivos
            ? await repository.GetActivosAsync(cancellationToken)
            : await repository.GetAllAsync(cancellationToken);

        return tratamientos
            .Select(tratamiento => new TratamientoOptionDto(
                tratamiento.Id,
                tratamiento.Nombre,
                tratamiento.PrecioBase,
                tratamiento.Estado == EstadoRegistro.Activo))
            .ToList();
    }

    public async Task<IReadOnlyList<PacienteOptionDto>> GetPacientesAsync(
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Ver", "No tienes permiso para consultar tratamientos.");

        var pacientes = await repository.GetPacientesAsync(cancellationToken);

        return pacientes
            .Select(paciente => new PacienteOptionDto(
                paciente.Id,
                NombreCompleto(paciente),
                paciente.Estado == EstadoRegistro.Activo))
            .ToList();
    }

    public async Task<IReadOnlyList<OdontologoOptionDto>> GetOdontologosAsync(
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Ver", "No tienes permiso para consultar tratamientos.");

        var odontologos = await repository.GetOdontologosAsync(cancellationToken);

        return odontologos
            .Select(odontologo => new OdontologoOptionDto(
                odontologo.Id,
                NombreCompleto(odontologo),
                odontologo.Estado == EstadoRegistro.Activo))
            .ToList();
    }

    public async Task<IReadOnlyList<PiezaDentalOptionDto>> GetPiezasAsync(
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Tratamientos.Ver", "No tienes permiso para consultar tratamientos.");

        var piezas = await repository.GetPiezasAsync(cancellationToken);

        return piezas
            .Select(pieza => new PiezaDentalOptionDto(pieza.Id, pieza.NumeroFDI, pieza.Nombre))
            .ToList();
    }

    // =====================================================================
    // Validaciones y ayudantes
    // =====================================================================

    private static void ValidateCatalogo(TratamientoFormDto model)
    {
        if (string.IsNullOrWhiteSpace(model.Nombre))
        {
            throw new InvalidOperationException("El nombre del tratamiento es obligatorio.");
        }

        if (model.Nombre.Trim().Length > 150)
        {
            throw new InvalidOperationException("El nombre no puede superar los 150 caracteres.");
        }

        if (model.Descripcion is { Length: > 500 })
        {
            throw new InvalidOperationException("La descripción no puede superar los 500 caracteres.");
        }

        if (model.PrecioBase <= 0 || model.PrecioBase > PrecioMaximo)
        {
            throw new InvalidOperationException("El precio base debe ser mayor que cero.");
        }
    }

    private static void ValidateRealizado(TratamientoRealizadoFormDto model)
    {
        if (model.PacienteId <= 0)
        {
            throw new InvalidOperationException("Debe seleccionar un paciente.");
        }

        if (model.TratamientoId <= 0)
        {
            throw new InvalidOperationException("Debe seleccionar un tratamiento.");
        }

        if (model.OdontologoId <= 0)
        {
            throw new InvalidOperationException("Debe seleccionar un odontólogo.");
        }

        if (model.Fecha == default)
        {
            throw new InvalidOperationException("La fecha del tratamiento es obligatoria.");
        }

        if (model.Fecha.Date > DateTime.Today)
        {
            throw new InvalidOperationException("La fecha del tratamiento no puede ser futura.");
        }

        if (model.Precio <= 0 || model.Precio > PrecioMaximo)
        {
            throw new InvalidOperationException("El precio debe ser mayor que cero.");
        }

        if (!Enum.IsDefined(typeof(EstadoTratamiento), model.Estado))
        {
            throw new InvalidOperationException("El estado del tratamiento no es válido.");
        }

        if (model.Observaciones is { Length: > 1000 })
        {
            throw new InvalidOperationException("Las observaciones no pueden superar los 1000 caracteres.");
        }

        if (model.PiezaDentalId is <= 0)
        {
            throw new InvalidOperationException("La pieza dental seleccionada no es válida.");
        }
    }

    private async Task EnsurePiezaExisteAsync(int piezaId, CancellationToken cancellationToken)
    {
        var pieza = await repository.GetPiezaAsync(piezaId, cancellationToken);
        if (pieza is null)
        {
            throw new InvalidOperationException("La pieza dental seleccionada no existe.");
        }
    }

    private async Task EjecutarEnTransaccionAsync(Func<Task> operacion, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await operacion();
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static (int Page, int PageSize) NormalizarPagina(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

    private static TratamientoListItemDto ToListItem(Tratamiento tratamiento) =>
        new(
            tratamiento.Id,
            tratamiento.Nombre,
            tratamiento.Descripcion,
            tratamiento.PrecioBase,
            tratamiento.Estado);

    private static TratamientoRealizadoListItemDto ToListItem(TratamientoRealizado realizado)
    {
        var pagado = realizado.Pagos.Sum(pago => pago.Monto);
        var saldo = realizado.Precio - pagado;

        return new(
            realizado.Id,
            realizado.PacienteId,
            NombreCompleto(realizado.Paciente),
            realizado.Tratamiento.Nombre,
            NombreCompleto(realizado.Odontologo),
            realizado.Fecha,
            realizado.PiezaDental?.NumeroFDI,
            realizado.Precio,
            pagado,
            saldo,
            CalcularEstadoPago(pagado, saldo),
            realizado.Estado);
    }

    private static EstadoFinanciero CalcularEstadoPago(decimal pagado, decimal saldo) =>
        saldo <= 0
            ? EstadoFinanciero.Pagado
            : pagado > 0
                ? EstadoFinanciero.PagoParcial
                : EstadoFinanciero.Pendiente;

    private static string NombreCompleto(Paciente paciente) =>
        $"{paciente.Nombres} {paciente.Apellidos}".Trim();

    private static string NombreCompleto(Odontologo odontologo) =>
        $"{odontologo.Nombres} {odontologo.Apellidos}".Trim();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Money(decimal value) => $"RD$ {value.ToString("N2", Cultura)}";
}
