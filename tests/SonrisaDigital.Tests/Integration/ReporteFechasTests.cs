using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Un pago se guarda como medianoche local de su día. El reporte de ese día tiene que incluirlo.
/// </summary>
public sealed class ReporteFechasTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task UnPagoDeMedianocheAparecEnSuDiaDelReporte()
    {
        var dia = new DateTime(2035, 7, 14);
        var pagoId = await CrearPagoAsync(dia);

        await using var db = fixture.CreateContext();
        var repositorio = new ReporteRepository(db);

        var delDia = await repositorio.GetPagosAsync(dia, dia.AddDays(1));
        var deOtroDia = await repositorio.GetPagosAsync(dia.AddDays(1), dia.AddDays(2));

        Assert.Contains(delDia, pago => pago.Id == pagoId);
        Assert.DoesNotContain(deOtroDia, pago => pago.Id == pagoId);
    }

    private async Task<int> CrearPagoAsync(DateTime fecha)
    {
        await using var db = fixture.CreateContext();
        var usuario = await TestData.AsegurarUsuarioAsync(db);
        var paciente = await TestData.CrearPacienteAsync(db);
        var odontologo = await TestData.CrearOdontologoAsync(db);
        var catalogo = await TestData.CrearTratamientoCatalogoAsync(db);
        var realizado = await TestData.CrearTratamientoRealizadoAsync(
            db, paciente.Id, odontologo.Id, catalogo.Id, precio: 100m);

        var pago = new Pago
        {
            TratamientoRealizadoId = realizado.Id,
            Fecha = fecha,
            Monto = 25m,
            MetodoPago = MetodoPago.Efectivo,
            UsuarioId = usuario.Id
        };
        db.Pagos.Add(pago);
        await db.SaveChangesAsync();
        return pago.Id;
    }
}
