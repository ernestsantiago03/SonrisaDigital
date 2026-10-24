using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SonrisaDigital.UI.Components;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Odontograma;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSonrisaDigitalServices(builder.Configuration, builder.Environment);
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Rate limiting (Fase 1 - Parte D): limita intentos de login por IP.
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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Cabeceras HTTP de seguridad (Fase 1 - Parte B).
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

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool IsLocalUrl(string? url) =>
    !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\');
