using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
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
using SonrisaDigital.Services.Autorizacion;
using Microsoft.Extensions.Options;
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
// El límite por IP es alto a propósito: toda una clínica puede salir por una misma IP pública.
// La protección contra fuerza bruta contra una cuenta concreta está en LimitadorCuentaLogin.
var loginPermitLimit = builder.Configuration.GetValue<int?>("Authentication:LoginPermitLimit") is int configurado && configurado > 0
    ? configurado
    : 20;

var loginCuentaLimit = builder.Configuration.GetValue<int?>("Authentication:LoginCuentaPermitLimit") is int limiteCuenta && limiteCuenta > 0
    ? limiteCuenta
    : LimitadorCuentaLogin.IntentosPorMinutoPredeterminado;

builder.Services.AddSingleton(new LimitadorCuentaLogin(loginCuentaLimit));

// El formulario de subida no puede superar el archivo máximo más un margen para sus campos.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = LimitesArchivo.TamanoMaximo + LimitesArchivo.MargenFormulario;
});

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

    // Las subidas se limitan por usuario autenticado; sin sesión, por IP.
    options.AddPolicy("subidas-archivo", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // La activación del segundo factor se limita por usuario: 5 intentos cada 10 minutos.
    options.AddPolicy("mfa-activar", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Los circuitos Blazor no pasan por OnValidatePrincipal en cada interacción.
// Este proveedor revalida estado y SecurityStamp mientras el circuito sigue abierto.
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider>();

var app = builder.Build();

// Comandos de administración desde la consola del servidor. No se exponen en la web.
//   dotnet SonrisaDigital.UI.dll crear-dueno <correo> <nombre>
//   dotnet SonrisaDigital.UI.dll desactivar-dueno <correo>
if (args is ["crear-dueno" or "desactivar-dueno" or "restablecer-mfa-dueno", ..])
{
    Environment.ExitCode = await CliDueno.EjecutarAsync(app.Services, args);
    return;
}

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
    var esDesarrollo = app.Environment.IsDevelopment();
    try
    {
        // La demo es una ayuda de desarrollo y de las pruebas E2E. En producción
        // no se crea: el Dueño registra la primera clínica desde la interfaz.
        if (esDesarrollo)
        {
            await ClinicaDemoSeeder.SeedAsync(
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        await IdentitySeeder.SeedAsync(scope.ServiceProvider, builder.Configuration, logger, esDesarrollo);
        await OdontogramaCatalogSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<SonrisaDigital.Data.ApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<ICatalogoCache>());
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "No se pudo preparar Identity. Verifica que la base SonrisaDigital exista y tenga aplicada la migracion.");

        // En producción no se puede seguir con una base a medio preparar: sin
        // Dueño no hay forma de crear la primera clínica ni su administrador.
        if (!esDesarrollo)
        {
            throw;
        }
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

// Una cookie emitida antes de suspender o vencer la clínica seguiría siendo
// válida hasta su vencimiento. El middleware comprueba la suscripción en toda
// petición autenticada (páginas, archivos, PDF y handshake de Blazor) y cierra
// la sesión si la clínica ya no puede operar.
app.UseMiddleware<SuscripcionAccessMiddleware>();

// El Dueño debe activar el segundo factor antes de usar el sistema.
app.UseMiddleware<MfaDuenoMiddleware>();

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

// Activación del segundo factor. Va por un formulario normal y no por el circuito de Blazor: activar
// cambia el sello de seguridad y cierra la sesión actual, así que los códigos de recuperación deben
// entregarse en esta misma respuesta.
app.MapPost("/seguridad/mfa/activar", async (
    HttpContext context,
    IAntiforgery antiforgery,
    UserManager<Usuario> userManager,
    ILoggerFactory loggerFactory) =>
{
    if (!await AntiforgeryValidoAsync(context, antiforgery, loggerFactory))
    {
        return Results.Redirect("/seguridad/mfa?error=expired");
    }

    var user = await userManager.GetUserAsync(context.User);
    if (user is null || !context.User.IsInRole(RolesSonrisaDigital.Dueno))
    {
        return Results.Forbid();
    }

    if (user.TwoFactorEnabled)
    {
        return Results.Redirect("/seguridad/mfa");
    }

    if (await userManager.IsLockedOutAsync(user))
    {
        return Results.Redirect("/seguridad/mfa?error=invalid");
    }

    var form = await context.Request.ReadFormAsync();
    var codigo = new string(form["Codigo"].ToString().Where(char.IsDigit).ToArray());
    if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, codigo))
    {
        // Un código incorrecto cuenta como fallo de Identity, igual que una contraseña mal escrita.
        await userManager.AccessFailedAsync(user);
        return Results.Redirect("/seguridad/mfa?error=invalid");
    }

    await userManager.ResetAccessFailedCountAsync(user);

    await userManager.SetTwoFactorEnabledAsync(user, true);
    var codigosRecuperacion = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToList() ?? [];

    var items = string.Concat(codigosRecuperacion.Select(item => $"<li><code>{WebUtility.HtmlEncode(item)}</code></li>"));
    var html = $$"""
        <!doctype html>
        <html lang="es">
        <head><meta charset="utf-8" /><title>Segundo factor activo · Sonrisa Digital</title></head>
        <body style="font-family:Segoe UI,system-ui,sans-serif;max-width:640px;margin:40px auto;padding:0 16px;color:#0f172a">
            <h1>Segundo factor activo</h1>
            <p>Guarda estos códigos de recuperación fuera de este equipo, por ejemplo en un gestor de contraseñas o en papel.
               Cada código sirve una sola vez y <strong>no volverán a mostrarse</strong>.</p>
            <ul style="font-family:Consolas,monospace;font-size:16px;line-height:1.9">{{items}}</ul>
            <p>Por seguridad, tu sesión anterior se cerró. Inicia sesión de nuevo con tu contraseña y el código de la app.</p>
            <p><a href="/login">Ir a iniciar sesión</a></p>
        </body>
        </html>
        """;

    context.Response.Headers["Cache-Control"] = "no-store";
    return Results.Content(html, "text/html; charset=utf-8");
}).RequireAuthorization().RequireRateLimiting("mfa-activar");

// Cambio de contraseña del Dueño. Exige la contraseña actual y un código de la app, así que una sesión
// abierta por sí sola no alcanza. Al cambiarla se invalidan todas las sesiones y se vuelve a iniciar sesión.
app.MapPost("/seguridad/cuenta/clave", async (
    HttpContext context,
    IAntiforgery antiforgery,
    UserManager<Usuario> userManager,
    SignInManager<Usuario> signInManager,
    ILoggerFactory loggerFactory) =>
{
    if (!await AntiforgeryValidoAsync(context, antiforgery, loggerFactory))
    {
        return Results.Redirect("/seguridad/cuenta?error=expired");
    }

    var user = await userManager.GetUserAsync(context.User);
    if (user is null || !context.User.IsInRole(RolesSonrisaDigital.Dueno))
    {
        return Results.Forbid();
    }

    if (!user.TwoFactorEnabled)
    {
        return Results.Redirect("/seguridad/mfa");
    }

    var form = await context.Request.ReadFormAsync();
    var actual = form["ClaveActual"].ToString();
    var nueva = form["ClaveNueva"].ToString();
    var confirmacion = form["ClaveConfirmar"].ToString();
    var codigo = new string(form["Codigo"].ToString().Where(char.IsDigit).ToArray());

    if (nueva != confirmacion)
    {
        return Results.Redirect("/seguridad/cuenta?error=mismatch");
    }

    if (SeguridadDueno.ValidarClave(nueva) is not null)
    {
        return Results.Redirect("/seguridad/cuenta?error=weak");
    }

    // Esta comprobación cuenta los fallos para el bloqueo de la cuenta, igual que el login.
    var resultado = await signInManager.CheckPasswordSignInAsync(user, actual, lockoutOnFailure: true);
    if (resultado.IsLockedOut)
    {
        return Results.Redirect("/seguridad/cuenta?error=locked");
    }

    if (!resultado.Succeeded)
    {
        return Results.Redirect("/seguridad/cuenta?error=current");
    }

    if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, codigo))
    {
        await userManager.AccessFailedAsync(user);
        return Results.Redirect(await userManager.IsLockedOutAsync(user)
            ? "/seguridad/cuenta?error=locked"
            : "/seguridad/cuenta?error=code");
    }

    var cambio = await userManager.ChangePasswordAsync(user, actual, nueva);
    if (!cambio.Succeeded)
    {
        return Results.Redirect("/seguridad/cuenta?error=policy");
    }

    await userManager.ResetAccessFailedCountAsync(user);

    // Cerrar las demás sesiones: la nueva clave invalida cualquier cookie abierta con la anterior.
    await userManager.UpdateSecurityStampAsync(user);
    await signInManager.SignOutAsync();

    return Results.Redirect("/login?cambio=clave");
}).RequireAuthorization();

// Acceso al sistema. El orden es: contraseña, segundo factor (si está activo) y, por último, la clínica.
// La sesión del Dueño dura 2 horas (ver SeguridadDueno).
app.MapPost("/account/login", async (
    HttpContext context,
    IAntiforgery antiforgery,
    SignInManager<Usuario> signInManager,
    UserManager<Usuario> userManager,
    ILoggerFactory loggerFactory,
    IOptions<SeguridadOptions> seguridad) =>
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

    // Límite por cuenta: aplica exista o no el correo, para no revelar qué cuentas existen.
    if (!context.RequestServices.GetRequiredService<LimitadorCuentaLogin>().PermitirIntento(email))
    {
        return Results.Redirect("/login?error=throttled");
    }

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

    // Con segundo factor activo, la sesión no se abre todavía: se pide el código en un paso aparte.
    if (user.TwoFactorEnabled)
    {
        // Identity lee el id de esta cookie desde ClaimTypes.Name (GetTwoFactorAuthenticationUserAsync).
        var identidad = new ClaimsIdentity(IdentityConstants.TwoFactorUserIdScheme);
        identidad.AddClaim(new Claim(ClaimTypes.Name, user.Id));
        await context.SignInAsync(IdentityConstants.TwoFactorUserIdScheme, new ClaimsPrincipal(identidad));

        return Results.Redirect(DestinoSegundoPaso(returnUrl));
    }

    // El Dueño sin segundo factor entra solo para configurarlo. MfaDuenoMiddleware no le deja
    // usar nada más hasta que lo active.
    if (seguridad.Value.MfaDuenoObligatorio && await userManager.IsInRoleAsync(user, RolesSonrisaDigital.Dueno))
    {
        // La clave se genera aquí, antes de abrir la sesión: Identity cambia el sello de seguridad
        // al crearla, y hacerlo después invalidaría la cookie recién emitida.
        if (await userManager.GetAuthenticatorKeyAsync(user) is null)
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
        }

        await signInManager.SignInAsync(user, PropiedadesSesion());
        return Results.Redirect("/seguridad/mfa");
    }

    var redireccionClinica = await ValidarAccesoClinicaAsync(user, context, loggerFactory);
    if (redireccionClinica is not null)
    {
        return Results.Redirect(redireccionClinica);
    }

    await signInManager.SignInAsync(user, PropiedadesSesion());
    return Results.Redirect(IsLocalUrl(returnUrl) ? returnUrl : "/");
}).AllowAnonymous().RequireRateLimiting("login");

// Segundo paso del acceso: código de la app o código de recuperación. Un intento fallido cuenta
// para el bloqueo de la cuenta, igual que una contraseña mal escrita.
app.MapPost("/account/login-2fa", async (
    HttpContext context,
    IAntiforgery antiforgery,
    SignInManager<Usuario> signInManager,
    UserManager<Usuario> userManager,
    ILoggerFactory loggerFactory) =>
{
    if (!await AntiforgeryValidoAsync(context, antiforgery, loggerFactory))
    {
        return Results.Redirect("/login/dos-pasos?error=expired");
    }

    var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
    if (user is null)
    {
        return Results.Redirect("/login?error=expired");
    }

    var form = await context.Request.ReadFormAsync();
    var codigo = new string(form["Codigo"].ToString().Where(char.IsLetterOrDigit).ToArray());
    // Identity guarda los códigos como "XXXXX-XXXXX". Se acepta con o sin guion y en cualquier mayúscula.
    var soloLetras = new string(form["CodigoRecuperacion"].ToString().Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    var codigoRecuperacion = soloLetras.Length == 10 ? $"{soloLetras[..5]}-{soloLetras[5..]}" : string.Empty;
    var returnUrl = form["ReturnUrl"].ToString();

    if (await userManager.IsLockedOutAsync(user))
    {
        return Results.Redirect(ErrorSegundoPaso(returnUrl, "locked"));
    }

    var valido = false;
    if (codigoRecuperacion.Length > 0)
    {
        valido = (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, codigoRecuperacion)).Succeeded;
    }
    else if (codigo.Length > 0)
    {
        valido = await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, codigo);
    }

    if (!valido)
    {
        await userManager.AccessFailedAsync(user);
        var bloqueada = await userManager.IsLockedOutAsync(user);
        return Results.Redirect(ErrorSegundoPaso(returnUrl, bloqueada ? "locked" : "invalid"));
    }

    await userManager.ResetAccessFailedCountAsync(user);
    await context.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);

    var redireccionClinica = await ValidarAccesoClinicaAsync(user, context, loggerFactory);
    if (redireccionClinica is not null)
    {
        return Results.Redirect(redireccionClinica);
    }

    await signInManager.SignInAsync(user, PropiedadesSesion());
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
    string? desde,
    string? hasta,
    int? odontologoId,
    string? estadoCita,
    string? estadoTratamiento,
    string? metodoPago,
    int? clinicaId,
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

    if (!TryParseFechaPdf(desde, out var fechaDesde) || !TryParseFechaPdf(hasta, out var fechaHasta))
    {
        return Results.BadRequest(new
        {
            error = "Debes indicar la fecha inicial y la fecha final en formato yyyy-MM-dd."
        });
    }

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

    // La clínica seleccionada en el panel del Dueño viaja en la URL, igual que en /archivos.
    // Solo el Dueño la usa; el resto de usuarios siempre trabaja con su clínica del token.
    if (clinicaId is > 0 && usuario.IsInRole(RolesSonrisaDigital.Dueno))
    {
        currentUserService.SeleccionarClinica(clinicaId);
    }

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
    // Se rechaza antes de leer el cuerpo: un archivo enorme no llega a memoria ni a disco.
    var limiteCuerpo = LimitesArchivo.TamanoMaximo + LimitesArchivo.MargenFormulario;
    if (context.Request.ContentLength is long tamanoDeclarado && tamanoDeclarado > limiteCuerpo)
    {
        return RedirigirArchivoConError(pacienteId, "El archivo supera el máximo de 20 MB.");
    }

    var limiteKestrel = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (limiteKestrel is { IsReadOnly: false })
    {
        limiteKestrel.MaxRequestBodySize = limiteCuerpo;
    }

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

    // Igual que en /archivos: el Dueño no tiene clínica propia y esta petición no conoce el selector del circuito.
    if (int.TryParse(form["clinicaId"].ToString(), out var clinicaId) && clinicaId > 0 && usuario.IsInRole(RolesSonrisaDigital.Dueno))
    {
        currentUserService.SeleccionarClinica(clinicaId);
    }

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
}).RequireAuthorization("Odontograma.Editar").RequireRateLimiting("subidas-archivo");

// Contenido de un archivo del paciente: la imagen completa o su miniatura. Nunca tiene una URL pública:
// cada petición pasa por el permiso y la clínica de la sesión, y no se guarda en caché.
app.MapGet("/archivos/{archivoId:int}", async (
    int archivoId,
    bool? miniatura,
    bool? descargar,
    int? clinicaId,
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

    // El Dueño no tiene clínica propia y la imagen se pide con una petición HTTP normal, que no
    // conoce la clínica del selector. La clínica del archivo viaja en la URL; solo el Dueño la usa.
    if (clinicaId is > 0 && usuario.IsInRole(RolesSonrisaDigital.Dueno))
    {
        currentUserService.SeleccionarClinica(clinicaId);
    }

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

static AuthenticationProperties PropiedadesSesion()
{
    var propiedades = new AuthenticationProperties { IsPersistent = false };
    SeguridadDueno.MarcarInicioSesion(propiedades, DateTimeOffset.UtcNow);
    return propiedades;
}

static string DestinoSegundoPaso(string? returnUrl) =>
    IsLocalUrl(returnUrl)
        ? $"/login/dos-pasos?returnUrl={Uri.EscapeDataString(returnUrl!)}"
        : "/login/dos-pasos";

static string ErrorSegundoPaso(string? returnUrl, string error) =>
    IsLocalUrl(returnUrl)
        ? $"/login/dos-pasos?returnUrl={Uri.EscapeDataString(returnUrl!)}&error={error}"
        : $"/login/dos-pasos?error={error}";

// Devuelve a dónde redirigir si la clínica del usuario no puede operar; null si puede entrar.
static async Task<string?> ValidarAccesoClinicaAsync(Usuario user, HttpContext context, ILoggerFactory loggerFactory)
{
    if (user.ClinicaId is not int clinicaId)
    {
        return null;
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
        return acceso == AccesoClinica.Vencida ? "/login?error=clinic-expired" : "/login?error=clinic";
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
        return "/login?error=clinic";
    }

    return null;
}

static bool IsLocalUrl(string? url) =>
    !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\');

// Lee una fecha yyyy-MM-dd del query string. Se recibe como texto para que un valor
// mal formado (p. ej. "2026-11-30?") responda 400 en lugar de fallar al enlazar parámetros.
static bool TryParseFechaPdf(string? valor, out DateOnly fecha)
{
    var limpio = valor?.Trim().TrimEnd('?').Trim();

    return DateOnly.TryParseExact(
        limpio,
        "yyyy-MM-dd",
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None,
        out fecha);
}

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

