using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Tests.Support;

namespace SonrisaDigital.Tests.Integration;

/// <summary>
/// Pruebas de aislamiento entre clínicas (plan multiclínica, criterios 2 a 4, 12 y 13).
/// Usan dos clínicas en la misma base y comprueban que una no ve ni modifica los datos de la otra.
/// </summary>
public sealed class AislamientoClinicasTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task UnaClinicaNoVeLosPacientesDeOtra()
    {
        var clinicaB = await CrearClinicaAsync();
        await using var dbA = fixture.CreateContext();
        await using var dbB = new ApplicationDbContext(fixture.Options, new TenantFijo(clinicaB));

        var pacienteA = await CrearPacienteAsync(dbA, "Aislamiento A");

        Assert.Single(await dbA.Pacientes.Where(p => p.Nombres == "Aislamiento A").ToListAsync());
        Assert.Empty(await dbB.Pacientes.Where(p => p.Nombres == "Aislamiento A").ToListAsync());
        Assert.Null(await dbB.Pacientes.FindAsync(pacienteA.Id));
    }

    [Fact]
    public async Task ElRegistroNuevoQuedaEnLaClinicaActiva()
    {
        var clinicaB = await CrearClinicaAsync();
        await using var dbB = new ApplicationDbContext(fixture.Options, new TenantFijo(clinicaB));

        var paciente = await CrearPacienteAsync(dbB, "Aislamiento B");

        Assert.Equal(clinicaB, paciente.ClinicaId);
    }

    [Fact]
    public async Task SinClinicaNoSeVeNingunDatoDeNegocio()
    {
        await using var dbA = fixture.CreateContext();
        await CrearPacienteAsync(dbA, "Aislamiento sin clinica");

        await using var dbSinClinica = new ApplicationDbContext(fixture.Options, SinClinica.Instancia);

        Assert.Empty(await dbSinClinica.Pacientes.Where(p => p.Nombres == "Aislamiento sin clinica").ToListAsync());
    }

    [Fact]
    public async Task SinClinicaNoSePuedeGuardarNingunRegistro()
    {
        await using var dbSinClinica = new ApplicationDbContext(fixture.Options, SinClinica.Instancia);
        dbSinClinica.Pacientes.Add(new Paciente { Nombres = "Sin clinica", Apellidos = "Prueba", Estado = EstadoRegistro.Activo });

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbSinClinica.SaveChangesAsync());
    }

    [Fact]
    public async Task NoSePuedeGuardarUnRegistroDeOtraClinica()
    {
        var clinicaB = await CrearClinicaAsync();
        await using var dbA = fixture.CreateContext();
        var paciente = new Paciente { Nombres = "De B", Apellidos = "Prueba", Estado = EstadoRegistro.Activo, ClinicaId = clinicaB };
        dbA.Pacientes.Add(paciente);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbA.SaveChangesAsync());
    }

    [Fact]
    public async Task NoSePuedeCambiarLaClinicaDeUnRegistro()
    {
        var clinicaB = await CrearClinicaAsync();
        await using var dbA = fixture.CreateContext();
        var paciente = await CrearPacienteAsync(dbA, "Cambio de clinica");

        paciente.ClinicaId = clinicaB;

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbA.SaveChangesAsync());
    }

    [Fact]
    public async Task UnaCitaNoPuedeReferenciarUnPacienteDeOtraClinica()
    {
        var clinicaB = await CrearClinicaAsync();
        await using var dbA = fixture.CreateContext();
        var pacienteA = await CrearPacienteAsync(dbA, "Referencia A");
        var odontologoA = await CrearOdontologoAsync(dbA);

        // Inserción directa en SQL (salta el control del DbContext): una cita de la clínica B
        // apunta a un paciente de la clínica A. La clave foránea compuesta debe rechazarla.
        await using var dbInsercionDirecta = new ApplicationDbContext(fixture.Options, SinClinica.Instancia);
        var fecha = new DateOnly(2030, 1, 7);
        var horaInicio = new TimeOnly(9, 0);
        var horaFin = new TimeOnly(9, 30);

        await Assert.ThrowsAnyAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            dbInsercionDirecta.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Citas (ClinicaId, PacienteId, OdontologoId, Fecha, HoraInicio, HoraFin, Estado)
                VALUES ({clinicaB}, {pacienteA.Id}, {odontologoA.Id}, {fecha}, {horaInicio}, {horaFin}, 'Programada')
                """));
    }

    private async Task<int> CrearClinicaAsync()
    {
        await using var db = new ApplicationDbContext(fixture.Options, SinClinica.Instancia);
        var clinica = new Clinica
        {
            Nombre = $"Clinica prueba {Guid.NewGuid():N}",
            Codigo = $"prueba-{Guid.NewGuid():N}"[..27]
        };
        db.Clinicas.Add(clinica);
        await db.SaveChangesAsync();
        return clinica.Id;
    }

    private static async Task<Paciente> CrearPacienteAsync(ApplicationDbContext db, string nombre)
    {
        var paciente = new Paciente { Nombres = nombre, Apellidos = "Prueba", Estado = EstadoRegistro.Activo };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        return paciente;
    }

    private static async Task<Odontologo> CrearOdontologoAsync(ApplicationDbContext db)
    {
        var odontologo = new Odontologo { Nombres = "Odontologo", Apellidos = "Aislamiento", Estado = EstadoRegistro.Activo };
        db.Odontologos.Add(odontologo);
        await db.SaveChangesAsync();
        return odontologo;
    }
}
