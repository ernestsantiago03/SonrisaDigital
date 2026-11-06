using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Reportes;
using SonrisaDigital.Services.Reportes.DTOs;

namespace SonrisaDigital.Tests.Reportes;

public class ReportePdfSmokeTests
{
    [Theory]
    [InlineData(TipoReportePdf.Citas)]
    [InlineData(TipoReportePdf.Pacientes)]
    [InlineData(TipoReportePdf.Tratamientos)]
    [InlineData(TipoReportePdf.Pagos)]
    public async Task GenerarAsync_DevuelveUnPdfValido(TipoReportePdf tipo)
    {
        var usuario = new CurrentUserService();
        usuario.Establecer("1", "Prueba");
        var servicio = new ReportePdfService(new ReporteFalso(), usuario);
        var filtro = new ReporteFiltroDto(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        var pdf = await servicio.GenerarAsync(tipo, filtro);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private sealed class ReporteFalso : IReporteService
    {
        public Task<ReporteOpcionesDto> GetOpcionesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReporteOpcionesDto([], PuedeFiltrarOdontologos: false, PuedeVerPagos: true));

        public Task<ReporteResumenDto> GetResumenAsync(ReporteFiltroDto filtro, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReporteResumenDto(1, 1, 0, 0, 0, 0, 1, 1, 0, 1, 1, 1, 0, 1, 0, 100m,
                new ReportePagosResumenDto(0m, 0, 0m, 0, [])));

        public Task<IReadOnlyList<ReporteCitaItemDto>> GetCitasAsync(ReporteFiltroDto filtro, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReporteCitaItemDto>>(
            [
                new ReporteCitaItemDto(1, new DateOnly(2026, 11, 2), new TimeOnly(8, 0), new TimeOnly(8, 30), "Paciente Prueba", "Odontólogo Prueba", EstadoCita.Programada, "Limpieza")
            ]);

        public Task<IReadOnlyList<ReportePacienteItemDto>> GetPacientesAsync(ReporteFiltroDto filtro, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReportePacienteItemDto>>([]);

        public Task<IReadOnlyList<ReporteTratamientoItemDto>> GetTratamientosAsync(ReporteFiltroDto filtro, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReporteTratamientoItemDto>>([]);

        public Task<IReadOnlyList<ReportePagoItemDto>> GetPagosAsync(ReporteFiltroDto filtro, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReportePagoItemDto>>([]);
    }
}
