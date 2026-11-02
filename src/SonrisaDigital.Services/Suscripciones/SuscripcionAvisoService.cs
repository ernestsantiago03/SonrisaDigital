using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Suscripciones;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Suscripciones.DTOs;

namespace SonrisaDigital.Services.Suscripciones;

/// <summary>
/// Aviso de vencimiento para los usuarios de una clínica. Se calcula con la
/// clínica del inquilino (el claim firmado), no con una clínica elegida por el
/// navegador, para que nadie pueda consultar la suscripción de otra clínica.
/// </summary>
public interface ISuscripcionAvisoService
{
    /// <summary>Devuelve el aviso solo cuando está por vencer o en gracia; si no, <c>null</c>.</summary>
    Task<AvisoSuscripcionDto?> ObtenerAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Usa su propio DbContext: el layout consulta el aviso al abrir cada página y,
/// si compartiera el contexto del circuito, competiría con la consulta que la
/// propia página está ejecutando (el mismo problema que resuelve el selector
/// de clínicas del menú).
/// </summary>
public sealed class SuscripcionAvisoService(
    DbContextOptions<ApplicationDbContext> dbOptions,
    ITenantProvider tenantProvider,
    IOptions<SuscripcionOptions> options) : ISuscripcionAvisoService
{
    public async Task<AvisoSuscripcionDto?> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        if (tenantProvider.ClinicaId is not int clinicaId)
        {
            return null;
        }

        // La tabla Clinicas no lleva el filtro del inquilino y aquí siempre se
        // filtra por el id de la sesión, así que el contexto se crea sin clínica.
        await using var dbContext = new ApplicationDbContext(dbOptions, SinClinica.Instancia);

        var clinica = await dbContext.Clinicas
            .AsNoTracking()
            .Where(item => item.Id == clinicaId)
            .Select(item => new
            {
                item.Estado,
                item.EnPrueba,
                item.FechaPrimerIngreso,
                item.FechaVencimiento
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (clinica is null)
        {
            return null;
        }

        var hoy = ReglaSuscripcion.Hoy();
        var acceso = ReglaSuscripcion.Evaluar(
            clinica.Estado,
            clinica.EnPrueba,
            clinica.FechaPrimerIngreso,
            clinica.FechaVencimiento,
            hoy);

        var dias = ReglaSuscripcion.DiasRestantes(clinica.FechaVencimiento, hoy);
        var avisar = acceso == AccesoClinica.EnGracia
            || (acceso == AccesoClinica.Permitido
                && clinica.FechaVencimiento is not null
                && dias <= options.Value.DiasAviso);

        return avisar
            ? new AvisoSuscripcionDto(acceso, dias, clinica.EnPrueba)
            : null;
    }
}
