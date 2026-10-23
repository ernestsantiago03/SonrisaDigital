using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Reportes.DTOs;

namespace SonrisaDigital.Services.Reportes;

/// <summary>
/// Genera los informes PDF con QuestPDF. La primera página es un resumen
/// ejecutivo y las siguientes contienen el anexo de detalle. El documento se
/// arma en el servidor para conservar la autorización y el alcance del Service.
/// </summary>
public sealed class ReportePdfService(
    IReporteService reporteService,
    ICurrentUserService currentUserService) : IReportePdfService
{
    // Paleta del informe. Se mantiene fuera de los métodos para que todas las
    // secciones del PDF usen exactamente los mismos colores.
    private const string Navy = "#102A43";
    private const string Blue = "#1D4ED8";
    private const string Teal = "#0F766E";
    private const string Green = "#15803D";
    private const string Amber = "#B45309";
    private const string Red = "#B91C1C";
    private const string Muted = "#64748B";
    private const string Border = "#D8E1EA";
    private const string Soft = "#F5F8FB";
    private const string White = "#FFFFFF";

    private static readonly CultureInfo Cultura = new("es-DO");

    /// <summary>
    /// QuestPDF exige indicar la licencia antes de generar el primer documento.
    /// Community aplica a organizaciones con ingresos anuales menores a USD 1M.
    /// </summary>
    static ReportePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerarAsync(
        TipoReportePdf tipo,
        ReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        // Se consultan las opciones para mostrar el nombre del odontólogo en la
        // ficha y el resumen para alimentar las tarjetas y los gráficos.
        var opciones = await reporteService.GetOpcionesAsync(cancellationToken);
        var resumen = await reporteService.GetResumenAsync(filtro, cancellationToken);
        var contenido = await ConstruirContenidoAsync(tipo, filtro, resumen, cancellationToken);

        var contexto = new ReportePdfContexto(
            Periodo: $"{filtro.Desde:dd/MM/yyyy} – {filtro.Hasta:dd/MM/yyyy}",
            Usuario: string.IsNullOrWhiteSpace(currentUserService.Nombre)
                ? currentUserService.Principal?.Identity?.Name ?? "Usuario"
                : currentUserService.Nombre,
            Generado: DateTime.Now.ToString("dd/MM/yyyy HH:mm", Cultura),
            Odontologo: ResolverOdontologo(filtro, opciones),
            Estado: ObtenerEstadoFiltro(tipo, filtro),
            Metodo: tipo == TipoReportePdf.Pagos
                ? filtro.MetodoPagoFiltro?.ToString()
                : null,
            Registros: contenido.Filas.Count);

        // El armado del documento es trabajo de CPU; se ejecuta fuera del hilo
        // del circuito para no bloquear la interfaz mientras se pagina la tabla.
        return await Task.Run(
            () => Document.Create(document =>
            {
                document.Page(page =>
                {
                    // Tratamientos necesita columnas más anchas; el resto de
                    // reportes se presenta en A4 vertical.
                    page.Size(tipo == TipoReportePdf.Tratamientos
                        ? PageSizes.A4.Landscape()
                        : PageSizes.A4);
                    page.Margin(26);
                    page.DefaultTextStyle(estilo => estilo
                        .FontSize(7.5f)
                        .FontColor(Navy));

                    page.Header().Element(encabezado =>
                        ComponerEncabezado(encabezado, contenido, contexto));

                    page.Content().Element(contenidoPagina =>
                        ComponerContenido(contenidoPagina, contenido, contexto));

                    page.Footer().Element(pie =>
                        ComponerPie(pie, contenido, contexto));
                });
            }).GeneratePdf(),
            cancellationToken);
    }

    /// <summary>Arma las tarjetas, las barras, las columnas y las filas de cada reporte.</summary>
    private async Task<ReportePdfContenido> ConstruirContenidoAsync(
        TipoReportePdf tipo,
        ReporteFiltroDto filtro,
        ReporteResumenDto resumen,
        CancellationToken cancellationToken)
    {
        return tipo switch
        {
            TipoReportePdf.Citas => await CrearCitasAsync(filtro, resumen, cancellationToken),
            TipoReportePdf.Pacientes => await CrearPacientesAsync(filtro, resumen, cancellationToken),
            TipoReportePdf.Tratamientos => await CrearTratamientosAsync(filtro, resumen, cancellationToken),
            TipoReportePdf.Pagos => await CrearPagosAsync(filtro, resumen, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de reporte no soportado.")
        };
    }

    private async Task<ReportePdfContenido> CrearCitasAsync(
        ReporteFiltroDto filtro,
        ReporteResumenDto resumen,
        CancellationToken cancellationToken)
    {
        var citas = await reporteService.GetCitasAsync(filtro, cancellationToken);

        var kpis = new List<KpiPdf>
        {
            new("Total de citas", resumen.CitasTotal.ToString(Cultura), "Agenda del período", Blue),
            new("Atendidas", resumen.CitasAtendidas.ToString(Cultura), "Citas completadas", Green),
            new("Confirmadas", resumen.CitasConfirmadas.ToString(Cultura), "Pendientes de atención", Teal),
            new("Canceladas", resumen.CitasCanceladas.ToString(Cultura), "Canceladas por el paciente o la clínica", Red),
            new("No asistió", resumen.CitasNoAsistio.ToString(Cultura), "Ausencias registradas", Amber),
            new("Pacientes atendidos", resumen.PacientesAtendidos.ToString(Cultura), "Pacientes únicos", Navy)
        };

        var barras = new List<BarraPdf>();
        AgregarBarra(barras, "Programadas", resumen.CitasProgramadas, resumen.CitasTotal, Blue);
        AgregarBarra(barras, "Confirmadas", resumen.CitasConfirmadas, resumen.CitasTotal, Teal);
        AgregarBarra(barras, "Atendidas", resumen.CitasAtendidas, resumen.CitasTotal, Green);
        AgregarBarra(barras, "Canceladas", resumen.CitasCanceladas, resumen.CitasTotal, Red);
        AgregarBarra(barras, "No asistió", resumen.CitasNoAsistio, resumen.CitasTotal, Amber);

        var puntosClave = new List<string>
        {
            $"{resumen.CitasAtendidas} de {resumen.CitasTotal} citas fueron atendidas ({Porcentaje(resumen.CitasAtendidas, resumen.CitasTotal)}).",
            $"{resumen.CitasConfirmadas} citas confirmadas quedan pendientes de atención.",
            $"{resumen.CitasCanceladas + resumen.CitasNoAsistio} citas se cancelaron o no asistió."
        };

        var columnas = new List<ColumnaPdf>
        {
            new("Fecha", 1.0f),
            new("Hora", 1.2f),
            new("Paciente", 1.8f),
            new("Odontólogo", 1.6f),
            new("Estado", 1.1f),
            new("Motivo", 2.0f)
        };

        var filas = citas
            .Select(cita => (IReadOnlyList<string>)
            [
                cita.Fecha.ToString("dd/MM/yyyy", Cultura),
                $"{cita.HoraInicio:HH:mm} - {cita.HoraFin:HH:mm}",
                cita.Paciente,
                cita.Odontologo,
                EstadoCitaTexto(cita.Estado),
                string.IsNullOrWhiteSpace(cita.Motivo) ? "—" : cita.Motivo
            ])
            .ToList();

        return new ReportePdfContenido(
            Titulo: "Reporte de citas",
            Subtitulo: "Actividad de la agenda en el período seleccionado.",
            Kpis: kpis,
            Barras: barras,
            PuntosClave: puntosClave,
            Columnas: columnas,
            Filas: filas,
            Nota: "El anexo incluye las citas del período con los filtros de estado y odontólogo aplicados. Un paciente se cuenta una sola vez en el indicador de pacientes atendidos.");
    }

    private async Task<ReportePdfContenido> CrearPacientesAsync(
        ReporteFiltroDto filtro,
        ReporteResumenDto resumen,
        CancellationToken cancellationToken)
    {
        var pacientes = await reporteService.GetPacientesAsync(filtro, cancellationToken);

        var kpis = new List<KpiPdf>
        {
            new("Pacientes registrados", resumen.PacientesTotal.ToString(Cultura), "Activos e inactivos", Blue),
            new("Activos", resumen.PacientesActivos.ToString(Cultura), "Estado actual del registro", Green),
            new("Inactivos", resumen.PacientesInactivos.ToString(Cultura), "Estado actual del registro", Muted),
            new("Nuevos", resumen.PacientesNuevos.ToString(Cultura), "Registrados en el período", Teal),
            new("Atendidos", resumen.PacientesAtendidos.ToString(Cultura), "Con al menos una cita atendida", Navy)
        };

        var barras = new List<BarraPdf>();
        AgregarBarra(barras, "Activos", resumen.PacientesActivos, resumen.PacientesTotal, Green);
        AgregarBarra(barras, "Inactivos", resumen.PacientesInactivos, resumen.PacientesTotal, Muted);
        AgregarBarra(barras, "Nuevos", resumen.PacientesNuevos, resumen.PacientesTotal, Teal);
        AgregarBarra(barras, "Atendidos", resumen.PacientesAtendidos, resumen.PacientesTotal, Blue);

        var puntosClave = new List<string>
        {
            $"{resumen.PacientesActivos} de {resumen.PacientesTotal} pacientes están activos ({Porcentaje(resumen.PacientesActivos, resumen.PacientesTotal)}).",
            $"{resumen.PacientesNuevos} pacientes nuevos se registraron en el período.",
            $"{resumen.PacientesAtendidos} pacientes tuvieron al menos una cita atendida."
        };

        var columnas = new List<ColumnaPdf>
        {
            new("Paciente", 1.9f),
            new("Identificación", 1.3f),
            new("Teléfono", 1.2f),
            new("Registro", 1.1f),
            new("Citas atendidas", 1.0f, Derecha: true),
            new("Última cita", 1.1f)
        };

        var filas = pacientes
            .Select(paciente => (IReadOnlyList<string>)
            [
                paciente.Nombre,
                string.IsNullOrWhiteSpace(paciente.Identificacion) ? "—" : paciente.Identificacion,
                string.IsNullOrWhiteSpace(paciente.Telefono) ? "—" : paciente.Telefono,
                paciente.FechaRegistro.ToLocalTime().ToString("dd/MM/yyyy", Cultura),
                paciente.CitasAtendidas.ToString(Cultura),
                paciente.UltimaCita.ToString("dd/MM/yyyy", Cultura)
            ])
            .ToList();

        return new ReportePdfContenido(
            Titulo: "Reporte de pacientes",
            Subtitulo: "Pacientes atendidos y composición del registro en el período.",
            Kpis: kpis,
            Barras: barras,
            PuntosClave: puntosClave,
            Columnas: columnas,
            Filas: filas,
            Nota: "La tabla muestra pacientes únicos con al menos una cita en estado Atendida dentro del período. Nuevos se refiere a registros creados en el período, por lo que puede solaparse con los demás indicadores.");
    }

    private async Task<ReportePdfContenido> CrearTratamientosAsync(
        ReporteFiltroDto filtro,
        ReporteResumenDto resumen,
        CancellationToken cancellationToken)
    {
        var tratamientos = await reporteService.GetTratamientosAsync(filtro, cancellationToken);
        var saldoPeriodo = tratamientos.Sum(tratamiento => tratamiento.Saldo);

        var kpis = new List<KpiPdf>
        {
            new("Total de tratamientos", resumen.TratamientosTotal.ToString(Cultura), "Realizados en el período", Blue),
            new("Completados", resumen.TratamientosCompletados.ToString(Cultura), "Tratamientos finalizados", Green),
            new("En proceso", resumen.TratamientosEnProceso.ToString(Cultura), "Tratamientos activos", Teal),
            new("Cancelados", resumen.TratamientosCancelados.ToString(Cultura), "Tratamientos cancelados", Red),
            new("Facturado", Money(resumen.TratamientosFacturado), "Valor de los tratamientos", Navy),
            new("Saldo del período", Money(saldoPeriodo), "Precio menos pagos registrados", Amber)
        };

        var barras = new List<BarraPdf>();
        AgregarBarra(barras, "En proceso", resumen.TratamientosEnProceso, resumen.TratamientosTotal, Teal);
        AgregarBarra(barras, "Completados", resumen.TratamientosCompletados, resumen.TratamientosTotal, Green);
        AgregarBarra(barras, "Cancelados", resumen.TratamientosCancelados, resumen.TratamientosTotal, Red);

        var puntosClave = new List<string>
        {
            $"{resumen.TratamientosCompletados} de {resumen.TratamientosTotal} tratamientos están completados ({Porcentaje(resumen.TratamientosCompletados, resumen.TratamientosTotal)}).",
            $"{resumen.TratamientosEnProceso} tratamientos siguen en proceso.",
            $"El saldo del período asciende a {Money(saldoPeriodo)}."
        };

        var columnas = new List<ColumnaPdf>
        {
            new("Fecha", 0.9f),
            new("Paciente", 1.5f),
            new("Tratamiento", 1.9f),
            new("Odontólogo", 1.4f),
            new("Precio", 1.0f, Derecha: true),
            new("Pagado", 1.0f, Derecha: true),
            new("Saldo", 1.0f, Derecha: true),
            new("Estado", 1.0f)
        };

        var filas = tratamientos
            .Select(tratamiento => (IReadOnlyList<string>)
            [
                tratamiento.Fecha.ToLocalTime().ToString("dd/MM/yyyy", Cultura),
                tratamiento.Paciente,
                tratamiento.Tratamiento,
                tratamiento.Odontologo,
                Money(tratamiento.Precio),
                Money(tratamiento.Pagado),
                Money(tratamiento.Saldo),
                EstadoTratamientoTexto(tratamiento.Estado)
            ])
            .ToList();

        return new ReportePdfContenido(
            Titulo: "Reporte de tratamientos",
            Subtitulo: "Tratamientos realizados y estado de cobro en el período.",
            Kpis: kpis,
            Barras: barras,
            PuntosClave: puntosClave,
            Columnas: columnas,
            Filas: filas,
            Nota: "El saldo del período se calcula como el precio del tratamiento menos los pagos registrados. El saldo pendiente global de la clínica se muestra en el reporte de pagos.");
    }

    private async Task<ReportePdfContenido> CrearPagosAsync(
        ReporteFiltroDto filtro,
        ReporteResumenDto resumen,
        CancellationToken cancellationToken)
    {
        var pagos = await reporteService.GetPagosAsync(filtro, cancellationToken);

        if (resumen.Pagos is null)
        {
            throw new InvalidOperationException(
                "No tienes permiso para consultar la información financiera de los pagos.");
        }

        var ticketPromedio = resumen.Pagos.CantidadPagos == 0
            ? 0m
            : resumen.Pagos.TotalCobrado / resumen.Pagos.CantidadPagos;

        var kpis = new List<KpiPdf>
        {
            new("Total cobrado", Money(resumen.Pagos.TotalCobrado), "Pagos del período", Green),
            new("Pagos registrados", resumen.Pagos.CantidadPagos.ToString(Cultura), "Operaciones del período", Blue),
            new("Ticket promedio", Money(ticketPromedio), "Promedio por pago", Teal),
            new("Saldo pendiente", Money(resumen.Pagos.SaldoPendiente), "Saldo actual de la clínica", Amber),
            new("Tratamientos con saldo", resumen.Pagos.TratamientosConSaldo.ToString(Cultura), "Al momento de generar el informe", Red)
        };

        var barras = resumen.Pagos.PorMetodo
            .Select(metodo =>
            {
                var porcentaje = resumen.Pagos.TotalCobrado <= 0
                    ? 0
                    : (double)(metodo.Total / resumen.Pagos.TotalCobrado * 100);
                return new BarraPdf(
                    MetodoPagoTexto(metodo.Metodo),
                    $"{Money(metodo.Total)} · {porcentaje:0}%",
                    porcentaje,
                    ColorMetodo(metodo.Metodo));
            })
            .ToList();

        var puntosClave = new List<string>
        {
            $"Se cobraron {Money(resumen.Pagos.TotalCobrado)} en {resumen.Pagos.CantidadPagos} pagos.",
            $"El ticket promedio fue de {Money(ticketPromedio)}.",
            $"Hay {resumen.Pagos.TratamientosConSaldo} tratamientos con saldo pendiente por {Money(resumen.Pagos.SaldoPendiente)}."
        };

        var columnas = new List<ColumnaPdf>
        {
            new("Fecha", 1.1f),
            new("Paciente", 1.8f),
            new("Tratamiento", 1.9f),
            new("Método", 1.0f),
            new("Monto", 1.0f, Derecha: true),
            new("Registrado por", 1.3f)
        };

        var filas = pagos
            .Select(pago => (IReadOnlyList<string>)
            [
                pago.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura),
                pago.Paciente,
                pago.Tratamiento,
                MetodoPagoTexto(pago.Metodo),
                Money(pago.Monto),
                pago.RegistradoPor
            ])
            .ToList();

        return new ReportePdfContenido(
            Titulo: "Reporte de pagos",
            Subtitulo: "Cobros registrados y distribución por método de pago.",
            Kpis: kpis,
            Barras: barras,
            PuntosClave: puntosClave,
            Columnas: columnas,
            Filas: filas,
            Nota: "El saldo pendiente corresponde a todos los tratamientos con saldo al momento de generar el informe, no únicamente a los tratamientos del período seleccionado.");
    }

    /// <summary>Encabezado institucional que se repite en todas las páginas.</summary>
    private static void ComponerEncabezado(
        IContainer container,
        ReportePdfContenido contenido,
        ReportePdfContexto contexto)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                // Monograma institucional. Evita depender de un archivo de imagen
                // externo y mantiene el PDF autocontenido.
                row.ConstantItem(30)
                    .Height(30)
                    .Background(Navy)
                    .AlignCenter()
                    .AlignMiddle()
                    .Text("SD")
                    .FontSize(10)
                    .Bold()
                    .FontColor(White);

                row.ConstantItem(10);

                row.RelativeItem().Column(marca =>
                {
                    marca.Item().Text("Sonrisa Digital")
                        .FontSize(11)
                        .Bold()
                        .FontColor(Navy);
                    marca.Item().Text("Clínica odontológica")
                        .FontSize(6.5f)
                        .FontColor(Muted);
                });

                row.ConstantItem(190).AlignRight().Column(titulo =>
                {
                    titulo.Item().Text(contenido.Titulo)
                        .FontSize(8)
                        .Bold()
                        .FontColor(Navy);
                    titulo.Item().Text(contexto.Periodo)
                        .FontSize(7)
                        .FontColor(Muted);
                });
            });

            column.Item().PaddingTop(10).Height(1).Background(Border);
        });
    }

    /// <summary>
    /// Primera página ejecutiva y, después de un salto, el anexo con la tabla
    /// detallada. La tabla repite su encabezado en cada página automáticamente.
    /// </summary>
    private static void ComponerContenido(
        IContainer container,
        ReportePdfContenido contenido,
        ReportePdfContexto contexto)
    {
        container.Column(column =>
        {
            column.Item().PaddingTop(8).Text("Resumen ejecutivo")
                .FontSize(17)
                .Bold()
                .FontColor(Navy);
            column.Item().Text(contenido.Subtitulo)
                .FontSize(8)
                .FontColor(Muted);

            column.Item().PaddingTop(12).Element(kpis =>
                ComponerKpis(kpis, contenido.Kpis));

            column.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem(1.15f).Element(grafico =>
                    ComponerGrafico(grafico, contenido.Barras));
                row.ConstantItem(10);
                row.RelativeItem(0.85f).Element(ficha =>
                    ComponerFicha(ficha, contenido, contexto));
            });

            column.Item().PaddingTop(14).Element(nota =>
                ComponerNota(nota, contenido.Nota));

            column.Item().PaddingTop(12).Element(puntos =>
                ComponerPuntosClave(puntos, contenido.PuntosClave));

            // El anexo comienza en una página nueva para que la primera quede
            // dedicada exclusivamente a la lectura ejecutiva.
            column.Item().PageBreak();

            column.Item().Text("Anexo · Detalle")
                .FontSize(14)
                .Bold()
                .FontColor(Navy);
            column.Item().Text($"{contexto.Registros} registro(s) · {contexto.Periodo}")
                .FontSize(7.5f)
                .FontColor(Muted);
            column.Item().PaddingTop(8).Element(tabla =>
                ComponerTabla(tabla, contenido));
        });
    }

    /// <summary>Tarjetas de indicadores en filas de tres columnas.</summary>
    private static void ComponerKpis(IContainer container, IReadOnlyList<KpiPdf> kpis)
    {
        container.Column(column =>
        {
            foreach (var grupo in kpis.Chunk(3))
            {
                column.Item().PaddingBottom(6).Row(row =>
                {
                    row.Spacing(6);
                    foreach (var kpi in grupo)
                    {
                        row.RelativeItem().Element(tarjeta =>
                            ComponerKpi(tarjeta, kpi));
                    }

                    // Mantiene el ancho de las tarjetas cuando la última fila
                    // tiene menos de tres elementos.
                    for (var i = grupo.Length; i < 3; i++)
                    {
                        row.RelativeItem();
                    }
                });
            }
        });
    }

    private static void ComponerKpi(IContainer container, KpiPdf kpi)
    {
        container.Border(1)
            .BorderColor(Border)
            .Background(White)
            .Padding(9)
            .Column(column =>
            {
                column.Item().Text(kpi.Etiqueta)
                    .FontSize(6.5f)
                    .Bold()
                    .FontColor(Muted);
                column.Item().PaddingTop(3).Text(kpi.Valor)
                    .FontSize(13)
                    .Bold()
                    .FontColor(kpi.Color);
                column.Item().PaddingTop(2).Text(kpi.Detalle)
                    .FontSize(6.2f)
                    .FontColor(Muted);
            });
    }

    /// <summary>Panel de barras horizontales; evita depender de imágenes externas.</summary>
    private static void ComponerGrafico(IContainer container, IReadOnlyList<BarraPdf> barras)
    {
        container.Border(1)
            .BorderColor(Border)
            .Background(White)
            .Padding(10)
            .Column(column =>
            {
                column.Item().Text("Comportamiento del período")
                    .FontSize(9)
                    .Bold()
                    .FontColor(Navy);
                column.Item().Text("Distribución de los indicadores principales")
                    .FontSize(6.5f)
                    .FontColor(Muted);

                column.Item().PaddingTop(10).Column(lista =>
                {
                    if (barras.Count == 0)
                    {
                        lista.Item().Text("Sin datos para graficar.")
                            .FontSize(7)
                            .FontColor(Muted);
                        return;
                    }

                    foreach (var barra in barras)
                    {
                        lista.Item().PaddingBottom(7).Column(fila =>
                        {
                            fila.Item().Row(encabezado =>
                            {
                                encabezado.RelativeItem().Text(barra.Etiqueta)
                                    .FontSize(7)
                                    .FontColor(Navy);
                                encabezado.ConstantItem(84).AlignRight().Text(barra.Valor)
                                    .FontSize(7)
                                    .Bold()
                                    .FontColor(Navy);
                            });

                            var porcentaje = (float)Math.Clamp(barra.Porcentaje, 0, 100);
                            fila.Item().PaddingTop(2).Height(7).Row(medidor =>
                            {
                                medidor.RelativeItem(Math.Max(0.1f, porcentaje))
                                    .Height(7)
                                    .Background(barra.Color);
                                medidor.RelativeItem(Math.Max(0.1f, 100 - porcentaje))
                                    .Height(7)
                                    .Background("#E8EEF5");
                            });
                        });
                    }
                });
            });
    }

    /// <summary>Ficha con período, filtros, usuario y fecha de generación.</summary>
    private static void ComponerFicha(
        IContainer container,
        ReportePdfContenido contenido,
        ReportePdfContexto contexto)
    {
        container.Border(1)
            .BorderColor(Border)
            .Background(White)
            .Padding(10)
            .Column(column =>
            {
                column.Item().Text("Ficha del reporte")
                    .FontSize(9)
                    .Bold()
                    .FontColor(Navy);

                column.Item().PaddingTop(7).Element(datos =>
                {
                    datos.Column(lista =>
                    {
                        AgregarDato(lista, "Período", contexto.Periodo);
                        AgregarDato(lista, "Odontólogo", contexto.Odontologo);
                        AgregarDato(lista, "Registros", contexto.Registros.ToString(Cultura));

                        if (!string.IsNullOrWhiteSpace(contexto.Estado))
                        {
                            AgregarDato(lista, "Estado", contexto.Estado);
                        }

                        if (!string.IsNullOrWhiteSpace(contexto.Metodo))
                        {
                            AgregarDato(lista, "Método", contexto.Metodo);
                        }

                        AgregarDato(lista, "Generado", contexto.Generado);
                        AgregarDato(lista, "Usuario", contexto.Usuario ?? "Usuario");
                    });
                });
            });
    }

    private static void AgregarDato(ColumnDescriptor column, string etiqueta, string valor)
    {
        column.Item().PaddingBottom(5).Row(row =>
        {
            row.ConstantItem(58).Text(etiqueta)
                .FontSize(6.5f)
                .FontColor(Muted);
            row.RelativeItem().Text(valor)
                .FontSize(7)
                .FontColor(Navy);
        });
    }

    private static void ComponerNota(IContainer container, string nota)
    {
        container.Border(1)
            .BorderColor("#C9D8E5")
            .Background(Soft)
            .Padding(9)
            .Column(column =>
            {
                column.Item().Text("Nota metodológica")
                    .FontSize(7)
                    .Bold()
                    .FontColor(Navy);
                column.Item().PaddingTop(2).Text(nota)
                    .FontSize(6.5f)
                    .FontColor(Muted);
            });
    }

    /// <summary>
    /// Lectura rápida del período. Complementa las tarjetas con frases que
    /// resumen los hallazgos más importantes del informe.
    /// </summary>
    private static void ComponerPuntosClave(IContainer container, IReadOnlyList<string> puntos)
    {
        container.Border(1)
            .BorderColor(Border)
            .Background(White)
            .Padding(10)
            .Column(column =>
            {
                column.Item().Text("Lectura del período")
                    .FontSize(9)
                    .Bold()
                    .FontColor(Navy);

                column.Item().PaddingTop(6).Column(lista =>
                {
                    foreach (var punto in puntos)
                    {
                        lista.Item().PaddingBottom(4).Row(fila =>
                        {
                            fila.ConstantItem(9).Text("•")
                                .FontSize(8)
                                .Bold()
                                .FontColor(Blue);
                            fila.RelativeItem().Text(punto)
                                .FontSize(7)
                                .FontColor(Muted);
                        });
                    }
                });
            });
    }

    /// <summary>Tabla del anexo. El encabezado se repite en cada página.</summary>
    private static void ComponerTabla(IContainer container, ReportePdfContenido contenido)
    {
        if (contenido.Filas.Count == 0)
        {
            container.Padding(14)
                .Border(1)
                .BorderColor(Border)
                .Text("No hay registros para el período y los filtros seleccionados.")
                .FontSize(7.5f)
                .FontColor(Muted);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columnas =>
            {
                foreach (var columna in contenido.Columnas)
                {
                    columnas.RelativeColumn(columna.Peso);
                }
            });

            table.Header(header =>
            {
                foreach (var columna in contenido.Columnas)
                {
                    header.Cell()
                        .Element(celda => CeldaEncabezado(celda, columna.Derecha))
                        .Text(columna.Titulo)
                        .FontSize(6.4f)
                        .Bold()
                        .FontColor(White);
                }
            });

            for (var indiceFila = 0; indiceFila < contenido.Filas.Count; indiceFila++)
            {
                var fila = contenido.Filas[indiceFila];

                for (var indiceColumna = 0; indiceColumna < contenido.Columnas.Count; indiceColumna++)
                {
                    var columna = contenido.Columnas[indiceColumna];
                    var valor = indiceColumna < fila.Count
                        ? fila[indiceColumna]
                        : string.Empty;

                    table.Cell()
                        .Element(celda => CeldaCuerpo(celda, indiceFila, columna.Derecha))
                        .Text(valor)
                        .FontSize(6.6f)
                        .FontColor(Navy);
                }
            }
        });
    }

    private static IContainer CeldaEncabezado(IContainer container, bool derecha)
    {
        var celda = container
            .Background(Navy)
            .PaddingVertical(5)
            .PaddingHorizontal(5);

        return derecha ? celda.AlignRight() : celda.AlignLeft();
    }

    private static IContainer CeldaCuerpo(IContainer container, int indiceFila, bool derecha)
    {
        var celda = container
            .Background(indiceFila % 2 == 0 ? Soft : White)
            .BorderBottom(1)
            .BorderColor(Border)
            .PaddingVertical(5)
            .PaddingHorizontal(5);

        return derecha ? celda.AlignRight() : celda.AlignLeft();
    }

    /// <summary>Pie con confidencialidad y numeración de páginas.</summary>
    private static void ComponerPie(
        IContainer container,
        ReportePdfContenido contenido,
        ReportePdfContexto contexto)
    {
        container.BorderTop(1)
            .BorderColor(Border)
            .PaddingTop(6)
            .Row(row =>
            {
                row.RelativeItem().Text("Sonrisa Digital · Documento confidencial")
                    .FontSize(6)
                    .FontColor(Muted);

                row.RelativeItem().AlignCenter()
                    .Text($"{contenido.Titulo} · {contexto.Periodo}")
                    .FontSize(6)
                    .FontColor(Muted);

                row.RelativeItem().AlignRight().Text(texto =>
                {
                    texto.Span("Página ").FontSize(6).FontColor(Muted);
                    texto.CurrentPageNumber().FontSize(6).Bold().FontColor(Navy);
                    texto.Span(" de ").FontSize(6).FontColor(Muted);
                    texto.TotalPages().FontSize(6).Bold().FontColor(Navy);
                });
            });
    }

    /// <summary>Nombre visible del odontólogo o alcance de la sesión.</summary>
    private static string ResolverOdontologo(
        ReporteFiltroDto filtro,
        ReporteOpcionesDto opciones)
    {
        if (!opciones.PuedeFiltrarOdontologos)
        {
            return "Mi actividad";
        }

        if (filtro.OdontologoId is not int odontologoId)
        {
            return "Todos";
        }

        return opciones.Odontologos
            .FirstOrDefault(odontologo => odontologo.Id == odontologoId)?.Nombre
            ?? "Odontólogo seleccionado";
    }

    private static string? ObtenerEstadoFiltro(TipoReportePdf tipo, ReporteFiltroDto filtro)
    {
        return tipo switch
        {
            TipoReportePdf.Citas when filtro.EstadoCitaFiltro is { } estadoCita =>
                EstadoCitaTexto(estadoCita),
            TipoReportePdf.Tratamientos when filtro.EstadoTratamientoFiltro is { } estadoTratamiento =>
                EstadoTratamientoTexto(estadoTratamiento),
            _ => null
        };
    }

    private static void AgregarBarra(
        ICollection<BarraPdf> barras,
        string etiqueta,
        int cantidad,
        int total,
        string color)
    {
        var porcentaje = total <= 0 ? 0 : (double)cantidad / total * 100;
        barras.Add(new BarraPdf(
            etiqueta,
            $"{cantidad.ToString(Cultura)} · {porcentaje:0}%",
            porcentaje,
            color));
    }

    private static string Porcentaje(int cantidad, int total) =>
        total <= 0 ? "0%" : $"{(double)cantidad / total * 100:0}%";

    private static string Money(decimal valor)
    {
        var texto = valor == decimal.Truncate(valor)
            ? valor.ToString("N0", Cultura)
            : valor.ToString("N2", Cultura);

        return $"RD${texto}";
    }

    private static string EstadoCitaTexto(EstadoCita estado) => estado switch
    {
        EstadoCita.Programada => "Programada",
        EstadoCita.Confirmada => "Confirmada",
        EstadoCita.Atendida => "Atendida",
        EstadoCita.Cancelada => "Cancelada",
        EstadoCita.NoAsistio => "No asistió",
        _ => estado.ToString()
    };

    private static string EstadoTratamientoTexto(EstadoTratamiento estado) => estado switch
    {
        EstadoTratamiento.EnProceso => "En proceso",
        EstadoTratamiento.Completado => "Completado",
        EstadoTratamiento.Cancelado => "Cancelado",
        _ => estado.ToString()
    };

    private static string MetodoPagoTexto(MetodoPago metodo) => metodo switch
    {
        MetodoPago.Efectivo => "Efectivo",
        MetodoPago.Tarjeta => "Tarjeta",
        MetodoPago.Transferencia => "Transferencia",
        _ => metodo.ToString()
    };

    private static string ColorMetodo(MetodoPago metodo) => metodo switch
    {
        MetodoPago.Efectivo => Green,
        MetodoPago.Tarjeta => Blue,
        MetodoPago.Transferencia => Teal,
        _ => Muted
    };

    // Modelos internos del documento. No se persisten ni salen del Service.
    private sealed record KpiPdf(
        string Etiqueta,
        string Valor,
        string Detalle,
        string Color);

    private sealed record BarraPdf(
        string Etiqueta,
        string Valor,
        double Porcentaje,
        string Color);

    private sealed record ColumnaPdf(
        string Titulo,
        float Peso,
        bool Derecha = false);

    private sealed record ReportePdfContenido(
        string Titulo,
        string Subtitulo,
        IReadOnlyList<KpiPdf> Kpis,
        IReadOnlyList<BarraPdf> Barras,
        IReadOnlyList<string> PuntosClave,
        IReadOnlyList<ColumnaPdf> Columnas,
        IReadOnlyList<IReadOnlyList<string>> Filas,
        string Nota);

    private sealed record ReportePdfContexto(
        string Periodo,
        string Usuario,
        string Generado,
        string Odontologo,
        string? Estado,
        string? Metodo,
        int Registros);
}
