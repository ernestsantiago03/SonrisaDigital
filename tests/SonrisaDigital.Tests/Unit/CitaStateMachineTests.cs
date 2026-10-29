using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Citas;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Pruebas de la máquina de estados central de las citas. La pantalla y el
/// servicio deben usar estas mismas reglas para no desincronizarse.
/// </summary>
public sealed class CitaStateMachineTests
{
    [Theory]
    [InlineData(EstadoCita.Programada, EstadoCita.Confirmada, "Citas.Editar")]
    [InlineData(EstadoCita.Programada, EstadoCita.Cancelada, "Citas.Cancelar")]
    [InlineData(EstadoCita.Programada, EstadoCita.NoAsistio, "Citas.Cancelar")]
    [InlineData(EstadoCita.Confirmada, EstadoCita.Atendida, "Citas.Atender")]
    public void DevuelveElPermisoDeCadaTransicion(
        EstadoCita actual,
        EstadoCita nuevo,
        string permisoEsperado)
    {
        Assert.Equal(permisoEsperado, CitaStateMachine.PermisoRequerido(actual, nuevo));
    }

    [Theory]
    [InlineData(EstadoCita.Programada, "Citas.Editar")]
    [InlineData(EstadoCita.Confirmada, "Citas.Editar")]
    [InlineData(EstadoCita.Atendida, "Citas.Atender")]
    [InlineData(EstadoCita.Cancelada, "Citas.Cancelar")]
    [InlineData(EstadoCita.NoAsistio, "Citas.Cancelar")]
    public void DevuelveElPermisoDeLaAccionSolicitada(
        EstadoCita nuevo,
        string permisoEsperado)
    {
        Assert.Equal(permisoEsperado, CitaStateMachine.PermisoRequeridoParaEstado(nuevo));
    }

    [Theory]
    [InlineData(EstadoCita.Programada, EstadoCita.Atendida)]
    [InlineData(EstadoCita.Confirmada, EstadoCita.Programada)]
    [InlineData(EstadoCita.Confirmada, EstadoCita.Cancelada)]
    [InlineData(EstadoCita.Confirmada, EstadoCita.NoAsistio)]
    [InlineData(EstadoCita.Atendida, EstadoCita.Programada)]
    [InlineData(EstadoCita.Cancelada, EstadoCita.Atendida)]
    [InlineData(EstadoCita.NoAsistio, EstadoCita.Confirmada)]
    public void RechazaTransicionesQueNoEstanEnElFlujo(EstadoCita actual, EstadoCita nuevo)
    {
        Assert.False(CitaStateMachine.EsTransicionPermitida(actual, nuevo));
        Assert.Throws<InvalidOperationException>(
            () => CitaStateMachine.ValidarTransicion(actual, nuevo));
    }

    [Theory]
    [InlineData(EstadoCita.Atendida)]
    [InlineData(EstadoCita.Cancelada)]
    [InlineData(EstadoCita.NoAsistio)]
    public void ReconoceLosEstadosFinales(EstadoCita estado)
    {
        Assert.True(CitaStateMachine.EsEstadoFinal(estado));
    }

    [Theory]
    [InlineData(EstadoCita.Programada)]
    [InlineData(EstadoCita.Confirmada)]
    public void ReconoceLosEstadosAbiertos(EstadoCita estado)
    {
        Assert.False(CitaStateMachine.EsEstadoFinal(estado));
    }

    [Fact]
    public void PermiteMantenerElMismoEstado()
    {
        Assert.True(CitaStateMachine.EsTransicionPermitida(
            EstadoCita.Programada,
            EstadoCita.Programada));
    }

    [Fact]
    public void ListaLasTransicionesPermitidasDeProgramada()
    {
        Assert.Equal(
            [EstadoCita.Confirmada, EstadoCita.Cancelada, EstadoCita.NoAsistio],
            CitaStateMachine.TransicionesPermitidas(EstadoCita.Programada));
    }

    [Fact]
    public void ListaLasTransicionesPermitidasDeConfirmada()
    {
        Assert.Equal(
            [EstadoCita.Atendida],
            CitaStateMachine.TransicionesPermitidas(EstadoCita.Confirmada));
    }

    [Theory]
    [InlineData(EstadoCita.Atendida)]
    [InlineData(EstadoCita.Cancelada)]
    [InlineData(EstadoCita.NoAsistio)]
    public void LosEstadosFinalesNoTienenTransiciones(EstadoCita estado)
    {
        Assert.Empty(CitaStateMachine.TransicionesPermitidas(estado));
    }
}
