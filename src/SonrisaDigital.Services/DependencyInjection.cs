using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Repositories;
using SonrisaDigital.Services.Autenticacion;
using SonrisaDigital.Services.Autorizacion;
using SonrisaDigital.Services.Odontograma;
using SonrisaDigital.Services.Citas;
using SonrisaDigital.Services.Dashboard;
using SonrisaDigital.Services.Odontologos;
using SonrisaDigital.Services.Pacientes;
using SonrisaDigital.Services.Pagos;
using SonrisaDigital.Services.HistoriasClinicas;
using SonrisaDigital.Services.Usuarios;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Reportes;
using SonrisaDigital.Services.Tratamientos;

namespace SonrisaDigital.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddSonrisaDigitalServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment? environment = null)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No se configuro DefaultConnection.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddIdentityCookies(options =>
        {
            options.ApplicationCookie?.Configure(cookieOptions =>
            {
                cookieOptions.LoginPath = "/login";
                // Sin parametros en la ruta: si se agrega un "?" aqui, el framework
                // construye mal la URL de redireccion y el usuario termina en un 404.
                cookieOptions.AccessDeniedPath = "/acceso-denegado";
                // Revalida la cookie en cada petición HTTP. Los circuitos Blazor ya
                // abiertos se cubren con RevalidatingIdentityAuthenticationStateProvider.
                cookieOptions.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;

                // Endurecimiento de la cookie de sesión (Fase 1 - Parte A).
                // HttpOnly evita que JavaScript (XSS) lea la cookie.
                cookieOptions.Cookie.HttpOnly = true;
                // SameSite=Lax protege contra CSRF sin romper la navegación top-level.
                cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
                // Secure solo en producción: en Development (http://localhost) una cookie
                // Secure no se enviaría y el login dejaría de funcionar.
                var isDevelopment = string.Equals(environment?.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
                cookieOptions.Cookie.SecurePolicy = isDevelopment
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                // Sesión con duración fija (8 h) sin renovación deslizante: reduce la
                // ventana de uso de una cookie robada.
                cookieOptions.ExpireTimeSpan = TimeSpan.FromHours(8);
                cookieOptions.SlidingExpiration = false;
                // Nombre explícito para evitar colisiones con otras apps en el mismo dominio.
                cookieOptions.Cookie.Name = "SonrisaDigital.Auth";
            });
        });

        services.AddIdentityCore<Usuario>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddRoles<Rol>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.AddScoped<IUserClaimsPrincipalFactory<Usuario>, UsuarioClaimsPrincipalFactory>();

        // Sin esto la cookie dura 30 minutos sin recargar los permisos; con el intervalo
        // en cero se regeneran los claims en cada petición HTTP. Para los circuitos
        // Blazor abiertos, el proveedor registrado en Program.cs hace la revalidación.
        services.AddScoped<ISecurityStampValidator, SecurityStampValidator<Usuario>>();
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRolService, RolService>();

        services.AddAuthorization(options =>
        {
            // Ninguna pagina ni endpoint queda accesible sin sesion salvo que marque
            // explicitamente [AllowAnonymous] (login, 404, error y acceso denegado).
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            foreach (var permission in PermissionCatalog.All)
            {
                options.AddPolicy(permission.Nombre, policy =>
                    policy.RequireClaim(PermissionCatalog.ClaimType, permission.Nombre));
            }
        });

        services.AddScoped<IPacienteRepository, PacienteRepository>();
        services.AddScoped<IPacienteService, PacienteService>();
        services.AddScoped<IOdontologoRepository, OdontologoRepository>();
        services.AddScoped<IOdontologoService, OdontologoService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IOdontogramaRepository, OdontogramaRepository>();
        services.AddScoped<IOdontogramaService, OdontogramaService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ICitaRepository, CitaRepository>();
        services.AddScoped<ICitaService, CitaService>();
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();
        // Reportes usa consultas de solo lectura sobre las entidades existentes;
        // no se registra ninguna tabla ni entidad nueva.
        services.AddScoped<IReporteRepository, ReporteRepository>();
        services.AddScoped<IReporteService, ReporteService>();
        // El generador PDF reutiliza IReporteService para aplicar los mismos
        // filtros, permisos y restricciones por odontólogo.
        services.AddScoped<IReportePdfService, ReportePdfService>();
        services.AddScoped<ITratamientoRepository, TratamientoRepository>();
        services.AddScoped<ITratamientoService, TratamientoService>();
        services.AddScoped<IPagoRepository, PagoRepository>();
        services.AddScoped<IPagoService, PagoService>();
        services.AddScoped<IHistoriaClinicaRepository, HistoriaClinicaRepository>();
        services.AddScoped<IHistoriaClinicaService, HistoriaClinicaService>();

        return services;
    }
}