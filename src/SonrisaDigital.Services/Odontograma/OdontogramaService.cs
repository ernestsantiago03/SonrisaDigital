using SonrisaDigital.Data.Common;
using Microsoft.AspNetCore.Authorization;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Catalogos;
using SonrisaDigital.Services.Odontograma.DTOs;

namespace SonrisaDigital.Services.Odontograma;

/// <summary>
/// Lógica (el "cerebro") del módulo de Odontograma.
///
/// ¿Qué hace este archivo?
/// Atiende los pedidos de la pantalla del odontograma: buscar pacientes para el panel,
/// leer las 32 piezas dentales de un paciente con su estado, y guardar el estado de una pieza.
/// Es la capa del medio de la aplicación:
///
///     Pantalla (UI)  →  Service (este archivo)  →  Repository  →  Base de datos
///
/// Idea clave para entender el odontograma:
///  - Las 32 piezas existen SIEMPRE en la tabla "PiezasDentales" (es un catálogo fijo).
///  - Lo que cambia por paciente es la tabla "RegistrosOdontograma": una fila por cada pieza
///    que alguien ha tocado (con su estado: sano, caries, etc.).
///  - Al mostrar el odontograma se cruzan las dos: para cada pieza se busca si el paciente
///    tiene registro; si no lo tiene, la pieza se pinta como sana y sin datos.
///
/// Orden de lectura del archivo:
///  1) Buscar pacientes                     → GetActivePatientsAsync
///  2) Leer las 32 piezas de un paciente    → GetByPatientAsync
///  3) Resolver la firma de la sesión       → GetEditContextAsync
///  4) Guardar el estado de una pieza       → SaveAsync
///  5) Ayudantes privados                   → ResolverOdontologoFirmanteAsync, EsAdministrador
/// </summary>
public sealed class OdontogramaService(
    IOdontogramaRepository repository,
    IPacienteRepository pacienteRepository,
    ICurrentUserService currentUserService,
    IAuthorizationService authorizationService,
    ICatalogoCache catalogoCache) : IOdontogramaService
{
    // =========================================================================================
    // 1) BUSCAR PACIENTES  →  alimenta el panel de búsqueda de la pantalla del odontograma
    // =========================================================================================

    /// <summary>
    /// Pacientes activos para el panel de búsqueda del odontograma. Con un texto de búsqueda
    /// filtra por nombre o apellido; sin texto devuelve todos, en orden alfabético.
    /// </summary>
    public async Task<PagedResult<OdontogramaPacienteDto>> SearchActivePatientsPagedAsync(
        string? searchTerm,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Ver", "No tienes permiso para consultar odontogramas.");

        // Paso 1: pedirle al repositorio los pacientes ACTIVOS que coincidan con el texto.
        // (Solo activos: a un paciente desactivado no se le sigue trabajando el odontograma.)
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var resultado = await repository.SearchActivePatientsPagedAsync(searchTerm, page, pageSize, cancellationToken);

        // Paso 2: quedarse solo con lo que la pantalla necesita: id y nombre completo.
        return new PagedResult<OdontogramaPacienteDto>(
            resultado.Items.Select(patient => new OdontogramaPacienteDto(patient.Id, $"{patient.Nombres} {patient.Apellidos}")).ToList(),
            resultado.Total,
            resultado.Page,
            resultado.PageSize);
    }

    public async Task<OdontogramaPacienteDto?> GetActivePatientAsync(int pacienteId, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Ver", "No tienes permiso para consultar odontogramas.");

        var patient = await repository.GetActivePatientByIdAsync(pacienteId, cancellationToken);
        return patient is null ? null : new OdontogramaPacienteDto(patient.Id, $"{patient.Nombres} {patient.Apellidos}");
    }

    // =========================================================================================
    // 2) LEER LAS 32 PIEZAS DE UN PACIENTE  →  dibuja el odontograma completo
    // =========================================================================================

    /// <summary>
    /// Las 32 piezas dentales con el estado que tienen para ese paciente. Las piezas que el
    /// paciente todavía no tiene registradas vienen "sanas" y sin observaciones.
    /// </summary>
    public async Task<IReadOnlyList<OdontogramaPiezaDto>> GetByPatientAsync(int pacienteId, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Ver", "No tienes permiso para consultar odontogramas.");

        // Paso 1: traer las dos listas por separado.
        //   pieces  = el catálogo de las 32 piezas dentales (siempre las mismas).
        //   records = los registros que ESTE paciente tiene (solo las piezas que se han tocado).
        // El catálogo se comparte en caché; los registros clínicos del paciente
        // se consultan siempre en la base de datos.
        var pieces = await catalogoCache.ObtenerPiezasAsync(
            ct => repository.GetPiecesAsync(ct),
            cancellationToken);
        var records = (await repository.GetRecordsAsync(pacienteId, cancellationToken)).ToDictionary(record => record.PiezaDentalId);

        // Paso 2: cruzar las dos listas. Se recorre el catálogo de las 32 piezas y, para cada
        // una, se busca su registro con el diccionario (que es rápido porque está indexado
        // por el id de la pieza).
        return pieces.Select(piece => records.TryGetValue(piece.Id, out var record)
            // Caso A: el paciente SÍ tiene registro de esta pieza → se usa su estado real.
            ? new OdontogramaPiezaDto
            {
                Id = piece.Id,
                NumeroFDI = piece.NumeroFDI,
                Nombre = piece.Nombre,
                Estado = record.Estado,
                Observaciones = record.Observaciones,
                FechaModificacion = record.FechaModificacion,
                Odontologo = $"{record.Odontologo.Nombres} {record.Odontologo.Apellidos}"
            }
            // Caso B: no hay registro → la pieza se muestra sin estado (sana, sin datos).
            : new OdontogramaPiezaDto { Id = piece.Id, NumeroFDI = piece.NumeroFDI, Nombre = piece.Nombre })
            .ToList();
    }

    /// <summary>
    /// Todos los guardados de una pieza de un paciente, del más reciente al más antiguo.
    /// </summary>
    public async Task<IReadOnlyList<OdontogramaHistorialDto>> GetHistorialAsync(int pacienteId, int piezaDentalId, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Ver", "No tienes permiso para consultar odontogramas.");

        var historial = await repository.GetHistorialAsync(pacienteId, piezaDentalId, cancellationToken);

        return historial
            .Select(item => new OdontogramaHistorialDto(
                item.FechaHora,
                item.EstadoAnterior,
                item.EstadoNuevo,
                item.Observaciones,
                $"{item.Odontologo.Nombres} {item.Odontologo.Apellidos}"))
            .ToList();
    }

    // =========================================================================================
    // 3) RESOLVER LA FIRMA DE LA SESIÓN  →  qué puede hacer el usuario en esta pantalla
    // =========================================================================================

    /// <summary>
    /// Contexto de edición del odontograma. El perfil vinculado al usuario manda; un
    /// administrador sin perfil puede elegir un odontólogo activo y un usuario sin perfil
    /// ni rol administrador queda en modo consulta con un mensaje claro.
    /// </summary>
    public async Task<OdontogramaEditContextDto> GetEditContextAsync(CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Ver", "No tienes permiso para consultar odontogramas.");

        // Paso 1: saber si el rol tiene permiso de edición y si el usuario tiene un
        // odontólogo vinculado. Esa vinculación es la firma de la sesión.
        var tienePermisoEditar = await authorizationService.TienePermisoAsync(currentUserService, "Odontograma.Editar");
        var userId = currentUserService.UsuarioId;
        var vinculadoId = string.IsNullOrWhiteSpace(userId)
            ? null
            : await repository.GetOdontologoIdByUserAsync(userId, cancellationToken);

        // Paso 2: cargar el perfil vinculado para comprobar que exista y siga activo.
        var vinculado = vinculadoId.HasValue
            ? await repository.GetOdontologoAsync(vinculadoId.Value, cancellationToken)
            : null;

        // Paso 3: solo un administrador sin perfil necesita elegir un firmante de la lista.
        var esAdministrador = EsAdministrador();
        var requiereSeleccionar = tienePermisoEditar && !vinculadoId.HasValue && esAdministrador;
        var disponibles = requiereSeleccionar
            ? await repository.GetActiveOdontologosAsync(cancellationToken)
            : [];

        // Paso 4: puede editar si tiene permiso y una firma válida. El administrador sin
        // perfil necesita que exista al menos un odontólogo activo para elegir.
        var vinculadoActivo = vinculado is not null && vinculado.Estado == EstadoRegistro.Activo;
        var puedeEditar = tienePermisoEditar
            && (vinculadoActivo || (requiereSeleccionar && disponibles.Count > 0));

        // Paso 5: explicar por qué no puede editar cuando el permiso sí existe.
        string? motivoSinEdicion = null;
        if (tienePermisoEditar && !puedeEditar)
        {
            if (vinculadoId.HasValue)
            {
                motivoSinEdicion = vinculado is null
                    ? "El odontólogo vinculado a tu usuario no existe. Un administrador debe corregir el perfil."
                    : "El odontólogo vinculado a tu usuario está inactivo. Un administrador debe activarlo para editar el odontograma.";
            }
            else if (!esAdministrador)
            {
                motivoSinEdicion = "Tu usuario no tiene un perfil de odontólogo vinculado. Un administrador debe asignarte uno para editar el odontograma.";
            }
            else
            {
                motivoSinEdicion = "No hay odontólogos activos para firmar el odontograma. Activa un odontólogo antes de guardar.";
            }
        }

        return new OdontogramaEditContextDto
        {
            TienePermisoEditar = tienePermisoEditar,
            PuedeEditar = puedeEditar,
            RequiereSeleccionarOdontologo = requiereSeleccionar,
            OdontologoVinculadoId = vinculadoActivo ? vinculado!.Id : null,
            OdontologoVinculadoNombre = vinculadoActivo
                ? $"{vinculado!.Nombres} {vinculado.Apellidos}".Trim()
                : null,
            MotivoSinEdicion = motivoSinEdicion,
            OdontologosDisponibles = disponibles
                .Select(dentist => new OdontogramaOdontologoDto(
                    dentist.Id,
                    $"{dentist.Nombres} {dentist.Apellidos}".Trim()))
                .ToList()
        };
    }

    // =========================================================================================
    // 4) GUARDAR EL ESTADO DE UNA PIEZA  →  cuando el odontólogo hace clic en un diente
    // =========================================================================================

    /// <summary>
    /// Cambia el estado de una pieza dental del paciente (sano, caries, restauración, etc.).
    /// Si es la primera vez que se toca esa pieza, crea el registro; si ya existía, lo actualiza.
    /// Deja el movimiento anotado en la auditoría.
    /// </summary>
    public async Task SaveAsync(int pacienteId, int piezaDentalId, EstadoOdontograma estado, string? observaciones, int? odontologoId = null, CancellationToken cancellationToken = default)
    {
        await authorizationService.AsegurarPermisoAsync(currentUserService, "Odontograma.Editar", "No tienes permiso para editar odontogramas.");

        // Paso 1: obtener de la sesión el usuario responsable. El Service ya no
        // acepta un userId arbitrario desde la UI (ARQ-05). Si no hay sesión,
        // la operación se detiene antes de escribir.
        var userId = currentUserService.UsuarioId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "No hay un usuario autenticado para guardar el odontograma.");
        }

        // Paso 2: resolver el odontólogo que firma el cambio. Un odontólogo vinculado solo
        // puede guardar como él mismo; un administrador sin perfil debe elegir uno activo.
        var odontologoFirmanteId = await ResolverOdontologoFirmanteAsync(odontologoId, cancellationToken);

        // Paso 3: buscar si ya existe un registro de esa pieza para ese paciente.
        var record = await repository.GetRecordAsync(pacienteId, piezaDentalId, cancellationToken);
        EstadoOdontograma? estadoAnterior = record?.Estado;

        // Paso 4: si no existe, se crea la fila nueva (primera vez que se toca la pieza).
        if (record is null)
        {
            record = new RegistroOdontograma
            {
                PacienteId = pacienteId,
                PiezaDentalId = piezaDentalId,
                OdontologoId = odontologoFirmanteId
            };
            await repository.AddAsync(record, cancellationToken);
        }

        // Paso 5: escribir los datos nuevos en la fila (sirve igual para la nueva y para la
        // que ya existía). Las observaciones vacías se guardan como null.
        record.Estado = estado;
        record.Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
        record.OdontologoId = odontologoFirmanteId;
        record.FechaModificacion = DateTime.UtcNow;

        // Paso 6: conseguir los nombres para el texto de la auditoría (paciente y pieza).
        // La pieza sale de la misma caché que usa la pantalla; el paciente es dato
        // clínico y se consulta directamente.
        var paciente = await pacienteRepository.GetByIdAsync(pacienteId, cancellationToken);
        var pieza = (await catalogoCache.ObtenerPiezasAsync(
                ct => repository.GetPiecesAsync(ct),
                cancellationToken))
            .FirstOrDefault(item => item.Id == piezaDentalId);

        // Si por alguna razón no se encuentra el nombre, se usa el id para que el texto
        // de auditoría nunca quede vacío.
        var nombrePaciente = paciente is null
            ? $"paciente {pacienteId}"
            : $"{paciente.Nombres} {paciente.Apellidos}".Trim();
        var numeroPieza = string.IsNullOrWhiteSpace(pieza?.NumeroFDI) ? piezaDentalId.ToString() : pieza!.NumeroFDI;

        // Paso 7: guardar el registro y la auditoría JUNTOS en una sola operación.
        // El repositorio completa el EntidadId después de guardar la pieza, para que
        // una fila nueva no quede auditada con Id 0 (ODO-03).
        await repository.SaveChangesWithAuditAsync(record, new RegistroAuditoria
        {
            UsuarioId = userId,
            Accion = "Modificó",
            Modulo = "Odontograma",
            Entidad = nameof(RegistroOdontograma),
            Descripcion = $"Actualizó la pieza dental {numeroPieza} del paciente {nombrePaciente}."
        }, new HistorialOdontograma
        {
            PacienteId = pacienteId,
            PiezaDentalId = piezaDentalId,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = estado,
            Observaciones = record.Observaciones,
            OdontologoId = odontologoFirmanteId,
            FechaHora = record.FechaModificacion
        }, cancellationToken);
    }

    // =========================================================================================
    // 5) AYUDANTES PRIVADOS  →  reglas de la firma que no dependen de la pantalla
    // =========================================================================================

    /// <summary>
    /// Resuelve qué odontólogo firma el cambio del odontograma. El perfil vinculado al
    /// usuario manda; solo un administrador sin perfil puede elegir otro odontólogo activo.
    /// </summary>
    private async Task<int> ResolverOdontologoFirmanteAsync(
        int? odontologoId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UsuarioId;

        // El vínculo usuario → odontólogo es la firma de la sesión. Si existe, no se
        // permite guardar como otro profesional, aunque la UI envíe un id distinto.
        var vinculadoId = string.IsNullOrWhiteSpace(userId)
            ? null
            : await repository.GetOdontologoIdByUserAsync(userId, cancellationToken);

        if (vinculadoId.HasValue)
        {
            if (odontologoId.HasValue && odontologoId.Value != vinculadoId.Value)
            {
                throw new InvalidOperationException(
                    "Solo puedes guardar el odontograma con el odontólogo vinculado a tu usuario.");
            }

            var vinculado = await repository.GetOdontologoAsync(vinculadoId.Value, cancellationToken)
                ?? throw new InvalidOperationException("El odontólogo vinculado a tu usuario no existe.");

            if (vinculado.Estado != EstadoRegistro.Activo)
            {
                throw new InvalidOperationException("El odontólogo vinculado a tu usuario está inactivo.");
            }

            return vinculado.Id;
        }

        // Un administrador sin perfil puede actuar como supervisor y elegir un firmante
        // activo de la lista.
        if (!EsAdministrador())
        {
            throw new InvalidOperationException(
                "Tu usuario no tiene un perfil de odontólogo vinculado. Un administrador debe asignarte uno.");
        }

        if (!odontologoId.HasValue || odontologoId.Value <= 0)
        {
            throw new InvalidOperationException(
                "Selecciona un odontólogo activo para guardar el odontograma.");
        }

        var seleccionado = await repository.GetOdontologoAsync(odontologoId.Value, cancellationToken)
            ?? throw new InvalidOperationException("El odontólogo seleccionado no existe.");

        if (seleccionado.Estado != EstadoRegistro.Activo)
        {
            throw new InvalidOperationException("El odontólogo seleccionado está inactivo.");
        }

        return seleccionado.Id;
    }

    /// <summary>
    /// Indica si la sesión tiene el rol administrador, que puede supervisar el odontograma
    /// aunque no tenga un perfil de odontólogo vinculado.
    /// </summary>
    private bool EsAdministrador() =>
        currentUserService.Principal?.IsInRole(RolesSonrisaDigital.Administrador) == true;
}
