using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Catalogos;
using SonrisaDigital.Services.HistoriasClinicas;
using SonrisaDigital.Services.HistoriasClinicas.DTOs;
using SonrisaDigital.Services.Tratamientos;
using SonrisaDigital.Services.Tratamientos.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Plan de tratamiento de una consulta: se agrega y se quita en Historia Clínica, y se asigna
/// como tratamiento realizado desde el módulo de Tratamientos.
/// </summary>
public sealed class PlanTratamientoTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task AgregarAlPlanCreaUnItemPendienteSinCobros()
    {
        var consultaId = await CrearConsultaAbiertaAsync();
        var tratamientoId = await CrearTratamientoAsync();
        await using var provider = await CrearProveedorAsync();
        var historia = provider.GetRequiredService<IHistoriaClinicaService>();

        await historia.AgregarPlanItemAsync(new PlanTratamientoItemFormDto
        {
            ConsultaId = consultaId,
            TratamientoId = tratamientoId,
            Precio = 120m
        });

        await using var comprobacion = fixture.CreateContext();
        var item = await comprobacion.PlanesTratamiento.SingleAsync(planItem => planItem.ConsultaId == consultaId);
        Assert.Equal(EstadoPlanTratamiento.Pendiente, item.Estado);
        Assert.Equal(120m, item.Precio);
        Assert.Null(item.TratamientoRealizadoId);
        Assert.False(await comprobacion.TratamientosRealizados.AnyAsync(realizado => realizado.ConsultaId == consultaId));
    }

    [Fact]
    public async Task AsignarDesdeElPlanCreaUnTratamientoRealizadoLigadoALaConsulta()
    {
        var (consultaId, pacienteId, odontologoId) = await CrearConsultaConDatosAsync();
        var itemId = await AgregarItemAsync(consultaId);
        await using var provider = await CrearProveedorAsync();
        var tratamientos = provider.GetRequiredService<ITratamientoService>();

        var realizadoId = await tratamientos.CreateRealizadoAsync(new TratamientoRealizadoFormDto
        {
            PlanTratamientoItemId = itemId,
            Fecha = DateTime.Today,
            Precio = 120m,
            Estado = EstadoTratamiento.EnProceso
        });

        await using var comprobacion = fixture.CreateContext();
        var realizado = await comprobacion.TratamientosRealizados.SingleAsync(item => item.Id == realizadoId);
        Assert.Equal(consultaId, realizado.ConsultaId);
        Assert.Equal(pacienteId, realizado.PacienteId);
        Assert.Equal(odontologoId, realizado.OdontologoId);
        Assert.Equal(EstadoTratamiento.EnProceso, realizado.Estado);

        var item = await comprobacion.PlanesTratamiento.SingleAsync(planItem => planItem.Id == itemId);
        Assert.Equal(EstadoPlanTratamiento.Asignado, item.Estado);
        Assert.Equal(realizadoId, item.TratamientoRealizadoId);
    }

    [Fact]
    public async Task UnItemYaAsignadoNoSePuedeAsignarOtraVez()
    {
        var (consultaId, _, _) = await CrearConsultaConDatosAsync();
        var itemId = await AgregarItemAsync(consultaId);
        await using var provider = await CrearProveedorAsync();
        var tratamientos = provider.GetRequiredService<ITratamientoService>();
        var modelo = new TratamientoRealizadoFormDto
        {
            PlanTratamientoItemId = itemId,
            Fecha = DateTime.Today,
            Precio = 120m,
            Estado = EstadoTratamiento.EnProceso
        };
        await tratamientos.CreateRealizadoAsync(modelo);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => tratamientos.CreateRealizadoAsync(modelo));

        Assert.Contains("ya fue asignado", error.Message);
    }

    [Fact]
    public async Task QuitarUnItemPendienteLoCancela()
    {
        var consultaId = await CrearConsultaAbiertaAsync();
        var itemId = await AgregarItemAsync(consultaId);
        await using var provider = await CrearProveedorAsync();
        var historia = provider.GetRequiredService<IHistoriaClinicaService>();

        await historia.CancelarPlanItemAsync(itemId);

        await using var comprobacion = fixture.CreateContext();
        var item = await comprobacion.PlanesTratamiento.SingleAsync(planItem => planItem.Id == itemId);
        Assert.Equal(EstadoPlanTratamiento.Cancelado, item.Estado);
    }

    [Fact]
    public async Task NoSePuedeCerrarUnaConsultaSinPlanNiNotas()
    {
        var consultaId = await CrearConsultaAbiertaAsync();
        await using var provider = await CrearProveedorAsync();
        var historia = provider.GetRequiredService<IHistoriaClinicaService>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => historia.CerrarConsultaAsync(consultaId));

        Assert.Contains("Agrega al menos un tratamiento al plan", error.Message);
    }

    [Fact]
    public async Task UnaConsultaCierraConUnTratamientoEnElPlan()
    {
        var consultaId = await CrearConsultaAbiertaAsync();
        await AgregarItemAsync(consultaId);
        await using var provider = await CrearProveedorAsync();
        var historia = provider.GetRequiredService<IHistoriaClinicaService>();

        await historia.CerrarConsultaAsync(consultaId);

        await using var comprobacion = fixture.CreateContext();
        var consulta = await comprobacion.ConsultasClinicas.SingleAsync(item => item.Id == consultaId);
        Assert.Equal(EstadoConsulta.Cerrada, consulta.Estado);
    }

    private async Task<int> CrearConsultaAbiertaAsync()
    {
        var (consultaId, _, _) = await CrearConsultaConDatosAsync();
        return consultaId;
    }

    private async Task<(int ConsultaId, int PacienteId, int OdontologoId)> CrearConsultaConDatosAsync()
    {
        await using var db = fixture.CreateContext();
        await TestData.AsegurarUsuarioAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var odontologo = await TestData.CrearOdontologoAsync(db);

        var historia = new HistoriaClinica { PacienteId = paciente.Id, FechaCreacion = DateTime.UtcNow };
        db.HistoriasClinicas.Add(historia);
        await db.SaveChangesAsync();

        var consulta = new ConsultaClinica
        {
            PacienteId = paciente.Id,
            HistoriaClinicaId = historia.Id,
            OdontologoId = odontologo.Id,
            Fecha = DateTime.Today,
            Motivo = "Revisión",
            Diagnostico = "Caries en pieza 36",
            Estado = EstadoConsulta.Abierta
        };
        db.ConsultasClinicas.Add(consulta);
        await db.SaveChangesAsync();

        return (consulta.Id, paciente.Id, odontologo.Id);
    }

    private async Task<int> CrearTratamientoAsync()
    {
        await using var db = fixture.CreateContext();
        var tratamiento = await TestData.CrearTratamientoCatalogoAsync(db);
        return tratamiento.Id;
    }

    private async Task<int> AgregarItemAsync(int consultaId)
    {
        var tratamientoId = await CrearTratamientoAsync();
        await using var provider = await CrearProveedorAsync();
        var historia = provider.GetRequiredService<IHistoriaClinicaService>();

        await historia.AgregarPlanItemAsync(new PlanTratamientoItemFormDto
        {
            ConsultaId = consultaId,
            TratamientoId = tratamientoId,
            Precio = 120m
        });

        await using var comprobacion = fixture.CreateContext();
        return await comprobacion.PlanesTratamiento
            .Where(item => item.ConsultaId == consultaId)
            .OrderByDescending(item => item.Id)
            .Select(item => item.Id)
            .FirstAsync();
    }

    private async Task<ServiceProvider> CrearProveedorAsync()
    {
        await using var db = fixture.CreateContext();
        var cadenaConexion = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("La base de pruebas no tiene cadena de conexión.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(cadenaConexion));

        services.AddSingleton<ITenantProvider>(new TenantMutable { ClinicaId = fixture.ClinicaId });
        services.AddSingleton<ICurrentUserService>(TestSession.Crear(TestSession.UsuarioId, RolesSonrisaDigital.Administrador));
        services.AddSingleton<IAuthorizationService>(new FakeAuthorizationService());
        services.AddSingleton<IAuditoriaService>(new FakeAuditoriaService());
        services.AddSingleton<ICatalogoCache, CatalogoCache>();
        services.AddScoped<IHistoriaClinicaRepository, HistoriaClinicaRepository>();
        services.AddScoped<IHistoriaClinicaService, HistoriaClinicaService>();
        services.AddScoped<ITratamientoRepository, TratamientoRepository>();
        services.AddScoped<ITratamientoService, TratamientoService>();

        return services.BuildServiceProvider();
    }
}
