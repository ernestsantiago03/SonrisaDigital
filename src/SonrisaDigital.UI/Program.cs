using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Suscripciones;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.UI.Components;
using SonrisaDigital.UI.Services;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Catalogos;
using SonrisaDigital.Services.Configuracion;
using SonrisaDigital.Services.Odontograma;
using SonrisaDigital.Services.Archivos;
using SonrisaDigital.Services.Reportes;
using SonrisaDigital.Services.Reportes.DTOs;
using SonrisaDigital.Services.Suscripciones;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// En Development se mantiene la consola legible. En producción los logs salen en
// JSON por consola (IIS stdout, journald o Docker) para poder filtrarlos por
// CorrelationId y por nivel sin herramientas adicionales.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

// ============================================================================
// Validación temprana de la configuración de producción.
//
// El objetivo es no arrancar con un dominio de ejemplo, una cadena de conexión
// vacía o claves de Data Protection efímeras. En Development no se aplica para
// no romper el runner E2E ni la configuración local.
// ============================================================================
var resultadoConfiguracion = ValidadorConfiguracionProduccion.Validar(
    builder.Configuration,
    builder.Environment.IsDevelopment());

foreach (var advertencia in resultadoConfiguracion.Advertencias)
{
    Console.Error.WriteLine($"[Configuración] Advertencia: {advertencia}");
}

if (resultadoConfiguracion.Errores.Count > 0)
{
    foreach (var error in resultadoConfiguracion.Errores)
    {
        Console.Error.WriteLine($"[Configuración] Error: {error}");
    }

    throw new InvalidOperationException(
        "La configuración de producción está incompleta. " +
        "Revisa los errores mostrados antes de iniciar la aplicación.");
}

// ============================================================================
// Data Protection firma las cookies de autenticación y protege el estado de los
// circuitos de Blazor. Si las claves cambian en cada publicación, todos los
// usuarios pierden la sesión. En producción se exige una carpeta persistente y
// respaldada, configurada en DataProtection:KeysPath.
// ============================================================================
var rutaClavesDataProtection = builder.Configuration["DataProtection:KeysPath"]?.Trim();
if (!string.IsNullOrWhiteSpace(rutaClavesDataProtection))
{
    var directorioClaves = new DirectoryInfo(rutaClavesDataProtection);
    if (!directorioClaves.Exists)
    {
        // La cuenta que ejecuta la aplicación debe tener permiso de escritura
        // sobre esta carpeta. Si no lo tiene, el arranque falla y queda visible
        // en el log en lugar de perder las claves silenciosamente.
        directorioClaves.Create();
    }

    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(directorioClaves)
        .SetApplicationName("SonrisaDigital");
}

// Add services to the container.
builder.Services.AddSonrisaDigitalServices(builder.Configuration, builder.Environment);
// La clínica activa se comparte entre el selector, el panel de clínicas y las páginas.
builder.Services.AddScoped<IClinicaActivaService, ClinicaActivaService>();

// ============================================================================
//
// Cuando la aplicacion corre detras de IIS, nginx o un balanceador, el request
// llega al proceso como HTTP y con la IP del proxy. Sin ForwardedHeaders la app
// no sabe que el usuario entro por HTTPS, las cookies Secure no se envian y
// UseHttpsRedirection puede entrar en un bucle de redirecciones.
//
// ForwardLimit = 1 significa "confiar en el ultimo proxy de la cadena". Los
// proxies adicionales se declaran en ReverseProxy:KnownProxies; si la lista esta
// vacia se mantiene el comportamiento por defecto (loopback, util para IIS en
// la misma maquina).
// ============================================================================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = builder.Configuration.GetValue<int?>("ReverseProxy:ForwardLimit") ?? 1;

    var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
    foreach (var proxy in knownProxies)
    {
        if (IPAddress.TryParse(proxy, out var direccion))
        {
            options.KnownProxies.Add(direccion);
        }
    }
});

// HSTS explicito: el valor por defecto son 30 dias; para produccion se usa un
// ano y se incluyen los subdominios. Preload se deja apagado hasta que el
// dominio este confirmado en la lista de precarga de los navegadores.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = false;
});

// Redireccion permanente (308) en lugar de temporal (307): conserva el metodo
// HTTP y le indica al navegador que recuerde el cambio a HTTPS.
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Tiempo que el servidor conserva el estado de un circuito desconectado
        // para que el cliente pueda reconectarse sin perder el trabajo en curso.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);

        // Límite de circuitos desconectados retenidos en memoria. 100 cubre el
        // uso de una clínica y evita que un pico de desconexiones consuma memoria
        // sin control.
        options.DisconnectedCircuitMaxRetained = 100;

        // Los detalles técnicos de una excepción solo se envían al navegador en
        // Development; en producción se evita filtrar rutas, SQL o datos internos.
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });

// Rate limiting: limita intentos de login por IP. Cada IP tiene su propio contador;
// un limitador global dejaría a toda la clínica sin poder entrar tras 5 intentos fallidos.
// El límite se puede elevar solo para las pruebas E2E, que inician sesión varias
// veces desde la misma IP. En producción queda en 5 por minuto si no se configura.
var loginPermitLimit = builder.Configuration.GetValue<int?>("Authentication:LoginPermitLimit") is int configurado && configurado > 0
    ? configurado
    : 5;

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Los circuitos Blazor no pasan por OnValidatePrincipal en cada interacción.
// Este proveedor revalida estado y SecurityStamp mientras el circuito sigue abierto.
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
    try
    {
        await IdentitySeeder.SeedAsync(scope.ServiceProvider, builder.Configuration, logger);
        await OdontogramaCatalogSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<SonrisaDigital.Data.ApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<ICatalogoCache>());
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "No se pudo preparar Identity. Verifica que la base SonrisaDigital exista y tenga aplicada la migracion.");
    }
}

// Debe ser el primer middleware del pipeline.
// Toma X-Forwarded-For / X-Forwarded-Proto y reconstruye el esquema y la IP
// reales antes de que HSTS, HTTPS o la autenticacion tomen decisiones.
app.UseForwardedHeaders();

// Identificador de correlación: cada petición lleva un X-Correlation-Id que aparece
// en su respuesta y en todas las líneas de log que genera. Si el proxy envía uno
// válido se reutiliza; si no, o si el valor tiene un formato raro, se genera uno nuevo.
// La validación evita que alguien inyecte saltos de línea o texto libre en los logs.
app.Use(async (context, next) =>
{
    var entrante = context.Request.Headers["X-Correlation-Id"].ToString();
    var correlationId = CorrelationIdValido(entrante)
        ? entrante
        : Guid.NewGuid().ToString("N");

    context.Response.Headers["X-Correlation-Id"] = correlationId;

    using (app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        await next();
    }
});

// Health checks expuestos sin autenticacion y antes de la redireccion HTTPS,
// para que un balanceador pueda consultarlos por HTTP sin recibir un 308.
// /health -> live: indica que la aplicacion esta en marcha.
// /health/ready -> ready: ademas valida que SQL Server responda.
app.MapGet("/health", (ApplicationDbContext db) =>
{
    return Results.Json(new { status = "Healthy" });
}).AllowAnonymous();

app.MapGet("/health/ready", async (ApplicationDbContext db) =>
{
    try
    {
        var dbReady = await db.Database.CanConnectAsync();
        if (!dbReady)
        {
            return Results.Json(
                new { status = "Unhealthy", database = "unreachable" },
                statusCode: 503);
        }

        return Results.Json(new { status = "Healthy", database = "reachable" });
    }
    catch
    {
        // No se expone el detalle de la excepcion (cadena de conexion,
        // servidor, etc.) para no filtrar informacion interna.
        return Results.Json(
            new { status = "Unhealthy", database = "error" },
            statusCode: 503);
    }
}).AllowAnonymous();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // HSTS configurado en AddHsts (365 dias, subdominios incluidos).
    app.UseHsts();
}

// Cabeceras HTTP de seguridad.
// Se aplican a todas las respuestas para endurecer el navegador frente a
// clickjacking, MIME-sniffing, fuga de referrer y carga de recursos no confiables.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    // CSP compatible con Blazor Server (SignalR/WebSocket) y los assets del framework.
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' wss: ws:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';";
    await next();
});

// El formulario de login lleva un token antiforgery. Si el navegador reutiliza
// una copia cacheada (bfcache, historial o una pestaña abierta antes de reiniciar
// la app), el token puede no corresponder con la cookie actual. Estas cabeceras
// obligan a pedir siempre una página fresca con su token.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method)
        && context.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
    }

    await next();
});

// Los endpoints de salud se responden sin forzar HTTPS para que un balanceador
// pueda consultarlos por HTTP sin recibir un 308. Se colocan antes de
// UseHttpsRedirection y devuelven la respuesta directamente, sin pasar al resto
// del pipeline.
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var db = context.RequestServices.GetRequiredService<ApplicationDbContext>();

    if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync("{\"status\":\"Healthy\"}");
        return;
    }

    if (context.Request.Path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var dbReady = await db.Database.CanConnectAsync();
            if (!dbReady)
            {
                context.Response.StatusCode = 503;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync("{\"status\":\"Unhealthy\",\"database\":\"unreachable\"}");
                return;
            }

            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync("{\"status\":\"Healthy\",\"database\":\"reachable\"}");
            return;
        }
        catch
        {
            // No se expone el detalle de la excepcion para no filtrar informacion interna.
            context.Response.StatusCode = 503;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync("{\"status\":\"Unhealthy\",\"database\":\"error\"}");
            return;
        }
    }

    await next();
});

// La redireccion a HTTPS va antes de autenticacion/autorizacion para que un
// request HTTP no llegue a generar un challenge de login sobre HTTP.
app.UseHttpsRedirection();
app.UseAuthentication();

// Una cookie emitida antes de suspender la clínica seguiría siendo válida hasta
// su vencimiento. En cada navegación se comprueba el estado de la clínica y, si
// ya no está activa, se cierra la sesión y se explica el motivo en /login.
app.Use(async (context, next) =>
{
    var esNavegacion = context.Request.Headers.Accept
        .Any(valor => valor?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);

    if (!esNavegacion || context.User.Identity?.IsAuthenticated != true)
    {
        await next();
        return;
    }

    var clinicaIdClaim = context.User.FindFirst(ReclamacionesClinica.ClinicaId);
    if (clinicaIdClaim is null || !int.TryParse(clinicaIdClaim.Value, out var clinicaId))
    {
        // El Dueño del sistema no pertenece a ninguna clínica.
        await next();
        return;
    }

    var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
    var clinica = await dbContext.Clinicas
        .Where(item => item.Id == clinicaId)
        .Select(item => new
        {
            item.Estado,
            item.EnPrueba,
            item.FechaPrimerIngreso,
            item.FechaVencimiento
        })
        .FirstOrDefaultAsync(context.RequestAborted);

    // Una clínica inexistente se trata como suspendida: cierre por defecto.
    var acceso = clinica is null
        ? AccesoClinica.Suspendida
        : ReglaSuscripcion.Evaluar(
            clinica.Estado,
            clinica.EnPrueba,
            clinica.FechaPrimerIngreso,
            clinica.FechaVencimiento,
            ReglaSuscripcion.Hoy());

    if (acceso is AccesoClinica.Suspendida or AccesoClinica.Vencida)
    {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        context.Response.Redirect(acceso == AccesoClinica.Vencida
            ? "/login?error=clinic-expired"
            : "/login?error=clinic");
        return;
    }

    await next();
});

app.UseAuthorization();

// El limitador va antes de UseStatusCodePagesWithReExecute: si estuviera dentro, la
// respuesta 429 se re-ejecutaba como página de error y el cliente recibía otro código.
app.UseRateLimiter();

// Si alguien pide una direccion que no existe, el servidor responde 404 pero
// SIN cuerpo: el navegador mostraba una pantalla en blanco.
// UseStatusCodePagesWithReExecute vuelve a ejecutar el pipeline apuntando a /404,
// asi el usuario ve la pantalla "No encontramos esta pagina" y el navegador
// recibe igual el codigo 404 (que es lo correcto).
app.UseStatusCodePagesWithReExecute("/404");

// MapStaticAssets sirve wwwroot y los assets generados por el build
// (SonrisaDigital.UI.styles.css, _framework/*.js) desde el manifiesto de endpoints,
// por lo que funcionan igual en Development y en Production.
// Con UseStaticFiles() esos archivos solo se servian en Development y el resto
// de entornos la pagina quedaba sin estilos y sin Blazor.
app.MapStaticAssets().AllowAnonymous();
app.UseAntiforgery();

app.MapPost("/account/login", async (
    HttpContext context,
    IAntiforgery antiforgery,
    SignInManager<Usuario> signInManager,
    UserManager<Usuario> userManager,
    ApplicationDbContext dbContext,
    ILoggerFactory loggerFactory) =>
{
    // Un token vencido no es un error del servidor: la página de login quedó
    // obsoleta. Se devuelve al formulario con un aviso y un token nuevo.
    if (!await AntiforgeryValidoAsync(context, antiforgery, loggerFactory))
    {
        return Results.Redirect("/login?error=expired");
    }

    var form = await context.Request.ReadFormAsync();
    var email = form["Email"].ToString().Trim();
    var password = form["Password"].ToString();
    var returnUrl = form["ReturnUrl"].ToString();
    var user = await userManager.FindByEmailAsync(email);

    if (user is null || user.Estado != EstadoRegistro.Activo)
    {
        return Results.Redirect("/login?error=invalid");
    }

    // Se validan las credenciales antes de revelar el estado de la clínica.
    var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
    if (!result.Succeeded)
    {
        return Results.Redirect(result.IsLockedOut ? "/login?error=locked" : "/login?error=invalid");
    }

    if (user.ClinicaId is int clinicaId)
    {
        var clinica = await dbContext.Clinicas
            .Where(item => item.Id == clinicaId)
            .Select(item => new
            {
                item.Estado,
                item.EnPrueba,
                item.FechaPrimerIngreso,
                item.FechaVencimiento
            })
            .FirstOrDefaultAsync();

        var acceso = clinica is null
            ? AccesoClinica.Suspendida
            : ReglaSuscripcion.Evaluar(
                clinica.Estado,
                clinica.EnPrueba,
                clinica.FechaPrimerIngreso,
                clinica.FechaVencimiento,
                ReglaSuscripcion.Hoy());

        if (acceso is AccesoClinica.Suspendida or AccesoClinica.Vencida)
        {
            return Results.Redirect(acceso == AccesoClinica.Vencida
                ? "/login?error=clinic-expired"
                : "/login?error=clinic");
        }

        // La prueba de 30 días empieza con el primer ingreso, no al crear la clínica.
        // Si no se puede guardar la fecha se niega el acceso: una prueba sin
        // vencimiento dejaría a la clínica operando gratis de forma indefinida.
        try
        {
            var suscripcionService = context.RequestServices.GetRequiredService<ISuscripcionService>();
            await suscripcionService.IniciarPruebaSiCorrespondeAsync(clinicaId);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("Login").LogError(
                exception, "No se pudo iniciar la prueba de la clínica {ClinicaId}.", clinicaId);
            return Results.Redirect("/login?error=clinic");
        }
    }

    await signInManager.SignInAsync(user, isPersistent: false);
    return Results.Redirect(IsLocalUrl(returnUrl) ? returnUrl : "/");
}).AllowAnonymous().RequireRateLimiting("login");

// Logout por POST: evita CSRF por GET (p. ej. <img src="/account/logout">).
app.MapPost("/account/logout", async (
    HttpContext context,
    IAntiforgery antiforgery,
    SignInManager<Usuario> signInManager,
    ILoggerFactory loggerFactory) =>
{
    if (!await AntiforgeryValidoAsync(context, antiforgery, loggerFactory))
    {
        return Results.Redirect("/login?error=expired");
    }

    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
}).AllowAnonymous();

// Descarga de reportes en PDF por HTTP.
// El documento se genera en el servidor y se entrega con Content-Disposition
// attachment, sin base64 ni transporte por el circuito de Blazor.
app.MapGet("/reportes/pdf", async (
    HttpContext context,
    string? tipo,
    DateOnly? desde,
    DateOnly? hasta,
    int? odontologoId,
    string? estadoCita,
    string? estadoTratamiento,
    string? metodoPago,
    IReportePdfService pdfService,
    ICurrentUserService currentUserService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    if (!Enum.TryParse<TipoReportePdf>(tipo, ignoreCase: true, out var tipoReporte)
        || !Enum.IsDefined(tipoReporte))
    {
        return Results.BadRequest(new { error = "Tipo de reporte inválido." });
    }

    if (desde is null || hasta is null)
    {
        return Results.BadRequest(new
        {
            error = "Debes indicar la fecha inicial y la fecha final."
        });
    }

    var fechaDesde = desde.Value;
    var fechaHasta = hasta.Value;

    if (fechaHasta < fechaDesde)
    {
        return Results.BadRequest(new
        {
            error = "La fecha final no puede ser anterior a la fecha inicial."
        });
    }

    if (!TryParseFiltro<EstadoCita>(estadoCita, out var filtroEstadoCita))
    {
        return Results.BadRequest(new { error = "Estado de cita inválido." });
    }

    if (!TryParseFiltro<EstadoTratamiento>(estadoTratamiento, out var filtroEstadoTratamiento))
    {
        return Results.BadRequest(new { error = "Estado de tratamiento inválido." });
    }

    if (!TryParseFiltro<MetodoPago>(metodoPago, out var filtroMetodoPago))
    {
        return Results.BadRequest(new { error = "Método de pago inválido." });
    }

    // El endpoint corre en una petición HTTP distinta al circuito de Blazor.
    // ICurrentUserService se llena normalmente desde MainLayout, por lo que aquí
    // debe alimentarse desde el usuario autenticado de esta petición.
    var usuario = context.User;
    currentUserService.Establecer(
        usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        usuario.FindFirst(ClaimTypes.GivenName)?.Value
            ?? usuario.FindFirst(ClaimTypes.Name)?.Value,
        usuario);

    var filtro = new ReporteFiltroDto(
        fechaDesde,
        fechaHasta,
        odontologoId,
        filtroEstadoCita,
        filtroEstadoTratamiento,
        filtroMetodoPago);

    try
    {
        var pdf = await pdfService.GenerarAsync(
            tipoReporte,
            filtro,
            cancellationToken);

        var nombreArchivo =
            $"reporte-{tipoReporte.ToString().ToLowerInvariant()}-" +
            $"{fechaDesde:yyyyMMdd}-{fechaHasta:yyyyMMdd}.pdf";

        // Los reportes contienen datos clínicos y financieros: no deben quedar
        // almacenados en la caché del navegador ni de un proxy.
        context.Response.Headers["Cache-Control"] = "no-store";

        return Results.File(pdf, "application/pdf", nombreArchivo);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Json(
            new { error = "No tienes permiso para descargar este reporte." },
            statusCode: StatusCodes.Status403Forbidden);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        loggerFactory
            .CreateLogger("Reportes.Pdf")
            .LogError(
                ex,
                "Error generando el PDF. Tipo: {Tipo}, Desde: {Desde}, Hasta: {Hasta}",
                tipoReporte,
                fechaDesde,
                fechaHasta);

        return Results.Json(
            new { error = "No fue posible generar el PDF del reporte." },
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.RequireAuthorization("Reportes.Ver");

// Subida de una imagen (JPG o PNG) al odontograma del paciente. Va por HTTP, como los PDF, para no
// cargar archivos grandes en el circuito de Blazor. El límite de 20 MB lo valida el servicio.
app.MapPost("/pacientes/{pacienteId:int}/archivos", async (
    int pacienteId,
    HttpContext context,
    IAntiforgery antiforgery,
    IArchivoPacienteService archivoService,
    ICurrentUserService currentUserService,
    ILoggerFactory loggerFactory) =>
{
    if (!await AntiforgeryValidoAsync(context, antiforgery, loggerFactory))
    {
        return RedirigirArchivoConError(pacienteId, "La página expiró. Vuelve a intentar la subida.");
    }

    // Igual que los PDF: esta petición HTTP no tiene el usuario del circuito de Blazor.
    var usuario = context.User;
    currentUserService.Establecer(
        usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        usuario.FindFirst(ClaimTypes.GivenName)?.Value
            ?? usuario.FindFirst(ClaimTypes.Name)?.Value,
        usuario);

    var form = await context.Request.ReadFormAsync();
    var archivo = form.Files["archivo"];

    if (archivo is null || archivo.Length == 0)
    {
        return RedirigirArchivoConError(pacienteId, "Selecciona un archivo JPG o PNG.");
    }

    if (archivo.Length > LimitesArchivo.TamanoMaximo)
    {
        return RedirigirArchivoConError(pacienteId, "El archivo supera el máximo de 20 MB.");
    }

    if (!DateOnly.TryParseExact(form["fechaEstudio"].ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaEstudio))
    {
        return RedirigirArchivoConError(pacienteId, "La fecha del estudio no es válida.");
    }

    try
    {
        byte[] datos;
        await using (var flujo = archivo.OpenReadStream())
        using (var memoria = new MemoryStream())
        {
            await flujo.CopyToAsync(memoria);
            datos = memoria.ToArray();
        }

        await archivoService.SubirAsync(
            pacienteId,
            archivo.FileName,
            datos,
            fechaEstudio,
            form["descripcion"].ToString());

        return Results.Redirect($"/odontograma?paciente={pacienteId}&archivo=subido");
    }
    catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
    {
        return RedirigirArchivoConError(pacienteId, ex.Message);
    }
}).RequireAuthorization("Odontograma.Editar");

// Contenido de un archivo del paciente: la imagen completa o su miniatura. Nunca tiene una URL pública:
// cada petición pasa por el permiso y la clínica de la sesión, y no se guarda en caché.
app.MapGet("/archivos/{archivoId:int}", async (
    int archivoId,
    bool? miniatura,
    bool? descargar,
    HttpContext context,
    IArchivoPacienteService archivoService,
    ICurrentUserService currentUserService) =>
{
    var usuario = context.User;
    currentUserService.Establecer(
        usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        usuario.FindFirst(ClaimTypes.GivenName)?.Value
            ?? usuario.FindFirst(ClaimTypes.Name)?.Value,
        usuario);

    var contenido = await archivoService.ObtenerContenidoAsync(archivoId, miniatura == true);
    if (contenido is null)
    {
        return Results.NotFound();
    }

    context.Response.Headers["Cache-Control"] = "private, no-store";

    return descargar == true
        ? Results.File(contenido.Datos, contenido.TipoContenido, contenido.NombreOriginal)
        : Results.File(contenido.Datos, contenido.TipoContenido);
}).RequireAuthorization("Odontograma.Ver");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task<bool> AntiforgeryValidoAsync(
    HttpContext context,
    IAntiforgery antiforgery,
    ILoggerFactory loggerFactory)
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
        return true;
    }
    catch (AntiforgeryValidationException ex)
    {
        // Ocurre con formularios cacheados o abiertos antes de reiniciar la app.
        // No es un fallo del servidor: se registra y se pide un formulario nuevo.
        loggerFactory.CreateLogger("Antiforgery").LogWarning(
            ex,
            "Token antiforgery inválido en {Path}. Se redirige al formulario.",
            context.Request.Path);

        return false;
    }
}

static IResult RedirigirArchivoConError(int pacienteId, string mensaje) =>
    Results.Redirect($"/odontograma?paciente={pacienteId}&archivoError={Uri.EscapeDataString(mensaje)}");

static bool CorrelationIdValido(string? valor) =>
    !string.IsNullOrEmpty(valor)
    && System.Text.RegularExpressions.Regex.IsMatch(valor, "^[A-Za-z0-9-]{8,64}$");

static bool IsLocalUrl(string? url) =>
    !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\');

// Convierte un valor de query string en un enum opcional. Devuelve true cuando
// el valor está vacío (sin filtro) o cuando es un nombre válido del enum.
static bool TryParseFiltro<T>(string? valor, out T? resultado)
    where T : struct, Enum
{
    resultado = null;

    if (string.IsNullOrWhiteSpace(valor))
    {
        return true;
    }

    if (!Enum.TryParse<T>(valor, ignoreCase: true, out var parseado)
        || !Enum.IsDefined(parseado))
    {
        return false;
    }

    resultado = parseado;
    return true;
}

