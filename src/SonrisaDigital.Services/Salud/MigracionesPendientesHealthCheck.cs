using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SonrisaDigital.Data;

namespace SonrisaDigital.Services.Salud;

/// <summary>
/// Comprueba que el esquema tenga aplicadas todas las migraciones. Una base
/// accesible pero atrasada rompe las consultas con errores de conversion
/// (por ejemplo DateOnly contra datetime2), asi que /health/ready debe fallar
/// antes de que el balanceador empiece a enviar trafico.
///
/// Aqui solo se decide el estado; el JSON lo escribe <see cref="RespuestaSalud"/>
/// para que no existan dos versiones del contrato.
/// </summary>
public sealed class MigracionesPendientesHealthCheck(
    ApplicationDbContext db,
    ILogger<MigracionesPendientesHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pendientes = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pendientes.Count == 0)
            {
                return HealthCheckResult.Healthy("El esquema tiene todas las migraciones aplicadas.");
            }

            // El detalle queda en el log del servidor; al cliente solo se le
            // informa "schema-outdated" para no exponer nombres internos.
            logger.LogError(
                "La base de datos tiene migraciones pendientes: {Migraciones}.",
                string.Join(", ", pendientes));

            return HealthCheckResult.Unhealthy(
                "La base de datos tiene migraciones pendientes.");
        }
        catch (Exception exception)
        {
            // El mensaje de la excepcion no se publica: el writer solo lee el
            // estado, y el detalle ya queda en el log del health check.
            return HealthCheckResult.Unhealthy(
                "No se pudo verificar el esquema de la base de datos.",
                exception);
        }
    }
}
