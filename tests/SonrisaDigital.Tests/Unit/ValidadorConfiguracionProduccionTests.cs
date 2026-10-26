using Microsoft.Extensions.Configuration;
using SonrisaDigital.Services.Configuracion;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Comprueba que la validación de producción detecte los errores que impedirían
/// un despliegue seguro, sin afectar el entorno de desarrollo ni el runner E2E.
/// </summary>
public class ValidadorConfiguracionProduccionTests
{
    [Fact]
    public void Validar_EnDesarrollo_NoExigeConfiguracionDeProduccion()
    {
        var configuracion = new ConfigurationBuilder().Build();

        var resultado = ValidadorConfiguracionProduccion.Validar(
            configuracion,
            esDesarrollo: true);

        Assert.Empty(resultado.Errores);
        Assert.Empty(resultado.Advertencias);
    }

    [Fact]
    public void Validar_EnProduccion_DetectaFaltantesObligatorios()
    {
        var configuracion = new ConfigurationBuilder().Build();

        var resultado = ValidadorConfiguracionProduccion.Validar(
            configuracion,
            esDesarrollo: false);

        Assert.Contains(
            resultado.Errores,
            error => error.Contains("AllowedHosts", StringComparison.Ordinal));
        Assert.Contains(
            resultado.Errores,
            error => error.Contains("DefaultConnection", StringComparison.Ordinal));
        Assert.Contains(
            resultado.Errores,
            error => error.Contains("DataProtection:KeysPath", StringComparison.Ordinal));
    }

    [Fact]
    public void Validar_EnProduccion_ConConfiguracionValida_NoDevuelveErrores()
    {
        var configuracion = CrearConfiguracionValida();

        var resultado = ValidadorConfiguracionProduccion.Validar(
            configuracion,
            esDesarrollo: false);

        Assert.Empty(resultado.Errores);
        Assert.Empty(resultado.Advertencias);
    }

    [Fact]
    public void Validar_EnProduccion_RechazaComodinYProxyInvalido()
    {
        var configuracion = CrearConfiguracionValida(
            allowedHosts: "*",
            proxy: "no-es-una-ip");

        var resultado = ValidadorConfiguracionProduccion.Validar(
            configuracion,
            esDesarrollo: false);

        Assert.Contains(
            resultado.Errores,
            error => error.Contains("AllowedHosts", StringComparison.Ordinal));
        Assert.Contains(
            resultado.Errores,
            error => error.Contains("IP inválida", StringComparison.Ordinal));
    }

    [Fact]
    public void Validar_EnProduccion_AdvirtiendoProxyVacio()
    {
        var configuracion = CrearConfiguracionValida(proxy: null);

        var resultado = ValidadorConfiguracionProduccion.Validar(
            configuracion,
            esDesarrollo: false);

        Assert.Empty(resultado.Errores);
        Assert.Contains(
            resultado.Advertencias,
            advertencia => advertencia.Contains("KnownProxies", StringComparison.Ordinal));
    }

    /// <summary>
    /// Construye una configuración mínima válida. Los parámetros permiten
    /// simular un dominio comodín, un proxy inválido o una lista vacía.
    /// </summary>
    private static IConfiguration CrearConfiguracionValida(
        string? allowedHosts = "clinica.ejemplo.com",
        string? proxy = "10.0.0.10")
    {
        var valores = new Dictionary<string, string?>
        {
            ["AllowedHosts"] = allowedHosts,
            ["ConnectionStrings:DefaultConnection"] =
                "Server=sql01.ejemplo.local;Database=SonrisaDigital;User Id=sonrisa_app;" +
                "Password=CAMBIA_ESTA_CLAVE;Encrypt=True;TrustServerCertificate=False;",
            ["ReverseProxy:ForwardLimit"] = "1",
            ["DataProtection:KeysPath"] = Path.Combine(
                Path.GetTempPath(),
                "sonrisa-digital-data-protection-keys")
        };

        if (!string.IsNullOrWhiteSpace(proxy))
        {
            valores["ReverseProxy:KnownProxies:0"] = proxy;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .Build();
    }
}
