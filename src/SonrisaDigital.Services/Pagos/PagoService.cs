using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Pagos.DTOs;

namespace SonrisaDigital.Services.Pagos;

/// <summary>
/// Lógica (el "cerebro") del módulo de Pagos.
///
/// ¿Qué hace este archivo?
/// Calcula todo lo relacionado con el dinero: cuánto se ha facturado, cuánto se ha cobrado,
/// cuánto falta por cobrar, el detalle de cada tratamiento con sus cuotas y el registro de
/// un pago nuevo. Es la capa del medio de la aplicación:
///
///     Pantalla (UI)  →  Service (este archivo)  →  Repository  →  Base de datos
///
/// Cómo se calculan los montos (tres números que se repiten en todo el módulo):
///   pagado  = suma de todos los pagos del tratamiento
///   saldo   = precio del tratamiento − pagado
///   estado  = Pagado (saldo ≤ 0) / Pago parcial (pagó algo) / Pendiente (no ha pagado nada)
///
/// Orden de lectura del archivo:
///  1) Totales para las tarjetas          → GetResumenAsync
///  2) Consultas de lectura               → GetDetalleAsync, GetByPacienteAsync,
///                                          SearchTratamientosPagedAsync, GetHistorialAsync
///  3) Registrar un pago nuevo            → RegistrarAsync
///  4) Ayudantes privados                 → ToDetalle, TotalPagado, ToListItem,
///                                          ObtenerNombreRegistrador
/// </summary>
public sealed class PagoService(
    IPagoRepository repository,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService,
    IAuditoriaService auditoriaService,
    ApplicationDbContext dbContext,
    ILogger<PagoService> logger) : IPagoService
{
    // Cultura de República Dominicana: se usa para escribir el dinero como "RD$ 1,500.00".
    private static readonly CultureInfo Cultura = new("es-DO");

    // Límite de filas por página que acepta el servicio, aunque la pantalla pida más.
    private const int MaxPageSize = 50;

    // =========================================================================================
    // 1) TOTALES PARA LAS TARJETAS  →  Dashboard ("Pagos pendientes") y página de Pagos
    // =========================================================================================

    /// <summary>
    /// Calcula los totales del módulo de pagos: total facturado, total cobrado, saldo pendiente
    /// y cuántos tratamientos siguen con saldo. Alimenta las tarjetas del Dashboard y de Pagos.
    /// </summary>
    public async Task<PagosResumenDto> GetResumenAsync(CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pagos.Ver", "No tienes permiso para consultar pagos.");

        // El repositorio hace las sumas y el conteo en SQL. Antes se traían todos los
        // tratamientos con sus pagos para calcular lo mismo en memoria.
        var totales = await repository.GetTotalesAsync(cancellationToken);
        var saldoPendiente = totales.TotalFacturado - totales.TotalCobrado;

        return new PagosResumenDto(
            totales.TotalFacturado,
            totales.TotalCobrado,
            saldoPendiente,
            totales.TratamientosConSaldo);
    }

    // =========================================================================================
    // 2) CONSULTAS DE LECTURA  →  solo miran la base de datos, no cambian nada
    // =========================================================================================

    /// <summary>
    /// Devuelve el detalle de un tratamiento realizado (montos y cuotas) para la página de
    /// detalle de pagos. Devuelve null si el tratamiento no existe.
    /// </summary>
    public async Task<PagoDetalleDto?> GetDetalleAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pagos.Ver", "No tienes permiso para consultar pagos.");

        // Paso 1: buscar el tratamiento con sus pagos, su paciente, su odontólogo y su pieza.
        var tratamiento = await repository.GetDetalleAsync(tratamientoRealizadoId, cancellationToken);

        // Paso 2: si no existe se devuelve null (la pantalla mostrará "no encontrado").
        // Si existe, se convierte con ToDetalle (calcula pagado, saldo y estado).
        return tratamiento is null ? null : ToDetalle(tratamiento);
    }

    /// <summary>
    /// Tratamientos con pagos de un paciente concreto. Es lo que usa la pestaña "Pagos"
    /// de la ficha del paciente.
    /// </summary>
    public async Task<IReadOnlyList<PagoDetalleDto>> GetByPacienteAsync(
        int pacienteId,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pagos.Ver", "No tienes permiso para consultar pagos.");

        // Paso 1: pedirle al repositorio solo los tratamientos de ese paciente.
        var tratamientos = await repository.GetByPacienteAsync(pacienteId, cancellationToken);

        // Paso 2: convertir cada uno al detalle que pinta la pantalla (mismo cálculo de montos).
        return tratamientos.Select(ToDetalle).ToList();
    }

    /// <summary>
    /// Convierte un tratamiento de la base de datos en el objeto de detalle que usa la pantalla:
    /// calcula lo pagado, el saldo, el estado de pago (Pagado / Pago parcial / Pendiente) y
    /// ordena las cuotas de la más reciente a la más antigua.
    /// </summary>
    private static PagoDetalleDto ToDetalle(TratamientoRealizado tratamiento)
    {
        // Paso 1: las dos cuentas base del módulo.
        var pagado = TotalPagado(tratamiento);
        var saldo = tratamiento.Precio - pagado;

        // Paso 2: decidir el estado de pago leyendo el saldo y lo pagado.
        // Se lee así: si el saldo ya es 0 → Pagado; si no, si pagó algo → Pago parcial; si no → Pendiente.
        var estado = saldo <= 0
            ? EstadoFinanciero.Pagado
            : pagado > 0
                ? EstadoFinanciero.PagoParcial
                : EstadoFinanciero.Pendiente;

        // Paso 3: armar la lista de cuotas (los pagos uno por uno).
        // Se ordenan de la más reciente a la más antigua; si dos pagos son del mismo día,
        // el de Id mayor (el último registrado) va primero.
        var pagos = tratamiento.Pagos
            .OrderByDescending(payment => payment.Fecha)
            .ThenByDescending(payment => payment.Id)
            .Select(payment => new PagoDetalleItemDto(
                payment.Id,
                payment.Fecha,
                payment.Monto,
                payment.MetodoPago,
                ObtenerNombreRegistrador(payment.Usuario),   // quién lo registró (o "Sistema")
                payment.Observaciones))
            .ToList();

        // Paso 4: devolver el paquete completo: datos del paciente, del tratamiento y las cuotas.
        return new PagoDetalleDto(
            tratamiento.Id,
            tratamiento.PacienteId,
            $"{tratamiento.Paciente.Nombres} {tratamiento.Paciente.Apellidos}",
            tratamiento.Paciente.Identificacion,
            tratamiento.Paciente.FechaNacimiento,
            tratamiento.Paciente.Telefono,
            tratamiento.Paciente.Correo,
            tratamiento.Tratamiento.Nombre,
            $"{tratamiento.Odontologo.Nombres} {tratamiento.Odontologo.Apellidos}",
            // La pieza dental es opcional: si el tratamiento no está ligado a una pieza,
            // se manda null y la pantalla no muestra esa línea.
            tratamiento.PiezaDental is null
                ? null
                : $"{tratamiento.PiezaDental.NumeroFDI} — {tratamiento.PiezaDental.Nombre}",
            tratamiento.Fecha,
            tratamiento.Estado,
            tratamiento.Observaciones,
            tratamiento.Precio,
            pagado,
            saldo,
            estado,
            pagos);
    }

    /// <summary>
    /// Página de tratamientos que pueden generar pagos. El repositorio filtra en SQL por
    /// texto, por estado financiero y por paciente; aquí solo se convierten las filas al
    /// DTO que pinta la tabla.
    /// </summary>
    public async Task<PagedResult<TratamientoPagoListItemDto>> SearchTratamientosPagedAsync(
        string? searchTerm,
        EstadoFinanciero? estado,
        int? pacienteId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pagos.Ver", "No tienes permiso para consultar pagos.");

        var (pagina, tamano) = NormalizarPagina(page, pageSize);
        var result = await repository.SearchTratamientosPagedAsync(
            searchTerm,
            estado,
            pacienteId,
            pagina,
            tamano,
            cancellationToken);

        return new PagedResult<TratamientoPagoListItemDto>(
            result.Items.Select(ToListItem).ToList(),
            result.Total,
            result.Page,
            result.PageSize);
    }

    /// <summary>
    /// Últimos pagos registrados en el sistema (por defecto 10), del más reciente al más antiguo.
    /// Alimenta la tabla "Historial de pagos".
    /// </summary>
    public async Task<IReadOnlyList<PagoHistorialDto>> GetHistorialAsync(
        int cantidad = 10,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pagos.Ver", "No tienes permiso para consultar pagos.");

        // Paso 1: pedir los últimos pagos (el repositorio ya los trae ordenados por fecha
        // descendente y corta la lista en "cantidad" filas).
        var pagos = await repository.GetHistorialAsync(cantidad, cancellationToken);

        // Paso 2: convertir cada pago a la fila que ve el usuario en la tabla.
        return pagos
            .Select(payment => new PagoHistorialDto(
                payment.Id,
                payment.TratamientoRealizadoId,
                payment.Fecha,
                $"{payment.TratamientoRealizado.Paciente.Nombres} {payment.TratamientoRealizado.Paciente.Apellidos}",
                payment.TratamientoRealizado.Tratamiento.Nombre,
                payment.Monto,
                payment.MetodoPago,
                ObtenerNombreRegistrador(payment.Usuario)))
            .ToList();
    }

    // =========================================================================================
    // 3) REGISTRAR UN PAGO NUEVO  →  botón "+ Registrar pago"
    // =========================================================================================

    /// <summary>
    /// Registra un pago nuevo sobre un tratamiento. Valida que el monto sea mayor que cero y que
    /// no pase del saldo pendiente, guarda el pago, lo deja anotado en la auditoría y devuelve
    /// el id del pago creado.
    /// </summary>
    public async Task<int> RegistrarAsync(RegistrarPagoDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Pagos.Crear", "No tienes permiso para registrar pagos.");
        ArgumentNullException.ThrowIfNull(model);

        if (model.TratamientoRealizadoId <= 0)
            throw new InvalidOperationException("El tratamiento seleccionado no es válido.");

        if (model.Monto is null || model.Monto <= 0)
            throw new InvalidOperationException("El monto del pago debe ser mayor que cero.");

        if (model.Fecha == default)
            throw new InvalidOperationException("La fecha del pago no es válida.");

        if (!Enum.IsDefined(model.MetodoPago))
            throw new InvalidOperationException("El método de pago no es válido.");

        var monto = model.Monto.Value;

        // El usuario se toma antes de abrir la transacción: si la sesión no está completa,
        // no tiene sentido bloquear el tratamiento.
        var usuarioId = currentUserService.UsuarioId;
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            throw new InvalidOperationException(
                "No se pudo identificar al usuario de la sesión para registrar el pago.");
        }

        var pagoId = 0;

        try
        {
            await dbContext.ExecuteInTransactionAsync(async () =>
            {
                // El repositorio bloquea la fila del tratamiento hasta que termine la transacción.
                // Así dos cobros simultáneos no leen el mismo saldo y permiten cobrar de más.
                var tratamiento = await repository.GetTratamientoParaPagoAsync(
                    model.TratamientoRealizadoId,
                    cancellationToken)
                    ?? throw new InvalidOperationException("El tratamiento seleccionado no existe.");

                var saldoPendiente = tratamiento.Precio - TotalPagado(tratamiento);

                if (tratamiento.Estado == EstadoTratamiento.Cancelado)
                {
                    throw new InvalidOperationException(
                        "Este tratamiento está cancelado; no se pueden registrar pagos.");
                }

                if (tratamiento.Estado == EstadoTratamiento.Completado)
                {
                    throw new InvalidOperationException(
                        "Este tratamiento ya está completado; no se pueden registrar más pagos.");
                }

                if (saldoPendiente <= 0)
                {
                    throw new InvalidOperationException("Este tratamiento ya está saldado; no se pueden registrar más pagos.");
                }

                if (monto > saldoPendiente)
                {
                    throw new InvalidOperationException(
                        $"El monto supera el saldo pendiente (RD$ {saldoPendiente.ToString("N2", Cultura)}).");
                }

                var completaElTratamiento = monto >= saldoPendiente;

                var pago = new Pago
                {
                    TratamientoRealizadoId = tratamiento.Id,
                    // La fecha de un pago es una fecha de calendario elegida por el usuario: se guarda sin hora.
                    Fecha = model.Fecha.Date,
                    Monto = monto,
                    MetodoPago = model.MetodoPago,
                    // Las observaciones vacías se guardan como null (no como texto vacío).
                    Observaciones = string.IsNullOrWhiteSpace(model.Observaciones) ? null : model.Observaciones.Trim(),
                    UsuarioId = usuarioId
                };

                if (completaElTratamiento)
                {
                    tratamiento.Estado = EstadoTratamiento.Completado;
                }

                await repository.AddAsync(pago, cancellationToken);
                await repository.SaveChangesAsync(cancellationToken);

                // La auditoría se guarda dentro de la misma transacción: si falla, el pago
                // y el cambio de estado del tratamiento también se revierten.
                var detalleEstado = completaElTratamiento
                    ? " El tratamiento quedó completado."
                    : string.Empty;

                await auditoriaService.RegistrarAsync(
                    "Registró", "Pagos", nameof(Pago), pago.Id,
                    $"Registró un pago de RD$ {pago.Monto.ToString("N2", Cultura)} para " +
                    $"{tratamiento.Paciente.Nombres} {tratamiento.Paciente.Apellidos} ({tratamiento.Tratamiento.Nombre})." +
                    detalleEstado,
                    cancellationToken);

                pagoId = pago.Id;
            }, cancellationToken);

            return pagoId;
        }
        catch (InvalidOperationException)
        {
            // Las validaciones del servicio ya traen un mensaje para el usuario.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al registrar el pago del tratamiento {TratamientoId}.", model.TratamientoRealizadoId);
            throw new InvalidOperationException("No se pudo registrar el pago. Intenta de nuevo o contacta soporte.");
        }
    }

    // =========================================================================================
    // 4) AYUDANTES PRIVADOS  →  piezas pequeñas reutilizadas por los métodos de arriba
    // =========================================================================================

    /// <summary>
    /// Suma todos los pagos hechos sobre un tratamiento.
    /// </summary>
    private static decimal TotalPagado(TratamientoRealizado tratamiento) =>
        tratamiento.Pagos.Sum(payment => payment.Monto);

    /// <summary>
    /// Convierte un tratamiento en una fila de la tabla de Pagos: calcula lo pagado, el saldo,
    /// el estado de pago y cuál fue el último pago registrado.
    /// </summary>
    private static TratamientoPagoListItemDto ToListItem(TratamientoRealizado tratamiento)
    {
        // Paso 1: las mismas cuentas base (pagado y saldo).
        var pagado = TotalPagado(tratamiento);
        var saldo = tratamiento.Precio - pagado;

        // Paso 2: buscar el último pago del tratamiento (el más reciente por fecha).
        // Si el tratamiento no tiene pagos, queda null.
        var ultimoPago = tratamiento.Pagos
            .OrderByDescending(payment => payment.Fecha)
            .ThenByDescending(payment => payment.Id)
            .FirstOrDefault();

        // Paso 3: el mismo cálculo de estado que en ToDetalle.
        var estado = saldo <= 0
            ? EstadoFinanciero.Pagado
            : pagado > 0
                ? EstadoFinanciero.PagoParcial
                : EstadoFinanciero.Pendiente;

        // Paso 4: armar la fila. El "?." del final deja la fecha y el método del último pago
        // en null cuando el tratamiento todavía no tiene ninguno.
        return new TratamientoPagoListItemDto(
            tratamiento.Id,
            tratamiento.PacienteId,
            $"{tratamiento.Paciente.Nombres} {tratamiento.Paciente.Apellidos}",
            tratamiento.Tratamiento.Nombre,
            tratamiento.Precio,
            pagado,
            saldo,
            tratamiento.Estado,
            ultimoPago?.Fecha,
            ultimoPago?.MetodoPago,
            estado);
    }

    /// <summary>
    /// Nombre del usuario que registró un pago. Si el pago no tiene usuario asociado devuelve
    /// "Sistema", para que la tabla nunca muestre ese espacio vacío.
    /// </summary>
    private static string ObtenerNombreRegistrador(Usuario? usuario) =>
        usuario is null || string.IsNullOrWhiteSpace(usuario.Nombre) ? "Sistema" : usuario.Nombre;

    // Página mínima 1 y tamaño dentro del límite permitido por el servicio.
    private static (int Page, int PageSize) NormalizarPagina(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
}
