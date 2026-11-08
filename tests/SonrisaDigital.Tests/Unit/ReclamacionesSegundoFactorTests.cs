using System.Security.Claims;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.Tests.Unit;

public sealed class ReclamacionesSegundoFactorTests
{
    [Fact]
    public void UnaSesionSinLaMarcaNoEstaVerificada()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, "Dueño de prueba")]));

        Assert.False(ReclamacionesSegundoFactor.Verificada(principal));
    }

    [Fact]
    public void SoloLaMarcaEsperadaCuentaComoVerificada()
    {
        var otraMarca = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ReclamacionesSegundoFactor.Tipo, "otro")]));
        Assert.False(ReclamacionesSegundoFactor.Verificada(otraMarca));

        var verificado = new ClaimsPrincipal(
            new ClaimsIdentity([ReclamacionesSegundoFactor.Crear()]));
        Assert.True(ReclamacionesSegundoFactor.Verificada(verificado));
    }
}
