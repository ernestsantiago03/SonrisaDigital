using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Suscripciones;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Regla única de acceso por suscripción. Es la misma que usan el login, el
/// middleware y la revalidación de sesiones, así que sus límites se prueban aquí.
/// </summary>
public sealed class ReglaSuscripcionTests
{
    private static readonly DateTime Hoy = new(2026, 11, 1);

    [Fact]
    public void UnaClinicaActivaConVencimientoFuturoEntra()
    {
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Activa, enPrueba: false, fechaPrimerIngreso: null,
            fechaVencimiento: Hoy.AddDays(10), Hoy);

        Assert.Equal(AccesoClinica.Permitido, acceso);
    }

    [Fact]
    public void UnaClinicaQueVenceHoyEntraTodoElDia()
    {
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Activa, enPrueba: false, fechaPrimerIngreso: null,
            fechaVencimiento: Hoy, Hoy);

        Assert.Equal(AccesoClinica.Permitido, acceso);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LosTresDiasDeGraciaDejanEntrar(int diasVencida)
    {
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Activa, enPrueba: false, fechaPrimerIngreso: null,
            fechaVencimiento: Hoy.AddDays(-diasVencida), Hoy);

        Assert.Equal(AccesoClinica.EnGracia, acceso);
    }

    [Fact]
    public void AlCuartoDiaVencidaSeBloquea()
    {
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Activa, enPrueba: false, fechaPrimerIngreso: null,
            fechaVencimiento: Hoy.AddDays(-4), Hoy);

        Assert.Equal(AccesoClinica.Vencida, acceso);
    }

    [Fact]
    public void SinDiasDeGraciaSeBloqueaAlDiaSiguiente()
    {
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Activa, enPrueba: false, fechaPrimerIngreso: null,
            fechaVencimiento: Hoy.AddDays(-1), Hoy, diasGracia: 0);

        Assert.Equal(AccesoClinica.Vencida, acceso);
    }

    [Fact]
    public void LaSuspensionManualGanaSobreUnaFechaFutura()
    {
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Suspendida, enPrueba: false, fechaPrimerIngreso: null,
            fechaVencimiento: Hoy.AddDays(30), Hoy);

        Assert.Equal(AccesoClinica.Suspendida, acceso);
    }

    [Fact]
    public void UnaPruebaSinPrimerIngresoPuedeEntrarAunqueNoTengaFecha()
    {
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Activa, enPrueba: true, fechaPrimerIngreso: null,
            fechaVencimiento: null, Hoy);

        Assert.Equal(AccesoClinica.Permitido, acceso);
    }

    [Fact]
    public void UnaPruebaIniciadaHaceMasDeUnMesSeBloquea()
    {
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Activa, enPrueba: true, fechaPrimerIngreso: Hoy.AddDays(-45),
            fechaVencimiento: ReglaSuscripcion.VencimientoDePrueba(Hoy.AddDays(-45)), Hoy);

        // La prueba venció hace 15 días: ya pasaron los 3 días de gracia.
        Assert.Equal(AccesoClinica.Vencida, acceso);
    }

    [Fact]
    public void UnaClinicaSinFechaDeVencimientoQuedaVencida()
    {
        // Sin un período válido no hay derecho de acceso. Las clínicas exentas
        // deben tener una fecha vigente, no un vencimiento nulo permanente.
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Activa, enPrueba: false, fechaPrimerIngreso: null,
            fechaVencimiento: null, Hoy);

        Assert.Equal(AccesoClinica.Vencida, acceso);
    }

    [Fact]
    public void UnaPruebaIniciadaSinFechaDeVencimientoQuedaVencida()
    {
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Activa, enPrueba: true, fechaPrimerIngreso: Hoy.AddDays(-1),
            fechaVencimiento: null, Hoy);

        Assert.Equal(AccesoClinica.Vencida, acceso);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    public void LosDiasDeGraciaRestantesSeCalculanDesdeElVencimiento(int diasVencida, int esperados)
    {
        var dias = ReglaSuscripcion.DiasGraciaRestantes(Hoy.AddDays(-diasVencida), Hoy);

        Assert.Equal(esperados, dias);
    }

    [Fact]
    public void UnEstadoVencidaGuardadoNoBloqueaSiLaFechaSigueVigente()
    {
        // Vencida es un estado calculado; el valor persistido no debe mandar.
        var acceso = ReglaSuscripcion.Evaluar(
            EstadoClinica.Vencida, enPrueba: false, fechaPrimerIngreso: null,
            fechaVencimiento: Hoy.AddDays(5), Hoy);

        Assert.Equal(AccesoClinica.Permitido, acceso);
    }

    [Fact]
    public void LaPruebaVenceTreintaDiasDespuesDelPrimerIngreso()
    {
        Assert.Equal(Hoy.AddDays(30), ReglaSuscripcion.VencimientoDePrueba(Hoy));
    }

    [Fact]
    public void RenovarUnaClinicaVigenteSumaElMesAlVencimientoActual()
    {
        var vencimiento = Hoy.AddDays(10);

        var nuevo = ReglaSuscripcion.Renovar(vencimiento, Hoy);

        Assert.Equal(vencimiento.AddMonths(1), nuevo);
    }

    [Fact]
    public void RenovarUnaClinicaVencidaEmpiezaAHoy()
    {
        var nuevo = ReglaSuscripcion.Renovar(Hoy.AddDays(-20), Hoy);

        Assert.Equal(Hoy.AddMonths(1), nuevo);
    }

    [Fact]
    public void RenovarVariosMesesSumaCadaMes()
    {
        var nuevo = ReglaSuscripcion.Renovar(Hoy.AddDays(-1), Hoy, meses: 3);

        Assert.Equal(Hoy.AddMonths(3), nuevo);
    }
}
