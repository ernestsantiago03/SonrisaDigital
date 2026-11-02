using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Suscripciones;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Clinicas.DTOs;
using SonrisaDigital.Services.Validacion;

namespace SonrisaDigital.Services.Clinicas;

public sealed class ClinicaService(
    ApplicationDbContext dbContext,
    IAuditoriaService auditoriaService,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IClinicaService
{
    private const string Modulo = "Clínicas";

    // Una sola oferta mientras se valida el producto. La prueba no cobra y,
    // al registrar el primer pago, el plan pasa a "Mensual".
    private const string PlanPrueba = "Prueba";
    private const int LimiteUsuariosInicial = 10;

    public async Task<IReadOnlyList<ClinicaListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Clinicas.Ver", "No tienes permiso para consultar clínicas.");

        var hoy = ReglaSuscripcion.Hoy();
        var clinicas = await dbContext.Clinicas
            .AsNoTracking()
            .OrderBy(clinic => clinic.Nombre)
            .ThenBy(clinic => clinic.Id)
            .Select(clinic => new
            {
                clinic.Id,
                clinic.Nombre,
                clinic.Codigo,
                clinic.Estado,
                clinic.MotivoSuspension,
                clinic.FechaAlta,
                clinic.Plan,
                clinic.EnPrueba,
                clinic.FechaPrimerIngreso,
                clinic.FechaVencimiento,
                clinic.LimiteUsuarios
            })
            .ToListAsync(cancellationToken);

        return clinicas
            .Select(clinic => new ClinicaListItemDto(
                clinic.Id,
                clinic.Nombre,
                clinic.Codigo,
                clinic.Estado,
                clinic.MotivoSuspension,
                clinic.FechaAlta,
                clinic.Plan,
                clinic.EnPrueba,
                clinic.FechaVencimiento,
                clinic.LimiteUsuarios,
                ReglaSuscripcion.Evaluar(
                    clinic.Estado, clinic.EnPrueba, clinic.FechaPrimerIngreso, clinic.FechaVencimiento, hoy),
                ReglaSuscripcion.DiasRestantes(clinic.FechaVencimiento, hoy)))
            .ToList();
    }

    public async Task<ClinicaFormDto?> ObtenerFormularioAsync(int id, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Clinicas.Ver", "No tienes permiso para consultar clínicas.");

        var clinica = await dbContext.Clinicas.AsNoTracking().SingleOrDefaultAsync(clinic => clinic.Id == id, cancellationToken);
        return clinica is null ? null : new ClinicaFormDto
        {
            Id = clinica.Id,
            Nombre = clinica.Nombre,
            Codigo = clinica.Codigo,
            Rnc = clinica.Rnc,
            Telefono = clinica.Telefono,
            Correo = clinica.Correo,
            Direccion = clinica.Direccion,
            Horario = clinica.Horario,
            Eslogan = clinica.Eslogan
        };
    }

    public async Task<int> CrearAsync(ClinicaFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Clinicas.Crear", "No tienes permiso para crear clínicas.");
        ArgumentNullException.ThrowIfNull(model);
        // El código se normaliza antes de validarlo: así "Mi Clínica" se acepta como "mi-clinica".
        model.Codigo = NormalizarCodigo(model.Codigo);
        ValidadorDto.PrepararYValidar(model);

        var codigo = model.Codigo;
        if (await dbContext.Clinicas.AnyAsync(clinic => clinic.Codigo == codigo, cancellationToken))
        {
            throw new InvalidOperationException("Ya existe una clínica con ese código.");
        }

        var clinica = new Clinica
        {
            Nombre = model.Nombre,
            Codigo = codigo,
            Rnc = Normalizar(model.Rnc),
            Telefono = Normalizar(model.Telefono),
            Correo = Normalizar(model.Correo),
            Direccion = Normalizar(model.Direccion),
            Horario = Normalizar(model.Horario),
            Eslogan = Normalizar(model.Eslogan),
            Estado = EstadoClinica.Activa,
            FechaAlta = DateTime.UtcNow,
            // La prueba no empieza al crear la clínica sino en su primer ingreso.
            // La regla solo permite el acceso porque EnPrueba es true y aún no
            // hay primer ingreso; al iniciar la prueba se asigna el vencimiento.
            Plan = PlanPrueba,
            EnPrueba = true,
            FechaPrimerIngreso = null,
            FechaVencimiento = null,
            LimiteUsuarios = LimiteUsuariosInicial
        };

        dbContext.Clinicas.Add(clinica);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Creó", Modulo, nameof(Clinica), clinica.Id,
            $"Creó la clínica {clinica.Nombre}.", cancellationToken, clinica.Id);

        return clinica.Id;
    }

    public async Task ActualizarAsync(ClinicaFormDto model, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Clinicas.Editar", "No tienes permiso para editar clínicas.");
        ArgumentNullException.ThrowIfNull(model);
        ValidadorDto.PrepararYValidar(model);

        var clinica = await dbContext.Clinicas.SingleOrDefaultAsync(clinic => clinic.Id == model.Id, cancellationToken)
            ?? throw new InvalidOperationException("La clínica no existe.");

        clinica.Nombre = model.Nombre;
        clinica.Rnc = Normalizar(model.Rnc);
        clinica.Telefono = Normalizar(model.Telefono);
        clinica.Correo = Normalizar(model.Correo);
        clinica.Direccion = Normalizar(model.Direccion);
        clinica.Horario = Normalizar(model.Horario);
        clinica.Eslogan = Normalizar(model.Eslogan);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditoriaService.RegistrarAsync(
            "Modificó", Modulo, nameof(Clinica), clinica.Id,
            $"Modificó los datos de la clínica {clinica.Nombre}.", cancellationToken, clinica.Id);
    }

    public async Task CambiarEstadoAsync(
        int id,
        EstadoClinica estado,
        MotivoSuspension? motivoSuspension = null,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Clinicas.Suspender", "No tienes permiso para suspender o reactivar clínicas.");

        if (estado == EstadoClinica.Vencida)
        {
            throw new InvalidOperationException("El estado vencida se calcula por la fecha de vencimiento y no se asigna manualmente.");
        }

        if (estado == EstadoClinica.Suspendida && motivoSuspension is null)
        {
            throw new InvalidOperationException("Indica si la suspensión es por falta de pago o por decisión administrativa.");
        }

        var clinica = await dbContext.Clinicas.SingleOrDefaultAsync(clinic => clinic.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("La clínica no existe.");

        var motivoNuevo = estado == EstadoClinica.Suspendida ? motivoSuspension : null;
        if (clinica.Estado == estado && clinica.MotivoSuspension == motivoNuevo)
        {
            return;
        }

        clinica.Estado = estado;
        clinica.MotivoSuspension = motivoNuevo;
        await dbContext.SaveChangesAsync(cancellationToken);

        var detalle = estado == EstadoClinica.Suspendida
            ? $"Suspendió la clínica {clinica.Nombre}. Motivo: {TextoMotivo(motivoSuspension)}."
            : $"Reactivó la clínica {clinica.Nombre}.";

        await auditoriaService.RegistrarAsync(
            estado == EstadoClinica.Suspendida ? "Suspendió" : "Reactivó",
            Modulo, nameof(Clinica), clinica.Id,
            detalle, cancellationToken, clinica.Id);
    }

    private static string TextoMotivo(MotivoSuspension? motivo) => motivo switch
    {
        MotivoSuspension.FaltaDePago => "falta de pago",
        MotivoSuspension.DecisionAdministrativa => "decisión administrativa",
        _ => "sin motivo"
    };

    private static string NormalizarCodigo(string codigo) =>
        codigo.Trim().ToLowerInvariant().Replace(' ', '-');

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
