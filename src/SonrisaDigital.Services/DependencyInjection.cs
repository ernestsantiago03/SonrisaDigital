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
using SonrisaDigital.Services.Tratamientos;

namespace SonrisaDigital.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddSonrisaDigitalServices(
        this IServiceCollection services,
        IConfiguration configuration)
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
                // Revalida la cookie en cada peticion para que los permisos se vuelvan a
                // calcular desde la base y los cambios lleguen a las sesiones abiertas.
                cookieOptions.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
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
        // en cero se regeneran los claims en cada peticion y el cambio es inmediato.
        services.AddScoped<ISecurityStampValidator, SecurityStampValidator<Usuario>>();
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRolService, RolService>();

        services.AddAuthorization(options =>
        {
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
        services.AddScoped<ITratamientoRepository, TratamientoRepository>();
        services.AddScoped<ITratamientoService, TratamientoService>();
        services.AddScoped<IPagoRepository, PagoRepository>();
        services.AddScoped<IPagoService, PagoService>();
        services.AddScoped<IHistoriaClinicaRepository, HistoriaClinicaRepository>();
        services.AddScoped<IHistoriaClinicaService, HistoriaClinicaService>();

        return services;
    }
}