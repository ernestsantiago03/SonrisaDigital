using Microsoft.AspNetCore.Authentication;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.Tests.Unit;

/// <summary>Reglas reforzadas para el rol Dueño: contraseña, sesión y mínimo de cuentas activas.</summary>
public class SeguridadDuenoTests
{
    [Fact]
    public void LaContrasenaDelDuenoNecesitaAlMenos16Caracteres()
    {
        Assert.NotNull(SeguridadDueno.ValidarClave("corta1234567890"));
        Assert.NotNull(SeguridadDueno.ValidarClave(null));
        Assert.NotNull(SeguridadDueno.ValidarClave(string.Empty));
        Assert.Null(SeguridadDueno.ValidarClave("una-clave-de-16-ok"));
    }

    [Fact]
    public void UnaSesionRecienteNoEstaVencida()
    {
        var ahora = DateTimeOffset.UtcNow;
        var propiedades = new AuthenticationProperties();
        SeguridadDueno.MarcarInicioSesion(propiedades, ahora.AddMinutes(-30));

        Assert.False(SeguridadDueno.SesionVencida(propiedades, ahora));
    }

    [Fact]
    public void UnaSesionDeMasDeDosHorasEstaVencida()
    {
        var ahora = DateTimeOffset.UtcNow;
        var propiedades = new AuthenticationProperties();
        SeguridadDueno.MarcarInicioSesion(propiedades, ahora.AddHours(-2).AddSeconds(-1));

        Assert.True(SeguridadDueno.SesionVencida(propiedades, ahora));
    }

    [Fact]
    public void UnaSesionSinMarcaDeInicioSeConsideraVencida()
    {
        Assert.True(SeguridadDueno.SesionVencida(new AuthenticationProperties(), DateTimeOffset.UtcNow));
        Assert.True(SeguridadDueno.SesionVencida(null, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    public void SiempreQuedanDosDuenosActivos(int duenosActivosAntes, bool puedeDesactivarse)
    {
        Assert.Equal(puedeDesactivarse, SeguridadDueno.PuedeDesactivarse(duenosActivosAntes));
    }
}
