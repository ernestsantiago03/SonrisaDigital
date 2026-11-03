using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Services.Autenticacion;

/// <summary>
/// Crea la clínica de demostración que usan el desarrollo local y las pruebas E2E.
///
/// No se llama en producción: allí la primera clínica y su administrador los crea
/// el Dueño desde la interfaz. La migración histórica todavía inserta la demo en
/// una base nueva, pero la migración <c>DemoFueraDeProduccion</c> la elimina cuando
/// no tiene datos; este seeder la vuelve a crear solo en Development.
/// </summary>
public static class ClinicaDemoSeeder
{
    public const string Codigo = "clinica-demo";
    public const string PlanPrueba = "Prueba";
    public const int LimiteUsuarios = 10;

    /// <summary>
    /// Crea la demo si no existe. Devuelve <c>true</c> cuando la creó y
    /// <c>false</c> cuando ya estaba presente, sin modificar su estado.
    /// </summary>
    public static async Task<bool> SeedAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (await dbContext.Clinicas.AnyAsync(
                clinic => clinic.Codigo == Codigo,
                cancellationToken))
        {
            return false;
        }

        dbContext.Clinicas.Add(new Clinica
        {
            Nombre = "Clinica Demo",
            Codigo = Codigo,
            Rnc = null,
            Telefono = "809-555-5555",
            Correo = "contacto@sonrisadigital.com",
            Direccion = "Av. Pedro A. Rivera, Plaza Comercial, Sonrisa Digital, 2do Nivel, La Vega, República Dominicana.",
            Horario = "Lunes a Viernes: 8:00 AM - 6:00 PM",
            Eslogan = "Tu sonrisa en las mejores manos profesionales.",
            Estado = EstadoClinica.Activa,
            FechaAlta = DateTime.UtcNow,
            Plan = PlanPrueba,
            FechaPrimerIngreso = null,
            FechaVencimiento = null,
            EnPrueba = true,
            LimiteUsuarios = LimiteUsuarios
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
