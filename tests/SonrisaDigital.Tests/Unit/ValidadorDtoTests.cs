using SonrisaDigital.Services.Pacientes.DTOs;
using SonrisaDigital.Services.Usuarios.DTOs;
using SonrisaDigital.Services.Validacion;

namespace SonrisaDigital.Tests.Unit;

public sealed class ValidadorDtoTests
{
    [Fact]
    public void RecortaLosTextosAntesDeValidar()
    {
        var modelo = ModeloPacienteValido();
        modelo.Nombres = "   Ana   ";

        ValidadorDto.PrepararYValidar(modelo);

        Assert.Equal("Ana", modelo.Nombres);
    }

    [Fact]
    public void NoRecortaLaContrasena()
    {
        var modelo = new UsuarioFormDto
        {
            Nombre = "Ana",
            Email = "ana@clinica.com",
            Password = "  Clave1234  "
        };

        ValidadorDto.PrepararYValidar(modelo);

        Assert.Equal("  Clave1234  ", modelo.Password);
    }

    [Fact]
    public void RechazaCampoObligatorioQueQuedaVacioAlRecortar()
    {
        var modelo = ModeloPacienteValido();
        modelo.Apellidos = "   ";

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => ValidadorDto.PrepararYValidar(modelo));

        Assert.Equal("Los apellidos son obligatorios.", excepcion.Message);
    }

    [Fact]
    public void RechazaTextoMasLargoQueElLimiteConMensajeEnEspanol()
    {
        var modelo = ModeloPacienteValido();
        modelo.Nombres = new string('a', 101);

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => ValidadorDto.PrepararYValidar(modelo));

        Assert.Equal("Los nombres no pueden superar los 100 caracteres.", excepcion.Message);
    }

    [Fact]
    public void RechazaCorreoConFormatoInvalido()
    {
        var modelo = ModeloPacienteValido();
        modelo.Correo = "no-es-un-correo";

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => ValidadorDto.PrepararYValidar(modelo));

        Assert.Equal("Introduzca un correo válido.", excepcion.Message);
    }

    [Fact]
    public void AceptaUnModeloValido()
    {
        var modelo = ModeloPacienteValido();

        ValidadorDto.PrepararYValidar(modelo);
    }

    private static PacienteFormDto ModeloPacienteValido() => new()
    {
        Nombres = "Ana",
        Apellidos = "Pérez"
    };
}
