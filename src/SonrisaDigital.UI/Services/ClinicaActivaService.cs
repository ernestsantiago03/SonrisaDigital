using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Auditoria;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.UI.Services;

/// <summary>
/// Clínica activa de la pestaña: mantiene la selección del Dueño en el circuito,
/// la conserva en sessionStorage y registra en auditoría cada cambio. Se centraliza
/// aquí para que el selector, el panel de clínicas y las páginas no dupliquen la
/// clave de almacenamiento ni el registro.
/// </summary>
public interface IClinicaActivaService
{
    /// <summary>
    /// Restaura la clínica guardada en esta pestaña si todavía existe. Devuelve
    /// <c>true</c> cuando se aplicó una selección válida.
    /// </summary>
    Task<bool> RestaurarAsync(IReadOnlyCollection<int> clinicasDisponibles);

    /// <summary>Selecciona una clínica, la guarda para esta pestaña y audita el cambio.</summary>
    Task SeleccionarAsync(int clinicaId, string nombre, string origen = "el selector");

    /// <summary>Quita la selección actual y borra la clínica guardada de la pestaña.</summary>
    Task LimpiarAsync();
}

public sealed class ClinicaActivaService(
    ProtectedSessionStorage sessionStorage,
    ICurrentUserService currentUserService,
    IAuditoriaService auditoriaService) : IClinicaActivaService
{
    // La elección se conserva al recargar, pero solo en esta pestaña del navegador.
    private const string ClaveClinicaSeleccionada = "sonrisadigital.clinica-seleccionada";

    public async Task<bool> RestaurarAsync(IReadOnlyCollection<int> clinicasDisponibles)
    {
        try
        {
            var guardada = await sessionStorage.GetAsync<int>(ClaveClinicaSeleccionada);
            if (!guardada.Success)
            {
                return false;
            }

            if (!clinicasDisponibles.Contains(guardada.Value))
            {
                // La clínica guardada ya no existe o dejó de estar disponible: se descarta.
                await sessionStorage.DeleteAsync(ClaveClinicaSeleccionada);
                return false;
            }

            currentUserService.SeleccionarClinica(guardada.Value);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            // Sin almacenamiento disponible se empieza sin clínica seleccionada.
            return false;
        }
    }

    public async Task SeleccionarAsync(int clinicaId, string nombre, string origen = "el selector")
    {
        currentUserService.SeleccionarClinica(clinicaId);

        try
        {
            await sessionStorage.SetAsync(ClaveClinicaSeleccionada, clinicaId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            // Sin almacenamiento del navegador la selección sigue viva en el circuito.
        }

        await auditoriaService.RegistrarAsync(
            "Seleccionó", "Clínicas", nameof(Clinica), clinicaId,
            $"Abrió la clínica {nombre} desde {origen}.", default, clinicaId);
    }

    public async Task LimpiarAsync()
    {
        currentUserService.SeleccionarClinica(null);

        try
        {
            await sessionStorage.DeleteAsync(ClaveClinicaSeleccionada);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            // Sin almacenamiento disponible no hay nada que borrar.
        }
    }
}
