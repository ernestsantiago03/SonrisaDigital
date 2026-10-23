using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Reportes.DTOs;

namespace SonrisaDigital.Services.Reportes.Pdf;

/// <summary>
/// Genera los PDF de cada pestaña de Reportes con QuestPDF. El documento incluye
/// membrete de la clínica, período y filtros aplicados, la tabla del reporte, un
/// bloque de totales y pie con autor, fecha y numeración de páginas.
///
/// Los datos y permisos se resuelven con IReporteService: si la sesión no puede
/// consultar el reporte, este service no genera nada.
/// </summary>
public sealed class PdfReportService(
    IReporteService reporteService,
    ICurrentUserService currentUserService) : IPdfReportService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-DO");

    static PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerarCitasPdfAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var citas = await reporteService.GetCitasAsync(filtro, cancellationToken);
        return await ConstruirPdfAsync("Reporte de citas", filtro,
            ["Fecha", "Hora", "Paciente", "Odontólogo", "Estado", "Motivo"],
            [1.2f, 1.4f, 3f, 2.5f, 1.6f, 3f],
            FilasCitas(citas), ResumenCitas(citas), cancellationToken);
    }

    public async Task<byte[]> GenerarPacientesPdfAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var pacientes = await reporteService.GetPacientesAsync(filtro, cancellationToken);
        return await ConstruirPdfAsync("Reporte de pacientes atendidos", filtro,
            ["Paciente", "Identificación", "Teléfono", "Registro", "Citas", "Última cita"],
            [3f, 1.8f, 1.6f, 1.4f, 1f, 1.4f],
            FilasPacientes(pacientes), ResumenPacientes(pacientes), cancellationToken);
    }

    public async Task<byte[]> GenerarTratamientosPdfAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var tratamientos = await reporteService.GetTratamientosAsync(filtro, cancellationToken);
        return await ConstruirPdfAsync("Reporte de tratamientos realizados", filtro,
            ["Fecha", "Paciente", "Tratamiento", "Odontólogo", "Precio", "Pagado", "Saldo", "Estado"],
            [1.2f, 2.4f, 2.4f, 2.2f, 1.1f, 1.1f, 1.1f, 1.4f],
            FilasTratamientos(tratamientos), ResumenTratamientos(tratamientos), cancellationToken);
    }

    public async Task<byte[]> GenerarPagosPdfAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var pagos = await reporteService.GetPagosAsync(filtro, cancellationToken);
        return await ConstruirPdfAsync("Reporte de pagos", filtro,
            ["Fecha", "Paciente", "Tratamiento", "Método", "Monto", "Registrado por"],
            [1.6f, 2.6f, 2.6f, 1.4f, 1.2f, 2.4f],
            FilasPagos(pagos), ResumenPagos(pagos), cancellationToken);
    }

    private async Task<byte[]> ConstruirPdfAsync(
        string titulo,
        ReporteFiltroDto filtro,
        string[] columnas,
        float[] anchos,
        List<List<string>> filas,
        List<string> resumen,
        CancellationToken cancellationToken)
    {
        var filtros = await DescribirFiltrosAsync(filtro, cancellationToken);
        var generadoPor = string.IsNullOrWhiteSpace(currentUserService.Nombre)
            ? "Sistema"
            : currentUserService.Nombre!;
        var generadoEn = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(estilo => estilo.FontFamily("Segoe UI").FontSize(9));

                page.Header().Column(membrete =>
                {
                    membrete.Item().Row(cabecera =>
                    {
                        cabecera.RelativeItem().Column(tituloBloque =>
                        {
                            tituloBloque.Item().Text(titulo).FontColor("#1d4ed8").FontSize(16).Bold();
                            tituloBloque.Item().PaddingTop(2).Text(filtros).FontColor("#64748b").FontSize(9);
                        });

                        cabecera.AutoItem().Column(logotipo =>
                        {
                            logotipo.Item().AlignRight().Text("SONRISA DIGITAL")
                                .FontColor("#0f172a").FontSize(11).Bold();
                            logotipo.Item().AlignRight().Text("Reportes administrativos y clínicos")
                                .FontColor("#94a3b8").FontSize(8);
                        });
                    });

                    membrete.Item().PaddingTop(10).PaddingBottom(6)
                        .LineHorizontal(1).LineColor("#dbe4ee");
                });

                page.Content().PaddingVertical(6).Column(contenido =>
                {
                    foreach (var linea in resumen)
                    {
                        contenido.Item().PaddingVertical(1).Text(linea).FontColor("#475569").FontSize(9);
                    }

                    contenido.Item().PaddingTop(8).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(definicion =>
                        {
                            foreach (var ancho in anchos)
                            {
                                definicion.RelativeColumn(ancho);
                            }
                        });

                        tabla.Header(cabecera =>
                        {
                            foreach (var texto in columnas)
                            {
                                cabecera.Cell().Text(texto).FontColor("#475569").FontSize(8).Bold();
                            }
                        });

                        foreach (var fila in filas)
                        {
                            foreach (var texto in fila)
                            {
                                tabla.Cell().Text(texto).FontSize(8);
                            }
                        }
                    });

                    if (filas.Count == 0)
                    {
                        contenido.Item().PaddingTop(10)
                            .Text("No hay registros para el período y filtros seleccionados.")
                            .FontColor("#64748b").FontSize(10);
                    }
                });

                page.Footer().Row(pie =>
                {
                    pie.RelativeItem().AlignLeft().Text($"Generado por {generadoPor} · {generadoEn}")
                        .FontColor("#94a3b8").FontSize(8);

                    pie.AutoItem().AlignRight().Text("Página {0}")
                        .FontColor("#94a3b8").FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    private async Task<string> DescribirFiltrosAsync(
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken)
    {
        var partes = new List<string>
        {
            $"Período: {filtro.Desde.ToString("dd/MM/yyyy")} — {filtro.Hasta.ToString("dd/MM/yyyy")}"
        };

        if (filtro.EstadoCitaFiltro is not null)
        {
            partes.Add($"Estado de cita: {TextoEstadoCita(filtro.EstadoCitaFiltro.Value)}");
        }

        if (filtro.EstadoTratamientoFiltro is not null)
        {
            partes.Add($"Estado de tratamiento: {TextoEstadoTratamiento(filtro.EstadoTratamientoFiltro.Value)}");
        }

        if (filtro.MetodoPagoFiltro is not null)
        {
            partes.Add($"Método de pago: {filtro.MetodoPagoFiltro.Value}");
        }

        if (filtro.OdontologoId is not null)
        {
            var opciones = await reporteService.GetOpcionesAsync(cancellationToken);
            var nombre = opciones.Odontologos
                .FirstOrDefault(o => o.Id == filtro.OdontologoId.Value)?.Nombre;

            partes.Add($"Odontólogo: {nombre ?? filtro.OdontologoId.Value.ToString()}");
        }

        return string.Join("  ·  ", partes);
    }

    private static List<List<string>> FilasCitas(IReadOnlyList<ReporteCitaItemDto> citas)
        => citas.Select(c => new List<string>
        {
            c.Fecha.ToString("dd/MM/yyyy"),
            $"{c.HoraInicio:HH:mm} - {c.HoraFin:HH:mm}",
            c.Paciente,
            c.Odontologo,
            TextoEstadoCita(c.Estado),
            c.Motivo ?? "—"
        }).ToList();

    private static List<string> ResumenCitas(IReadOnlyList<ReporteCitaItemDto> citas)
        => new()
        {
            $"{citas.Count} cita(s) en el período",
            $"Atendidas: {citas.Count(c => c.Estado == EstadoCita.Atendida)} · Programadas: {citas.Count(c => c.Estado == EstadoCita.Programada)} · Confirmadas: {citas.Count(c => c.Estado == EstadoCita.Confirmada)} · Canceladas: {citas.Count(c => c.Estado == EstadoCita.Cancelada)} · No asistió: {citas.Count(c => c.Estado == EstadoCita.NoAsistio)}"
        };

    private static List<List<string>> FilasPacientes(IReadOnlyList<ReportePacienteItemDto> pacientes)
        => pacientes.Select(p => new List<string>
        {
            p.Nombre,
            string.IsNullOrWhiteSpace(p.Identificacion) ? "—" : p.Identificacion,
            string.IsNullOrWhiteSpace(p.Telefono) ? "—" : p.Telefono,
            p.FechaRegistro.ToLocalTime().ToString("dd/MM/yyyy"),
            p.CitasAtendidas.ToString(Cultura),
            p.UltimaCita.ToString("dd/MM/yyyy")
        }).ToList();

    private static List<string> ResumenPacientes(IReadOnlyList<ReportePacienteItemDto> pacientes)
        => new()
        {
            $"{pacientes.Count} paciente(s) atendido(s) en el período",
            $"Citas atendidas del período: {pacientes.Sum(p => p.CitasAtendidas)}"
        };

    private static List<List<string>> FilasTratamientos(IReadOnlyList<ReporteTratamientoItemDto> tratamientos)
        => tratamientos.Select(t => new List<string>
        {
            t.Fecha.ToLocalTime().ToString("dd/MM/yyyy"),
            t.Paciente,
            t.Tratamiento,
            t.Odontologo,
            FormatoDinero(t.Precio),
            FormatoDinero(t.Pagado),
            FormatoDinero(t.Saldo),
            TextoEstadoTratamiento(t.Estado)
        }).ToList();

    private static List<string> ResumenTratamientos(IReadOnlyList<ReporteTratamientoItemDto> tratamientos)
        => new()
        {
            $"{tratamientos.Count} tratamiento(s) en el período",
            $"Facturado: {FormatoDinero(tratamientos.Sum(t => t.Precio))} · Pagado: {FormatoDinero(tratamientos.Sum(t => t.Pagado))} · Saldo: {FormatoDinero(tratamientos.Sum(t => t.Saldo))}"
        };

    private static List<List<string>> FilasPagos(IReadOnlyList<ReportePagoItemDto> pagos)
        => pagos.Select(p => new List<string>
        {
            p.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            p.Paciente,
            p.Tratamiento,
            p.Metodo.ToString(),
            FormatoDinero(p.Monto),
            p.RegistradoPor
        }).ToList();

    private static List<string> ResumenPagos(IReadOnlyList<ReportePagoItemDto> pagos)
        => new()
        {
            $"{pagos.Count} pago(s) en el período",
            $"Total cobrado: {FormatoDinero(pagos.Sum(p => p.Monto))}"
        };

    private static string TextoEstadoCita(EstadoCita estado) => estado switch
    {
        EstadoCita.Programada => "Programada",
        EstadoCita.Confirmada => "Confirmada",
        EstadoCita.Atendida => "Atendida",
        EstadoCita.Cancelada => "Cancelada",
        EstadoCita.NoAsistio => "No asistió",
        _ => estado.ToString()
    };

    private static string TextoEstadoTratamiento(EstadoTratamiento estado) => estado switch
    {
        EstadoTratamiento.EnProceso => "En proceso",
        EstadoTratamiento.Completado => "Completado",
        EstadoTratamiento.Cancelado => "Cancelado",
        _ => estado.ToString()
    };

    private static string FormatoDinero(decimal valor)
    {
        var texto = valor == decimal.Truncate(valor)
            ? valor.ToString("N0", Cultura)
            : valor.ToString("N2", Cultura);
        return $"RD${texto}";
    }
}