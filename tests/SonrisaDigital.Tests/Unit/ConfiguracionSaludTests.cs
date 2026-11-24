using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SonrisaDigital.Services.Salud;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// El tope de tiempo de los health checks evita que /health/ready espere los
/// reintentos de EF mientras nginx ya cortó la sonda por timeout.
/// </summary>
public sealed class ConfiguracionSaludTests
{
    [Fact]
    public void AplicaElTiempoMaximoATodosLosRegistros()
    {
        var opciones = new HealthCheckServiceOptions();
        opciones.Registrations.Add(new HealthCheckRegistration(
            "database", new CheckSinRespuesta(), failureStatus: null, tags: ["ready"]));
        opciones.Registrations.Add(new HealthCheckRegistration(
            "migraciones", new CheckSinRespuesta(), failureStatus: null, tags: ["ready"]));

        ConfiguracionSalud.AplicarTiempoMaximo(opciones);

        Assert.All(opciones.Registrations, registro =>
            Assert.Equal(ConfiguracionSalud.TiempoMaximoPorCheck, registro.Timeout));
    }

    [Fact]
    public async Task UnCheckQueNoRespondeSeCortaPorTimeout()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddHealthChecks().Add(new HealthCheckRegistration(
            "lento", new CheckSinRespuesta(), failureStatus: null, tags: ["ready"]));
        servicios
            .AddOptions<HealthCheckServiceOptions>()
            .Configure(opciones => ConfiguracionSalud.AplicarTiempoMaximo(
                opciones, TimeSpan.FromMilliseconds(200)));

        await using var proveedor = servicios.BuildServiceProvider();
        var servicio = proveedor.GetRequiredService<HealthCheckService>();

        var informe = await servicio.CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, informe.Status);
        Assert.Contains(
            "timeout",
            informe.Entries["lento"].Description,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Simula una base que no responde: solo termina al cancelarse.</summary>
    private sealed class CheckSinRespuesta : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return HealthCheckResult.Healthy();
        }
    }
}
