using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SonrisaDigital.Data;

/// <summary>
/// Utilidades de base de datos compartidas por los repositorios y servicios.
/// </summary>
public static class DatabaseFacadeExtensions
{
    /// <summary>
    /// Ejecuta una operación dentro de una transacción y de la estrategia de
    /// reintentos configurada en EF Core.
    ///
    /// Es necesario usar la estrategia cuando la conexión tiene reintentos
    /// habilitados: EF Core no permite abrir transacciones manuales por fuera de
    /// ella. El delegado puede ejecutarse más de una vez si ocurre un error
    /// transitorio, por eso la operación debe dejar la base en un estado
    /// consistente al comenzar cada intento.
    /// </summary>
    public static async Task ExecuteInTransactionAsync(
        this DatabaseFacade database,
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await database.BeginTransactionAsync(cancellationToken);

            try
            {
                await operation();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    /// <summary>
    /// Variante para operaciones que cargan y modifican entidades dentro del
    /// bloque. Limpia el rastreador antes de cada intento para que un reintento
    /// no reutilice entidades que quedaron marcadas por el intento anterior.
    /// </summary>
    public static async Task ExecuteInTransactionAsync(
        this DbContext context,
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);

        await context.Database.ExecuteInTransactionAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await operation();
        }, cancellationToken);
    }
}
