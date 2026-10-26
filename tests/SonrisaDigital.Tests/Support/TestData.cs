using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Tests.Support;

/// <summary>
/// Datos mínimos y aislados para las pruebas de integración. Cada entidad usa
/// un sufijo único para no chocar con otras pruebas del mismo archivo.
/// </summary>
internal static class TestData
{
    public static string NuevoSufijo() => Guid.NewGuid().ToString("N")[..8];

    public static async Task<Usuario> AsegurarUsuarioAsync(
        ApplicationDbContext db,
        string usuarioId = TestSession.UsuarioId)
    {
        var existente = await db.Users.FirstOrDefaultAsync(usuario => usuario.Id == usuarioId);
        if (existente is not null)
        {
            return existente;
        }

        var sufijo = NuevoSufijo();
        var usuario = new Usuario
        {
            Id = usuarioId,
            UserName = $"pruebas-{sufijo}",
            NormalizedUserName = $"PRUEBAS-{sufijo.ToUpperInvariant()}",
            Email = $"pruebas-{sufijo}@sonrisadigital.local",
            NormalizedEmail = $"PRUEBAS-{sufijo}@SONRISADIGITAL.LOCAL",
            EmailConfirmed = true,
            Nombre = "Usuario de pruebas",
            Estado = EstadoRegistro.Activo
        };

        db.Users.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    public static async Task<Paciente> CrearPacienteAsync(
        ApplicationDbContext db,
        string? prefijo = null,
        EstadoRegistro estado = EstadoRegistro.Activo)
    {
        var sufijo = NuevoSufijo();
        var paciente = new Paciente
        {
            Nombres = $"{prefijo ?? "Paciente"} {sufijo}",
            Apellidos = "Pruebas",
            Estado = estado
        };

        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        return paciente;
    }

    public static async Task<Odontologo> CrearOdontologoAsync(
        ApplicationDbContext db,
        string? prefijo = null,
        EstadoRegistro estado = EstadoRegistro.Activo)
    {
        var sufijo = NuevoSufijo();
        var odontologo = new Odontologo
        {
            Nombres = $"{prefijo ?? "Odontologo"} {sufijo}",
            Apellidos = "Pruebas",
            Estado = estado,
            FechaRegistro = DateTime.UtcNow
        };

        db.Odontologos.Add(odontologo);
        await db.SaveChangesAsync();
        return odontologo;
    }

    public static async Task<Tratamiento> CrearTratamientoCatalogoAsync(
        ApplicationDbContext db,
        decimal precioBase = 100m)
    {
        var tratamiento = new Tratamiento
        {
            Nombre = $"Tratamiento {NuevoSufijo()}",
            PrecioBase = precioBase,
            Estado = EstadoRegistro.Activo
        };

        db.Tratamientos.Add(tratamiento);
        await db.SaveChangesAsync();
        return tratamiento;
    }

    public static async Task<TratamientoRealizado> CrearTratamientoRealizadoAsync(
        ApplicationDbContext db,
        int pacienteId,
        int odontologoId,
        int tratamientoId,
        decimal precio,
        EstadoTratamiento estado = EstadoTratamiento.EnProceso)
    {
        var realizado = new TratamientoRealizado
        {
            PacienteId = pacienteId,
            OdontologoId = odontologoId,
            TratamientoId = tratamientoId,
            Precio = precio,
            Fecha = DateTime.UtcNow,
            Estado = estado
        };

        db.TratamientosRealizados.Add(realizado);
        await db.SaveChangesAsync();
        return realizado;
    }
}
