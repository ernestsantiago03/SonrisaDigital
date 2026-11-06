using SonrisaDigital.Data.Suscripciones;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Services.HistoriasClinicas.DTOs;
using SonrisaDigital.Services.Pagos.DTOs;
using SonrisaDigital.Services.Tratamientos.DTOs;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// El negocio opera en República Dominicana. Estas pruebas fijan la conversión
/// entre la hora local de la clínica y UTC, y evitan que un servidor en otra
/// zona horaria cambie el día de un pago, una consulta o un reporte.
/// </summary>
public sealed class RelojNegocioTests
{
    [Fact]
    public void DeUtc_ConvierteAlHorarioDeRepublicaDominicana()
    {
        var utc = new DateTime(2026, 1, 15, 12, 30, 0, DateTimeKind.Utc);

        var local = RelojNegocio.DeUtc(utc);

        Assert.Equal(new DateTime(2026, 1, 15, 8, 30, 0), local);
    }

    [Fact]
    public void AUtc_InterpretaLaFechaComoHoraLocalDelNegocio()
    {
        var local = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified);

        var utc = RelojNegocio.AUtc(local);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2026, 1, 15, 4, 0, 0), utc);
    }

    [Fact]
    public void AUtcYDeUtc_ConservanLaFechaDeCalendario()
    {
        var fecha = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Unspecified);

        var ida = RelojNegocio.AUtc(fecha);
        var vuelta = RelojNegocio.DeUtc(ida);

        Assert.Equal(fecha, vuelta);
    }

    [Fact]
    public void Hoy_CoincideConLaFechaDelNegocioYConLaReglaDeSuscripcion()
    {
        var esperado = TimeZoneInfo
            .ConvertTimeFromUtc(DateTime.UtcNow, RelojNegocio.ZonaHoraria)
            .Date;

        Assert.Equal(esperado, RelojNegocio.Hoy());
        Assert.Equal(esperado, ReglaSuscripcion.Hoy());
    }

    [Fact]
    public void LosFormulariosInicianConLaFechaDelNegocio()
    {
        var hoy = RelojNegocio.Hoy();
        var hoyCalendario = RelojNegocio.HoyDateOnly();

        Assert.Equal(hoyCalendario, new RegistrarPagoDto().Fecha);
        Assert.Equal(hoyCalendario, new TratamientoRealizadoFormDto().Fecha);
        Assert.Equal(hoy, new ConsultaClinicaFormDto().Fecha);
    }
}
