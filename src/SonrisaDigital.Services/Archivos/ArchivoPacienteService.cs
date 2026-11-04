using System.Runtime.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Common;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Data.Tiempo;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;

namespace SonrisaDigital.Services.Archivos;

/// <summary>
/// Archivos del paciente que se muestran en el odontograma. Reglas:
///  - Cada subida crea un archivo nuevo; nunca se reemplaza uno registrado.
///  - Solo JPG y PNG, de hasta 20 MB, con metadatos quitados.
///  - Ocultar y restaurar es exclusivo del Dueño (Archivos.Ocultar); el archivo se conserva.
///  - Eliminar borra el archivo y su miniatura de forma definitiva (Odontograma.Editar), con auditoría.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ArchivoPacienteService(
    ApplicationDbContext dbContext,
    ITenantProvider tenant,
    IArchivoStorage storage,
    IAuditoriaService auditoriaService,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService) : IArchivoPacienteService
{
    private const string Modulo = "Archivos del paciente";
    private const int MaxDescripcion = 300;
    private const int MaxNombre = 255;

    public async Task<ArchivosPacienteListaDto> ListarAsync(int pacienteId, bool incluirOcultos = false, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Ver", "No tienes permiso para consultar odontogramas.");

        var puedeOcultar = await authorizationService.TienePermisoAsync(currentUserService, "Archivos.Ocultar");
        var mostrarOcultos = incluirOcultos && puedeOcultar;

        var consulta = dbContext.ArchivosPaciente.AsNoTracking()
            .Include(item => item.Usuario)
            .Where(item => item.PacienteId == pacienteId);

        if (!mostrarOcultos)
        {
            consulta = consulta.Where(item => !item.Oculto);
        }

        var archivos = await consulta
            .OrderByDescending(item => item.FechaEstudio)
            .ThenByDescending(item => item.FechaSubida)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return new ArchivosPacienteListaDto(archivos.Select(ToDto).ToList(), puedeOcultar);
    }

    public async Task<ArchivoPacienteDto> SubirAsync(
        int pacienteId,
        string nombreOriginal,
        byte[] contenido,
        DateOnly fechaEstudio,
        string? descripcion,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Editar", "No tienes permiso para subir archivos al odontograma.");

        var usuarioId = currentUserService.UsuarioId
            ?? throw new InvalidOperationException("No hay un usuario autenticado para subir el archivo.");

        if (contenido.Length == 0)
        {
            throw new InvalidOperationException("El archivo está vacío.");
        }

        if (contenido.Length > LimitesArchivo.TamanoMaximo)
        {
            throw new InvalidOperationException("El archivo supera el máximo de 20 MB.");
        }

        var tipo = ImagenArchivo.DetectarTipo(contenido)
            ?? throw new InvalidOperationException("Solo se permiten archivos JPG o PNG.");

        if (fechaEstudio == default || fechaEstudio > DateOnly.FromDateTime(RelojNegocio.Hoy()))
        {
            throw new InvalidOperationException("La fecha del estudio es obligatoria y no puede ser futura.");
        }

        var descripcionLimpia = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        if (descripcionLimpia is { Length: > MaxDescripcion })
        {
            throw new InvalidOperationException($"La descripción no puede superar los {MaxDescripcion} caracteres.");
        }

        var clinicaId = tenant.ClinicaId
            ?? throw new InvalidOperationException("No hay clínica en la sesión.");

        var paciente = await dbContext.Pacientes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == pacienteId, cancellationToken)
            ?? throw new InvalidOperationException("El paciente no existe.");

        if (paciente.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("Solo se pueden subir archivos a pacientes activos.");
        }

        // La validación y el procesamiento ocurren antes de escribir nada en disco.
        var original = ImagenArchivo.QuitarMetadatos(contenido, tipo);
        var miniatura = ImagenArchivo.GenerarMiniatura(original);

        var nombre = NormalizarNombre(nombreOriginal, tipo);
        var carpeta = $"{clinicaId}/{pacienteId}";
        var identificador = Guid.NewGuid().ToString("N");
        var claveArchivo = $"{carpeta}/{identificador}{ExtensionPara(tipo)}";
        var claveMiniatura = $"{carpeta}/{identificador}_m.jpg";

        await storage.GuardarAsync(claveArchivo, original, cancellationToken);

        try
        {
            await storage.GuardarAsync(claveMiniatura, miniatura, cancellationToken);

            var archivo = new ArchivoPaciente
            {
                PacienteId = pacienteId,
                NombreOriginal = nombre,
                ClaveArchivo = claveArchivo,
                ClaveMiniatura = claveMiniatura,
                TipoContenido = tipo,
                Tamano = original.LongLength,
                FechaEstudio = fechaEstudio,
                Descripcion = descripcionLimpia,
                UsuarioId = usuarioId,
                FechaSubida = DateTime.UtcNow
            };

            await dbContext.ExecuteInTransactionAsync(async () =>
            {
                dbContext.ArchivosPaciente.Add(archivo);
                await dbContext.SaveChangesAsync(cancellationToken);

                await auditoriaService.RegistrarAsync(
                    "Subió", Modulo, nameof(ArchivoPaciente), archivo.Id,
                    $"Subió el archivo {nombre} al paciente {paciente.Nombres} {paciente.Apellidos}.",
                    cancellationToken);
            }, cancellationToken);

            return ToDto(archivo, currentUserService.Nombre ?? string.Empty);
        }
        catch
        {
            // Si la base no registró la subida, no dejamos archivos huérfanos en disco.
            storage.EliminarSubidaFallida(claveArchivo);
            storage.EliminarSubidaFallida(claveMiniatura);
            throw;
        }
    }

    public async Task<ArchivoPacienteContenido?> ObtenerContenidoAsync(int archivoId, bool miniatura = false, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Ver", "No tienes permiso para consultar odontogramas.");

        var archivo = await dbContext.ArchivosPaciente.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == archivoId, cancellationToken);

        if (archivo is null)
        {
            return null;
        }

        if (archivo.Oculto && !await authorizationService.TienePermisoAsync(currentUserService, "Archivos.Ocultar"))
        {
            return null;
        }

        var datos = await storage.LeerAsync(miniatura ? archivo.ClaveMiniatura : archivo.ClaveArchivo, cancellationToken);
        return miniatura
            ? new ArchivoPacienteContenido(datos, "image/jpeg", archivo.NombreOriginal)
            : new ArchivoPacienteContenido(datos, archivo.TipoContenido, archivo.NombreOriginal);
    }

    public async Task OcultarAsync(int archivoId, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Archivos.Ocultar", "Solo el Dueño puede ocultar archivos del paciente.");
        await CambiarVisibilidadAsync(archivoId, ocultar: true, cancellationToken);
    }

    public async Task RestaurarAsync(int archivoId, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Archivos.Ocultar", "Solo el Dueño puede restaurar archivos del paciente.");
        await CambiarVisibilidadAsync(archivoId, ocultar: false, cancellationToken);
    }

    private async Task CambiarVisibilidadAsync(int archivoId, bool ocultar, CancellationToken cancellationToken)
    {
        var usuarioId = currentUserService.UsuarioId
            ?? throw new InvalidOperationException("No hay un usuario autenticado.");

        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            var archivo = await dbContext.ArchivosPaciente
                .FirstOrDefaultAsync(item => item.Id == archivoId, cancellationToken)
                ?? throw new InvalidOperationException("El archivo no existe.");

            if (archivo.Oculto == ocultar)
            {
                throw new InvalidOperationException(ocultar ? "El archivo ya está oculto." : "El archivo no está oculto.");
            }

            archivo.Oculto = ocultar;
            archivo.FechaOcultado = ocultar ? DateTime.UtcNow : null;
            archivo.OcultadoPorUsuarioId = ocultar ? usuarioId : null;

            await dbContext.SaveChangesAsync(cancellationToken);

            await auditoriaService.RegistrarAsync(
                ocultar ? "Ocultó" : "Restauró", Modulo, nameof(ArchivoPaciente), archivo.Id,
                $"{(ocultar ? "Ocultó" : "Restauró")} el archivo {archivo.NombreOriginal}.",
                cancellationToken);
        }, cancellationToken);
    }

    public async Task EliminarAsync(int archivoId, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Editar", "No tienes permiso para eliminar archivos del odontograma.");

        var archivo = await dbContext.ArchivosPaciente
            .FirstOrDefaultAsync(item => item.Id == archivoId, cancellationToken)
            ?? throw new InvalidOperationException("El archivo no existe.");

        var claveArchivo = archivo.ClaveArchivo;
        var claveMiniatura = archivo.ClaveMiniatura;

        await dbContext.ExecuteInTransactionAsync(async () =>
        {
            dbContext.ArchivosPaciente.Remove(archivo);
            await dbContext.SaveChangesAsync(cancellationToken);

            await auditoriaService.RegistrarAsync(
                "Eliminó", Modulo, nameof(ArchivoPaciente), archivoId,
                $"Eliminó el archivo {archivo.NombreOriginal} del paciente.",
                cancellationToken);
        }, cancellationToken);

        // El registro ya no existe; ahora se liberan los archivos físicos.
        storage.Eliminar(claveArchivo);
        storage.Eliminar(claveMiniatura);
    }

    private static ArchivoPacienteDto ToDto(ArchivoPaciente archivo) =>
        ToDto(archivo, archivo.Usuario?.Nombre ?? string.Empty);

    private static ArchivoPacienteDto ToDto(ArchivoPaciente archivo, string subidoPor) =>
        new(
            archivo.Id,
            archivo.NombreOriginal,
            archivo.Descripcion,
            archivo.FechaEstudio,
            archivo.FechaSubida,
            subidoPor,
            archivo.TipoContenido,
            archivo.Oculto,
            archivo.ClinicaId);

    private static string NormalizarNombre(string? nombreOriginal, string tipo)
    {
        var nombre = Path.GetFileName(nombreOriginal ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = "imagen" + ExtensionPara(tipo);
        }

        return nombre.Length > MaxNombre ? nombre[..MaxNombre] : nombre;
    }

    private static string ExtensionPara(string tipo) => tipo == "image/png" ? ".png" : ".jpg";
}
