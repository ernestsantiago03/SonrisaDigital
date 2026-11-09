using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Suscripciones;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Suscripciones.DTOs;
using SonrisaDigital.Services.Validacion;

namespace SonrisaDigital.Services.Suscripciones;

public sealed class SuscripcionService(
    ApplicationDbContext dbContext,
    IAuditoriaService auditoriaService,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService,
    IOptions<SuscripcionOptions> options) : ISuscripcionService
{
    private const string Modulo = "Suscripciones";
    private const string PlanMensual = "Mensual";
    private const string PlanPrueba = "Prueba";

    public async Task<ResumenSuscripcionDto?> ObtenerAsync(int clinicaId, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(
            currentUserService, "Clinicas.Ver", "No tienes permiso para consultar la suscripción.");

        // Clinicas y PagosSuscripcion no llevan el filtro por clínica del inquilino:
        // el Dueño debe poder facturar cualquier clínica aunque no la haya seleccionado.
        var clinica = await dbContext.Clinicas
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == clinicaId, cancellationToken);

        if (clinica is null)
        {
            return null;
        }

        var pagos = await dbContext.PagosSuscripcion
            .AsNoTracking()
            .Where(payment => payment.ClinicaId == clinicaId)
            .OrderByDescending(payment => payment.FechaPago)
            .ThenByDescending(payment => payment.Id)
            .Select(payment => new PagoSuscripcionDto(
                payment.Id,
                payment.FechaPago,
                payment.PeriodoDesde,
                payment.PeriodoHasta,
                payment.Monto,
                payment.Moneda,
                payment.Metodo,
                payment.Referencia,
                payment.Notas,
                payment.RegistradoPor.Nombre,
                payment.FechaRegistro))
            .ToListAsync(cancellationToken);

        var hoy = ReglaSuscripcion.Hoy();
        var acceso = ReglaSuscripcion.Evaluar(
            clinica.Estado,
            clinica.EnPrueba,
            clinica.FechaPrimerIngreso,
            clinica.FechaVencimiento,
            hoy);

        return new ResumenSuscripcionDto(
            clinica.Id,
            clinica.Nombre,
            clinica.Codigo,
            clinica.Plan,
            clinica.Estado,
            clinica.MotivoSuspension,
            clinica.EnPrueba,
            clinica.FechaPrimerIngreso,
            clinica.FechaVencimiento,
            clinica.LimiteUsuarios,
            acceso,
            ReglaSuscripcion.DiasRestantes(clinica.FechaVencimiento, hoy),
            options.Value.PrecioMensualUsd,
            pagos);
    }

    public async Task RegistrarPagoAsync(RegistrarPagoSuscripcionDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(
            currentUserService, "Clinicas.Editar", "No tienes permiso para registrar pagos de suscripción.");

        ArgumentNullException.ThrowIfNull(model);
        ValidadorDto.PrepararYValidar(model);

        if (model.Monto <= 0)
        {
            throw new InvalidOperationException("El monto del pago debe ser mayor que cero.");
        }

        var usuarioId = ObtenerUsuarioId();

        // Serializable evita que dos pagos simultáneos lean el mismo vencimiento
        // y uno de los dos meses se pierda.
        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            var clinica = await dbContext.Clinicas
                .SingleOrDefaultAsync(item => item.Id == model.ClinicaId, cancellationToken)
                ?? throw new InvalidOperationException("La clínica no existe.");

            var hoy = ReglaSuscripcion.HoyDateOnly();
            var vencimientoActual = clinica.FechaVencimiento is DateTime vencimiento
                ? DateOnly.FromDateTime(vencimiento)
                : (DateOnly?)null;
            var periodoDesde = ReglaSuscripcion.InicioPeriodo(vencimientoActual, hoy);
            var periodoHasta = periodoDesde.AddMonths(model.Meses);

            var pago = new PagoSuscripcion
            {
                ClinicaId = clinica.Id,
                FechaPago = hoy,
                PeriodoDesde = periodoDesde,
                PeriodoHasta = periodoHasta,
                Monto = model.Monto,
                Moneda = "USD",
                Metodo = model.Metodo,
                Referencia = Normalizar(model.Referencia),
                Notas = Normalizar(model.Notas),
                RegistradoPorId = usuarioId,
                FechaRegistro = DateTime.UtcNow
            };

            // Un pago reactiva una suspensión por impago y termina la prueba.
            // Si la suspensión fue una decisión administrativa, el pago se guarda
            // pero el acceso sigue bloqueado hasta que el Dueño reactive a mano.
            var reactivaPorPago = clinica.Estado == EstadoClinica.Suspendida
                && clinica.MotivoSuspension == MotivoSuspension.FaltaDePago;

            clinica.FechaVencimiento = periodoHasta.ToDateTime(TimeOnly.MinValue);
            clinica.FechaPrimerIngreso ??= hoy.ToDateTime(TimeOnly.MinValue);
            clinica.EnPrueba = false;
            if (clinica.Estado != EstadoClinica.Suspendida || reactivaPorPago)
            {
                clinica.Estado = EstadoClinica.Activa;
                clinica.MotivoSuspension = null;
            }
            if (string.IsNullOrWhiteSpace(clinica.Plan) || clinica.Plan == PlanPrueba)
            {
                clinica.Plan = PlanMensual;
            }

            dbContext.PagosSuscripcion.Add(pago);
            await dbContext.SaveChangesAsync(cancellationToken);

            var detallePago = $"Registró {model.Meses} mes(es) de suscripción de {clinica.Nombre} por {model.Monto:0.00} USD. Nuevo vencimiento: {periodoHasta:dd/MM/yyyy}.";
            if (reactivaPorPago)
            {
                detallePago += " La clínica se reactivó automáticamente porque la suspensión era por falta de pago.";
            }

            await auditoriaService.RegistrarAsync(
                "Registró pago", Modulo, nameof(PagoSuscripcion), pago.Id,
                detallePago, cancellationToken, clinica.Id);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task ExtenderAsync(ExtenderSuscripcionDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(
            currentUserService, "Clinicas.Editar", "No tienes permiso para extender suscripciones.");

        ArgumentNullException.ThrowIfNull(model);
        ValidadorDto.PrepararYValidar(model);

        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            var clinica = await dbContext.Clinicas
                .SingleOrDefaultAsync(item => item.Id == model.ClinicaId, cancellationToken)
                ?? throw new InvalidOperationException("La clínica no existe.");

            var hoy = ReglaSuscripcion.HoyDateOnly();
            var vencimientoActual = clinica.FechaVencimiento is DateTime vencimiento
                ? DateOnly.FromDateTime(vencimiento)
                : (DateOnly?)null;
            var inicio = ReglaSuscripcion.InicioPeriodo(vencimientoActual, hoy);
            clinica.FechaVencimiento = inicio.AddDays(model.Dias).ToDateTime(TimeOnly.MinValue);
            clinica.FechaPrimerIngreso ??= hoy.ToDateTime(TimeOnly.MinValue);

            // La extensión sirve tanto para una clínica comercial como para una
            // prueba: se conserva el modo (EnPrueba/Plan) y solo se devuelve el
            // acceso si la clínica no está suspendida manualmente.
            if (clinica.Estado != EstadoClinica.Suspendida)
            {
                clinica.Estado = EstadoClinica.Activa;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await auditoriaService.RegistrarAsync(
                "Extendió", Modulo, nameof(Clinica), clinica.Id,
                $"Extendió {model.Dias} día(s) la suscripción de {clinica.Nombre}. Motivo: {model.Motivo}. Nuevo vencimiento: {clinica.FechaVencimiento:dd/MM/yyyy}.",
                cancellationToken, clinica.Id);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<bool> IniciarPruebaSiCorrespondeAsync(int clinicaId, CancellationToken cancellationToken = default)
    {
        // Se ejecuta desde el login, cuando todavía no hay una sesión de Blazor.
        // Por eso no valida permisos ni audita: el evento es del sistema y la
        // fecha queda registrada en la propia clínica.
        //
        // Serializable y con relectura: si el Dueño registra un pago al mismo
        // tiempo, el inicio de prueba no puede sobrescribir el vencimiento pagado.
        var iniciada = false;

        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            var clinica = await dbContext.Clinicas
                .SingleOrDefaultAsync(item => item.Id == clinicaId, cancellationToken);

            if (clinica is null
                || !clinica.EnPrueba
                || clinica.FechaPrimerIngreso is not null
                || clinica.FechaVencimiento is not null)
            {
                return;
            }

            var hoy = ReglaSuscripcion.HoyDateOnly();
            clinica.FechaPrimerIngreso = hoy.ToDateTime(TimeOnly.MinValue);
            clinica.FechaVencimiento = ReglaSuscripcion.VencimientoDePrueba(hoy)
                .ToDateTime(TimeOnly.MinValue);
            clinica.Plan ??= PlanPrueba;

            await dbContext.SaveChangesAsync(cancellationToken);
            iniciada = true;
        }, IsolationLevel.Serializable, cancellationToken);

        return iniciada;
    }

    /// <summary>Usuario que registra el pago. Si el circuito aún no lo cargó, sale del principal.</summary>
    private string ObtenerUsuarioId()
    {
        var usuarioId = currentUserService.UsuarioId
            ?? currentUserService.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrWhiteSpace(usuarioId)
            ? throw new InvalidOperationException("No hay un usuario autenticado para registrar el pago.")
            : usuarioId;
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
