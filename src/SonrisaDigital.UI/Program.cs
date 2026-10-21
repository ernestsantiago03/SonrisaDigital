using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using SonrisaDigital.UI.Components;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Odontograma;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSonrisaDigitalServices(builder.Configuration);
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
}).AllowAnonymous();

app.MapGet("/account/logout", async (SignInManager<Usuario> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool IsLocalUrl(string? url) =>
    !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\');
