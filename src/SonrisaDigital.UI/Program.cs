using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using SonrisaDigital.Data;
using SonrisaDigital.UI.Components;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Odontograma;
using SonrisaDigital.Services.Reportes;
using SonrisaDigital.Services.Reportes.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSonrisaDigitalServices(builder.Configuration, builder.Environment);

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
    .AddInteractiveServerComponents();

// Rate limiting : limita intentos de login por IP.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
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
        await OdontogramaCatalogSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<SonrisaDigital.Data.ApplicationDbContext>());
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
app.UseAuthorization();

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
app.UseRateLimiter();

app.MapPost("/account/login", async (HttpContext context, IAntiforgery antiforgery, SignInManager<Usuario> signInManager, UserManager<Usuario> userManager) =>
{
    await antiforgery.ValidateRequestAsync(context);
    var form = await context.Request.ReadFormAsync();
    var email = form["Email"].ToString().Trim();
    var password = form["Password"].ToString();
    var returnUrl = form["ReturnUrl"].ToString();
    var user = await userManager.FindByEmailAsync(email);

    if (user is null || user.Estado != EstadoRegistro.Activo)
    {
        return Results.Redirect("/login?error=invalid");
    }

    var result = await signInManager.PasswordSignInAsync(user, password, false, true);
    if (result.Succeeded)
    {
        return Results.Redirect(IsLocalUrl(returnUrl) ? returnUrl : "/");
    }

    return Results.Redirect(result.IsLockedOut ? "/login?error=locked" : "/login?error=invalid");
}).AllowAnonymous().RequireRateLimiting("login");

// Logout por POST: evita CSRF por GET (p. ej. <img src="/account/logout">).
app.MapPost("/account/logout", async (HttpContext context, IAntiforgery antiforgery, SignInManager<Usuario> signInManager) =>
{
    await antiforgery.ValidateRequestAsync(context);
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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

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

