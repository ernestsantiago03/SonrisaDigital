using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Suscripciones;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Suscripciones;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// El bloqueo de suscripción debe cubrir cualquier petición autenticada, no solo
/// las navegaciones HTML. Estas pruebas simulan archivos, PDF y llamadas de Blazor.
/// </summary>
public sealed class SuscripcionAccessMiddlewareTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task UnaClinicaVencidaRecibe403EnUnaDescarga()
    {
        await using var db = fixture.CreateContext();
        var clinica = await CrearClinicaAsync(db, EstadoClinica.Activa, diasVencimiento: -5);
        var autenticacion = new FakeAuthenticationService();
        var (middleware, estado) = CrearMiddleware();
        var contexto = CrearContexto(
            UsuarioDeClinica(clinica.Id),
            "application/json",
            autenticacion);

        await middleware.InvokeAsync(contexto, db);

        Assert.False(estado.Llamado);
        Assert.Equal(StatusCodes.Status403Forbidden, contexto.Response.StatusCode);
        Assert.Equal(1, autenticacion.SignOutLlamadas);
        Assert.Contains("clinic-expired", await LeerCuerpoAsync(contexto));
    }

    [Fact]
    public async Task UnaDescargaDePdfTambienRecibe403AunqueElNavegadorPidaHtml()
    {
        await using var db = fixture.CreateContext();
        var clinica = await CrearClinicaAsync(db, EstadoClinica.Activa, diasVencimiento: -5);
        var autenticacion = new FakeAuthenticationService();
        var (middleware, estado) = CrearMiddleware();
        var contexto = CrearContexto(
            UsuarioDeClinica(clinica.Id),
            "text/html,application/xhtml+xml",
            autenticacion);
        contexto.Request.Path = "/reportes/pdf";

        await middleware.InvokeAsync(contexto, db);

        Assert.False(estado.Llamado);
        Assert.Equal(StatusCodes.Status403Forbidden, contexto.Response.StatusCode);
        Assert.Contains("clinic-expired", await LeerCuerpoAsync(contexto));
    }

    [Fact]
    public async Task UnaClinicaVencidaSeRedirigeAlLoginEnNavegacionHtml()
    {
        await using var db = fixture.CreateContext();
        var clinica = await CrearClinicaAsync(db, EstadoClinica.Activa, diasVencimiento: -5);
        var autenticacion = new FakeAuthenticationService();
        var (middleware, estado) = CrearMiddleware();
        var contexto = CrearContexto(
            UsuarioDeClinica(clinica.Id),
            "text/html,application/xhtml+xml",
            autenticacion);

        await middleware.InvokeAsync(contexto, db);

        Assert.False(estado.Llamado);
        Assert.Equal(StatusCodes.Status302Found, contexto.Response.StatusCode);
        Assert.Equal("/login?error=clinic-expired", contexto.Response.Headers.Location);
        Assert.Equal(1, autenticacion.SignOutLlamadas);
    }

    [Fact]
    public async Task UnaClinicaSuspendidaTambienQuedaBloqueada()
    {
        await using var db = fixture.CreateContext();
        var clinica = await CrearClinicaAsync(db, EstadoClinica.Suspendida, diasVencimiento: 30);
        var autenticacion = new FakeAuthenticationService();
        var (middleware, estado) = CrearMiddleware();
        var contexto = CrearContexto(
            UsuarioDeClinica(clinica.Id),
            "application/json",
            autenticacion);

        await middleware.InvokeAsync(contexto, db);

        Assert.False(estado.Llamado);
        Assert.Equal(StatusCodes.Status403Forbidden, contexto.Response.StatusCode);
        Assert.Contains("clinic-suspended", await LeerCuerpoAsync(contexto));
    }

    [Fact]
    public async Task UnaClinicaAlDiaPuedeUsarElSistema()
    {
        await using var db = fixture.CreateContext();
        var clinica = await CrearClinicaAsync(db, EstadoClinica.Activa, diasVencimiento: 5);
        var autenticacion = new FakeAuthenticationService();
        var (middleware, estado) = CrearMiddleware();
        var contexto = CrearContexto(
            UsuarioDeClinica(clinica.Id),
            "application/json",
            autenticacion);

        await middleware.InvokeAsync(contexto, db);

        Assert.True(estado.Llamado);
        Assert.Equal(0, autenticacion.SignOutLlamadas);
    }

    [Fact]
    public async Task ElDuenoPuedeEntrarSinPertenecerAUnaClinica()
    {
        await using var db = fixture.CreateContext();
        var autenticacion = new FakeAuthenticationService();
        var (middleware, estado) = CrearMiddleware();
        var dueno = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "dueno"),
            new Claim(ClaimTypes.Role, RolesSonrisaDigital.Dueno)
        ], "Test"));
        var contexto = CrearContexto(dueno, "text/html", autenticacion);

        await middleware.InvokeAsync(contexto, db);

        Assert.True(estado.Llamado);
        Assert.Equal(0, autenticacion.SignOutLlamadas);
    }

    [Fact]
    public async Task LasRutasPublicasNoConsultanLaSuscripcion()
    {
        await using var db = fixture.CreateContext();
        var autenticacion = new FakeAuthenticationService();
        var (middleware, estado) = CrearMiddleware();
        var contexto = CrearContexto(UsuarioDeClinica(0), "text/html", autenticacion);
        contexto.Request.Path = "/login";

        await middleware.InvokeAsync(contexto, db);

        Assert.True(estado.Llamado);
        Assert.Equal(0, autenticacion.SignOutLlamadas);
    }

    private static (SuscripcionAccessMiddleware Middleware, EstadoSiguiente Estado) CrearMiddleware()
    {
        var estado = new EstadoSiguiente();
        var middleware = new SuscripcionAccessMiddleware(
            _ =>
            {
                estado.Llamado = true;
                return Task.CompletedTask;
            },
            NullLogger<SuscripcionAccessMiddleware>.Instance);

        return (middleware, estado);
    }

    private static DefaultHttpContext CrearContexto(
        ClaimsPrincipal usuario,
        string accept,
        IAuthenticationService autenticacion)
    {
        var services = new ServiceCollection()
            .AddSingleton(autenticacion)
            .BuildServiceProvider();

        var contexto = new DefaultHttpContext
        {
            RequestServices = services,
            User = usuario
        };

        contexto.Request.Headers.Accept = accept;
        contexto.Response.Body = new MemoryStream();
        return contexto;
    }

    private static ClaimsPrincipal UsuarioDeClinica(int clinicaId)
    {
        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "usuario"),
            new Claim(ReclamacionesClinica.ClinicaId, clinicaId.ToString())
        ], "Test");

        return new ClaimsPrincipal(identidad);
    }

    private static async Task<string> LeerCuerpoAsync(HttpContext contexto)
    {
        contexto.Response.Body.Position = 0;
        using var lector = new StreamReader(contexto.Response.Body);
        return await lector.ReadToEndAsync();
    }

    private static async Task<Clinica> CrearClinicaAsync(
        ApplicationDbContext db,
        EstadoClinica estado,
        int diasVencimiento)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var clinica = new Clinica
        {
            Nombre = $"Clínica middleware {sufijo}",
            Codigo = $"middleware-{sufijo}",
            Estado = estado,
            FechaAlta = DateTime.UtcNow,
            Plan = "Mensual",
            EnPrueba = false,
            FechaVencimiento = ReglaSuscripcion.Hoy().AddDays(diasVencimiento),
            LimiteUsuarios = 10
        };

        db.Clinicas.Add(clinica);
        await db.SaveChangesAsync();
        return clinica;
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public int SignOutLlamadas { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            SignOutLlamadas++;
            return Task.CompletedTask;
        }
    }

    private sealed class EstadoSiguiente
    {
        public bool Llamado { get; set; }
    }
}
