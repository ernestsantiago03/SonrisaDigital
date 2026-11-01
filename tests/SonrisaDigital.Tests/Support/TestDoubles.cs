using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Auditoria.DTOs;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.HistoriasClinicas;
using SonrisaDigital.Services.HistoriasClinicas.DTOs;
using SonrisaDigital.Services.Pagos;

namespace SonrisaDigital.Tests.Support;

/// <summary>
/// Autorización de pruebas: permite o niega permisos sin depender del motor de
/// políticas de ASP.NET Core. Además de permitir/negar todo, permite denegar
/// políticas concretas para probar flujos con permisos separados.
/// </summary>
internal sealed class FakeAuthorizationService : IAuthorizationService
{
    private readonly bool permitirTodo;
    private readonly HashSet<string> denegadas;

    public FakeAuthorizationService(bool permitir = true, params string[] denegadas)
    {
        permitirTodo = permitir;
        this.denegadas = new HashSet<string>(denegadas, StringComparer.Ordinal);
    }

    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        IEnumerable<IAuthorizationRequirement> requirements) =>
        Task.FromResult(permitirTodo ? AuthorizationResult.Success() : AuthorizationResult.Failed());

    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        string policyName) =>
        Task.FromResult(permitirTodo && !denegadas.Contains(policyName)
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());
}

/// <summary>
/// Sesión mínima para los servicios que exigen un usuario autenticado.
/// </summary>
internal static class TestSession
{
    public const string UsuarioId = "usuario-pruebas";

    public static CurrentUserService Crear(string usuarioId = UsuarioId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId),
            new(ClaimTypes.Name, "Usuario de pruebas")
        };

        // Las pruebas pueden simular un rol concreto (por ejemplo, Recepcionista)
        // sin tener que montar todo el sistema de identidad.
        claims.AddRange(roles.Select(rol => new Claim(ClaimTypes.Role, rol)));

        var identidad = new ClaimsIdentity(claims, "Pruebas");

        var servicio = new CurrentUserService();
        servicio.Establecer(usuarioId, "Usuario de pruebas", new ClaimsPrincipal(identidad));
        return servicio;
    }
}

/// <summary>
/// Auditoría de pruebas. Las operaciones auditadas no deben escribir en la base
/// durante una prueba de concurrencia: lo que se valida es el bloqueo y el
/// guardado del pago o de la cita.
/// </summary>
internal sealed class FakeAuditoriaService : IAuditoriaService
{
    private readonly object candado = new();
    private readonly List<RegistroAuditoria> registros = [];

    /// <summary>Movimientos registrados, para comprobar qué clínica recibió la auditoría.</summary>
    public IReadOnlyList<RegistroAuditoria> Registros
    {
        get
        {
            lock (candado)
            {
                return registros.ToArray();
            }
        }
    }

    public Task RegistrarAsync(
        string accion,
        string modulo,
        string entidad,
        int? entidadId,
        string descripcion,
        CancellationToken cancellationToken = default,
        int? clinicaId = null)
    {
        // Las pruebas de concurrencia auditan desde varios hilos a la vez.
        lock (candado)
        {
            registros.Add(new RegistroAuditoria(accion, modulo, entidad, entidadId, clinicaId));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActividadRecienteDto>> GetRecientesAsync(
        int cantidad = 20,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AuditoriaPaginaDto> SearchAsync(
        AuditoriaFiltroDto filtro,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AuditoriaOpcionesDto> GetOpcionesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>Movimiento capturado por la auditoría de pruebas.</summary>
internal sealed record RegistroAuditoria(
    string Accion,
    string Modulo,
    string Entidad,
    int? EntidadId,
    int? ClinicaId);

/// <summary>
/// Historia clínica de pruebas. Las citas programadas no generan consulta;
/// si una prueba llegara a pedirla, devuelve un identificador ficticio.
/// </summary>
internal sealed class FakeHistoriaClinicaService : IHistoriaClinicaService
{
    /// <summary>
    /// Permite simular que el usuario no tiene permiso clínico al marcar una cita
    /// como atendida y comprobar que la transacción revierte el cambio de estado.
    /// </summary>
    public bool FallarAlRegistrarConsulta { get; set; }

    public int ConsultasRegistradas { get; private set; }

    public Task<PagedResult<HistoriaClinicaPacienteDto>> SearchPatientsPagedAsync(
        string? searchTerm,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<HistoriaClinicaDto?> GetByPatientAsync(
        int pacienteId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SaveAsync(HistoriaClinicaDto model, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<OdontologoOptionDto>> GetOdontologosAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int?> GetOdontologoVinculadoAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task CreateConsultaAsync(
        ConsultaClinicaFormDto model,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UpdateConsultaAsync(
        ConsultaClinicaFormDto model,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int> RegistrarConsultaDeCitaAsync(
        Cita cita,
        CancellationToken cancellationToken = default)
    {
        ConsultasRegistradas++;

        if (FallarAlRegistrarConsulta)
        {
            throw new UnauthorizedAccessException("No tienes permiso para crear información clínica.");
        }

        return Task.FromResult(1);
    }

    public Task CerrarConsultaAsync(int consultaId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TratamientoPlanOptionDto>> GetTratamientosPlanAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PiezaPlanOptionDto>> GetPiezasPlanAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task AgregarPlanItemAsync(PlanTratamientoItemFormDto model, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task CancelarPlanItemAsync(int itemId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>
/// Repositorio de pacientes en memoria para probar permisos y reglas del servicio
/// sin depender de SQL Server. Mantiene las entidades tal como las recibe el servicio.
/// </summary>
internal sealed class StubPacienteRepository : IPacienteRepository
{
    private int siguienteId = 1;

    public List<Paciente> Pacientes { get; } = [];

    public Task<IReadOnlyList<Paciente>> SearchAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Paciente>>(Pacientes);

    public Task<PagedResult<Paciente>> SearchPagedAsync(
        string? searchTerm,
        EstadoRegistro? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<Paciente>(Pacientes, Pacientes.Count, page, pageSize));

    public Task<int> CountAsync(
        EstadoRegistro? estado = null,
        DateTime? registradosDesdeUtc = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Pacientes.Count);

    public Task<Paciente?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pacientes.FirstOrDefault(paciente => paciente.Id == id));

    public Task<bool> ExistsWithIdentificationAsync(
        string identification,
        int? excludedId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Pacientes.Any(paciente =>
            paciente.Identificacion == identification &&
            (!excludedId.HasValue || paciente.Id != excludedId.Value)));

    public Task AddAsync(Paciente paciente, CancellationToken cancellationToken = default)
    {
        paciente.Id = siguienteId++;
        if (paciente.HistoriaClinica is not null)
        {
            paciente.HistoriaClinica.PacienteId = paciente.Id;
        }

        Pacientes.Add(paciente);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

/// <summary>
/// Repositorio de pagos mínimo para probar cálculos que no tocan la base,
/// como el saldo pendiente del resumen.
/// </summary>
internal sealed class StubPagoRepository(PagoTotales totales) : IPagoRepository
{
    public Task<PagoTotales> GetTotalesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(totales);

    public Task<PagedResult<TratamientoRealizado>> SearchTratamientosPagedAsync(
        string? searchTerm,
        EstadoFinanciero? estado,
        int? pacienteId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TratamientoRealizado?> GetTratamientoAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TratamientoRealizado?> GetTratamientoParaPagoAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TratamientoRealizado?> GetDetalleAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TratamientoRealizado>> GetByPacienteAsync(
        int pacienteId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Pago>> GetHistorialAsync(
        int cantidad,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task AddAsync(Pago pago, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>
/// Barrera asíncrona para que dos operaciones lleguen al mismo punto antes de
/// continuar. Se usa en las pruebas de concurrencia para provocar la carrera.
/// </summary>
internal sealed class AsyncBarrier(int participantes)
{
    private int _llegadas;
    private readonly TaskCompletionSource _liberar =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task EsperarAsync(TimeSpan timeout)
    {
        if (Interlocked.Increment(ref _llegadas) == participantes)
        {
            _liberar.TrySetResult();
        }

        await _liberar.Task.WaitAsync(timeout);
    }
}

/// <summary>
/// Envuelve el repositorio de citas y detiene la comprobación de choque hasta
/// que las dos transacciones hayan leído el horario. Después las deja continuar
/// para comprobar que el aislamiento Serializable impide duplicar la reserva.
/// </summary>
internal sealed class BarrierCitaRepository(ICitaRepository interno, AsyncBarrier barrera)
    : ICitaRepository
{
    public Task<IReadOnlyList<Cita>> SearchAsync(
        DateOnly? fecha = null,
        int? pacienteId = null,
        int? odontologoId = null,
        EstadoCita? estado = null,
        CancellationToken cancellationToken = default) =>
        interno.SearchAsync(fecha, pacienteId, odontologoId, estado, cancellationToken);

    public Task<PagedResult<Cita>> SearchPagedAsync(
        DateOnly? fecha,
        int? pacienteId,
        int? odontologoId,
        EstadoCita? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        interno.SearchPagedAsync(fecha, pacienteId, odontologoId, estado, page, pageSize, cancellationToken);

    public Task<Cita?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        interno.GetByIdAsync(id, cancellationToken);

    public async Task<CitaConflicto> BuscarConflictoAsync(
        int odontologoId,
        int pacienteId,
        DateOnly fecha,
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int? excluirCitaId = null,
        CancellationToken cancellationToken = default)
    {
        var conflicto = await interno.BuscarConflictoAsync(
            odontologoId,
            pacienteId,
            fecha,
            horaInicio,
            horaFin,
            excluirCitaId,
            cancellationToken);

        await barrera.EsperarAsync(TimeSpan.FromSeconds(15));
        return conflicto;
    }

    public Task<Cita> AddAsync(Cita cita, CancellationToken cancellationToken = default) =>
        interno.AddAsync(cita, cancellationToken);

    public Task<Cita> UpdateAsync(Cita cita, CancellationToken cancellationToken = default) =>
        interno.UpdateAsync(cita, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        interno.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Envuelve el repositorio de pagos y espera a que las dos cajas estén dentro
/// de su transacción antes de pedir el bloqueo del tratamiento.
/// </summary>
internal sealed class BarrierPagoRepository(IPagoRepository interno, AsyncBarrier barrera)
    : IPagoRepository
{
    public Task<PagedResult<TratamientoRealizado>> SearchTratamientosPagedAsync(
        string? searchTerm,
        EstadoFinanciero? estado,
        int? pacienteId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        interno.SearchTratamientosPagedAsync(searchTerm, estado, pacienteId, page, pageSize, cancellationToken);

    public Task<PagoTotales> GetTotalesAsync(CancellationToken cancellationToken = default) =>
        interno.GetTotalesAsync(cancellationToken);

    public Task<TratamientoRealizado?> GetTratamientoAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default) =>
        interno.GetTratamientoAsync(tratamientoRealizadoId, cancellationToken);

    public async Task<TratamientoRealizado?> GetTratamientoParaPagoAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default)
    {
        await barrera.EsperarAsync(TimeSpan.FromSeconds(15));
        return await interno.GetTratamientoParaPagoAsync(tratamientoRealizadoId, cancellationToken);
    }

    public Task<TratamientoRealizado?> GetDetalleAsync(
        int tratamientoRealizadoId,
        CancellationToken cancellationToken = default) =>
        interno.GetDetalleAsync(tratamientoRealizadoId, cancellationToken);

    public Task<IReadOnlyList<TratamientoRealizado>> GetByPacienteAsync(
        int pacienteId,
        CancellationToken cancellationToken = default) =>
        interno.GetByPacienteAsync(pacienteId, cancellationToken);

    public Task<IReadOnlyList<Pago>> GetHistorialAsync(
        int cantidad,
        CancellationToken cancellationToken = default) =>
        interno.GetHistorialAsync(cantidad, cancellationToken);

    public Task AddAsync(Pago pago, CancellationToken cancellationToken = default) =>
        interno.AddAsync(pago, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        interno.SaveChangesAsync(cancellationToken);
}
