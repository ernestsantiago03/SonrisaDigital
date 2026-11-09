using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Catalogos;
using SonrisaDigital.Services.Tratamientos;
using SonrisaDigital.Services.Tratamientos.DTOs;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>La consulta asociada a un tratamiento es opcional, pero debe ser del mismo paciente.</summary>
public sealed class TratamientoConsultaTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task UnTratamientoGuardaLaConsultaDelMismoPaciente()
    {
        var (paciente, odontologo, catalogo) = await PrepararAsync();
        var consultaId = await CrearConsultaAsync(paciente.Id, odontologo.Id);
        await using var provider = await CrearProveedorAsync();
        var servicio = provider.GetRequiredService<ITratamientoService>();

        var id = await servicio.CreateRealizadoAsync(Formulario(paciente.Id, odontologo.Id, catalogo.Id, consultaId));

        await using var comprobacion = fixture.CreateContext();
        var guardado = await comprobacion.TratamientosRealizados.SingleAsync(item => item.Id == id);
        Assert.Equal(consultaId, guardado.ConsultaId);
    }

    [Fact]
    public async Task UnTratamientoSinConsultaSigueSiendoValido()
    {
        var (paciente, odontologo, catalogo) = await PrepararAsync();
        await using var provider = await CrearProveedorAsync();
        var servicio = provider.GetRequiredService<ITratamientoService>();

        var id = await servicio.CreateRealizadoAsync(Formulario(paciente.Id, odontologo.Id, catalogo.Id, consultaId: null));

        await using var comprobacion = fixture.CreateContext();
        var guardado = await comprobacion.TratamientosRealizados.SingleAsync(item => item.Id == id);
        Assert.Null(guardado.ConsultaId);
    }

    [Fact]
    public async Task NoAceptaUnaConsultaDeOtroPaciente()
    {
        var (paciente, odontologo, catalogo) = await PrepararAsync();
        var (otro, _, _) = await PrepararAsync();
        var consultaDeOtro = await CrearConsultaAsync(otro.Id, odontologo.Id);
        await using var provider = await CrearProveedorAsync();
        var servicio = provider.GetRequiredService<ITratamientoService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.CreateRealizadoAsync(Formulario(paciente.Id, odontologo.Id, catalogo.Id, consultaDeOtro)));
    }

    private async Task<(Paciente Paciente, Odontologo Odontologo, Tratamiento Catalogo)> PrepararAsync()
    {
        await using var db = fixture.CreateContext();
        await TestData.AsegurarUsuarioAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var catalogo = await TestData.CrearTratamientoCatalogoAsync(db);
        return (paciente, odontologo, catalogo);
    }

    private async Task<int> CrearConsultaAsync(int pacienteId, int odontologoId)
    {
        await using var db = fixture.CreateContext();
        var historia = new HistoriaClinica { PacienteId = pacienteId };
        db.HistoriasClinicas.Add(historia);
        await db.SaveChangesAsync();

        var consulta = new ConsultaClinica
        {
            PacienteId = pacienteId,
            OdontologoId = odontologoId,
            HistoriaClinicaId = historia.Id,
            Fecha = RelojNegocio.HoyDateOnly(),
            Motivo = "Revisión de prueba"
        };
        db.ConsultasClinicas.Add(consulta);
        await db.SaveChangesAsync();
        return consulta.Id;
    }

    private static TratamientoRealizadoFormDto Formulario(int pacienteId, int odontologoId, int tratamientoId, int? consultaId) => new()
    {
        PacienteId = pacienteId,
        TratamientoId = tratamientoId,
        OdontologoId = odontologoId,
        ConsultaId = consultaId,
        Fecha = RelojNegocio.HoyDateOnly(),
        Precio = 100m,
        Estado = EstadoTratamiento.EnProceso
    };

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
        services.AddSingleton<ICurrentUserService>(TestSession.Crear(TestSession.UsuarioId, RolesSonrisaDigital.Dueno));
        services.AddSingleton<IAuthorizationService>(new FakeAuthorizationService());
        services.AddSingleton<IAuditoriaService>(new FakeAuditoriaService());
        services.AddSingleton<ICatalogoCache, CatalogoCache>();
        services.AddScoped<ITratamientoRepository, TratamientoRepository>();
        services.AddScoped<ITratamientoService, TratamientoService>();

        return services.BuildServiceProvider();
    }
}
