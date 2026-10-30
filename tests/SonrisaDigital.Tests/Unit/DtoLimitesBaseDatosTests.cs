using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SonrisaDigital.Data;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;
using SonrisaDigital.Services.Citas.DTOs;
using SonrisaDigital.Services.Odontologos.DTOs;
using SonrisaDigital.Services.Pacientes.DTOs;
using SonrisaDigital.Services.Tratamientos.DTOs;
using SonrisaDigital.Services.Usuarios.DTOs;

namespace SonrisaDigital.Tests.Unit;

/// <summary>
/// Comprueba que ningún formulario permita más caracteres de los que la base de datos
/// acepta. Si alguien sube un límite en el DTO sin cambiar la configuración de EF Core,
/// o al revés, esta prueba falla antes de llegar a producción.
/// </summary>
public sealed class DtoLimitesBaseDatosTests
{
    private static readonly IModel ModeloEf = CrearModelo();

    public static TheoryData<Type, string, Type, string> Parejas => new()
    {
        { typeof(PacienteFormDto), nameof(PacienteFormDto.Nombres), typeof(Paciente), nameof(Paciente.Nombres) },
        { typeof(PacienteFormDto), nameof(PacienteFormDto.Apellidos), typeof(Paciente), nameof(Paciente.Apellidos) },
        { typeof(OdontologoFormDto), nameof(OdontologoFormDto.Nombres), typeof(Odontologo), nameof(Odontologo.Nombres) },
        { typeof(OdontologoFormDto), nameof(OdontologoFormDto.Apellidos), typeof(Odontologo), nameof(Odontologo.Apellidos) },
        { typeof(CitaFormDto), nameof(CitaFormDto.Motivo), typeof(Cita), nameof(Cita.Motivo) },
        { typeof(TratamientoFormDto), nameof(TratamientoFormDto.Nombre), typeof(Tratamiento), nameof(Tratamiento.Nombre) },
        { typeof(UsuarioFormDto), nameof(UsuarioFormDto.Nombre), typeof(Usuario), nameof(Usuario.Nombre) },
        { typeof(UsuarioFormDto), nameof(UsuarioFormDto.Email), typeof(Usuario), nameof(Usuario.Email) }
    };

    [Theory]
    [MemberData(nameof(Parejas))]
    public void ElDtoNoPermiteMasCaracteresQueLaBase(
        Type tipoDto,
        string propiedadDto,
        Type tipoEntidad,
        string propiedadEntidad)
    {
        var limiteDto = tipoDto
            .GetProperty(propiedadDto, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;

        var limiteBase = ModeloEf
            .FindEntityType(tipoEntidad)!
            .FindProperty(propiedadEntidad)!
            .GetMaxLength();

        Assert.NotNull(limiteDto);
        Assert.NotNull(limiteBase);
        Assert.True(
            limiteDto <= limiteBase,
            $"{tipoDto.Name}.{propiedadDto} permite {limiteDto} caracteres, pero " +
            $"{tipoEntidad.Name}.{propiedadEntidad} solo admite {limiteBase}.");
    }

    private static IModel CrearModelo()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=ModeloSinConexion;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var contexto = new ApplicationDbContext(opciones, SinClinica.Instancia);
        return contexto.Model;
    }
}
