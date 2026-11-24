using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SonrisaDigital.Services.Salud;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// El JSON de /health y /health/ready lo consumen nginx, el balanceador y los
/// scripts de despliegue. Estas pruebas fijan el contrato en un único lugar:
/// antes existían dos implementaciones y solo una de ellas se ejecutaba.
/// </summary>
public sealed class RespuestaSaludTests
{
    [Fact]
    public void UnaBaseAlDiaSeReportaComoReachable()
    {
        var informe = Informe(
            (RespuestaSalud.CheckBaseDatos, HealthStatus.Healthy, null),
            (RespuestaSalud.CheckMigraciones, HealthStatus.Healthy, null));

        var (estado, baseDatos) = RespuestaSalud.Interpretar(informe);

        Assert.Equal("Healthy", estado);
        Assert.Equal("reachable", baseDatos);
    }

    [Fact]
    public void UnaBaseInalcanzableSeInformaAntesQueLasMigraciones()
    {
        // Si la base no responde, el check de migraciones también falla. El
        // diagnóstico correcto es "unreachable", no "schema-outdated".
        var informe = Informe(
            (RespuestaSalud.CheckBaseDatos, HealthStatus.Unhealthy, null),
            (RespuestaSalud.CheckMigraciones, HealthStatus.Unhealthy, new InvalidOperationException("sin conexión")));

        var (estado, baseDatos) = RespuestaSalud.Interpretar(informe);

        Assert.Equal("Unhealthy", estado);
        Assert.Equal("unreachable", baseDatos);
    }

    [Fact]
    public void UnErrorAlConsultarLaBaseSeReportaComoError()
    {
        var informe = Informe(
            (RespuestaSalud.CheckBaseDatos, HealthStatus.Unhealthy, new InvalidOperationException("cadena inválida")));

        var (estado, baseDatos) = RespuestaSalud.Interpretar(informe);

        Assert.Equal("Unhealthy", estado);
        Assert.Equal("error", baseDatos);
    }

    [Fact]
    public void MigracionesPendientesSeReportanComoSchemaOutdated()
    {
        var informe = Informe(
            (RespuestaSalud.CheckBaseDatos, HealthStatus.Healthy, null),
            (RespuestaSalud.CheckMigraciones, HealthStatus.Unhealthy, null));

        var (estado, baseDatos) = RespuestaSalud.Interpretar(informe);

        Assert.Equal("Unhealthy", estado);
        Assert.Equal("schema-outdated", baseDatos);
    }

    [Fact]
    public void UnInformeSinElCheckDeBaseSeReportaComoError()
    {
        var informe = Informe((RespuestaSalud.CheckMigraciones, HealthStatus.Healthy, null));

        var (_, baseDatos) = RespuestaSalud.Interpretar(informe);

        Assert.Equal("error", baseDatos);
    }

    [Fact]
    public async Task LiveEscribeSoloElEstadoYNoTocaLaBase()
    {
        var contexto = CrearContexto(out var leerCuerpo);

        // /health se configura con Predicate = _ => false: el informe llega vacío
        // y el estado agregado es Healthy sin ejecutar ningún check.
        await RespuestaSalud.EscribirLiveAsync(contexto, Informe());

        using var documento = JsonDocument.Parse(leerCuerpo());
        Assert.Equal("application/json; charset=utf-8", contexto.Response.ContentType);
        Assert.Equal("Healthy", documento.RootElement.GetProperty("status").GetString());
        Assert.False(documento.RootElement.TryGetProperty("database", out _));
    }

    [Fact]
    public async Task ReadyEscribeEstadoYBaseDeDatos()
    {
        var contexto = CrearContexto(out var leerCuerpo);
        var informe = Informe(
            (RespuestaSalud.CheckBaseDatos, HealthStatus.Healthy, null),
            (RespuestaSalud.CheckMigraciones, HealthStatus.Healthy, null));

        await RespuestaSalud.EscribirReadyAsync(contexto, informe);

        using var documento = JsonDocument.Parse(leerCuerpo());
        Assert.Equal("application/json; charset=utf-8", contexto.Response.ContentType);
        Assert.Equal("Healthy", documento.RootElement.GetProperty("status").GetString());
        Assert.Equal("reachable", documento.RootElement.GetProperty("database").GetString());
    }

    private static HealthReport Informe(
        params (string Nombre, HealthStatus Estado, Exception? Excepcion)[] entradas)
    {
        var diccionario = entradas.ToDictionary(
            entrada => entrada.Nombre,
            entrada => new HealthReportEntry(
                entrada.Estado,
                description: null,
                duration: TimeSpan.Zero,
                exception: entrada.Excepcion,
                data: null),
            StringComparer.OrdinalIgnoreCase);

        return new HealthReport(diccionario, TimeSpan.Zero);
    }

    private static DefaultHttpContext CrearContexto(out Func<string> leerCuerpo)
    {
        var contexto = new DefaultHttpContext();
        var flujo = new MemoryStream();
        contexto.Response.Body = flujo;

        leerCuerpo = () => Encoding.UTF8.GetString(flujo.ToArray());

        return contexto;
    }
}
