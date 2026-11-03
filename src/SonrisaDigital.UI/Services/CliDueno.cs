using System.Text;
using Microsoft.AspNetCore.Identity;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Services.Autenticacion;

namespace SonrisaDigital.UI.Services;

/// <summary>
/// Comandos de administración de cuentas Dueño, ejecutados desde la consola del servidor.
/// </summary>
public static class CliDueno
{
    public static async Task<int> EjecutarAsync(IServiceProvider services, string[] args)
    {
        // UserManager y RoleManager son de alcance (scoped): se resuelven dentro de un ámbito propio.
        await using var ambito = services.CreateAsyncScope();
        var userManager = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var roleManager = ambito.ServiceProvider.GetRequiredService<RoleManager<Rol>>();

        // La base no admite auditoría sin clínica y el Dueño no tiene clínica: estas acciones
        // quedan en el log del servidor, con fecha y usuario de consola.
        var logger = ambito.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seguridad.Consola");

        try
        {
            switch (args)
            {
                case ["crear-dueno", var correo, var nombre]:
                    if (Console.IsInputRedirected)
                    {
                        Console.Error.WriteLine("Crear un Dueño necesita una consola interactiva para pedir la contraseña.");
                        return 1;
                    }

                    var clave = LeerClave("Contraseña (mínimo 16 caracteres): ");
                    var confirmacion = LeerClave("Repite la contraseña: ");
                    if (clave != confirmacion)
                    {
                        Console.Error.WriteLine("Las contraseñas no coinciden. No se creó la cuenta.");
                        return 1;
                    }

                    var id = await CuentasDueno.CrearAsync(userManager, roleManager, correo, nombre, clave);
                    logger.LogWarning("Consola: se creó el Dueño {Correo} (Id {Id}).", correo, id);
                    Console.WriteLine($"Dueño creado: {correo} (Id {id}).");
                    Console.WriteLine("Al primer inicio de sesión deberá activar el segundo factor.");
                    return 0;

                case ["desactivar-dueno", var correoBaja]:
                    await CuentasDueno.DesactivarAsync(userManager, correoBaja);
                    logger.LogWarning("Consola: se desactivó el Dueño {Correo}.", correoBaja);
                    Console.WriteLine($"Dueño desactivado: {correoBaja}.");
                    return 0;

                case ["restablecer-mfa-dueno", var correoMfa]:
                    if (Console.IsInputRedirected)
                    {
                        Console.Error.WriteLine("Este comando necesita una consola interactiva para confirmar.");
                        return 1;
                    }

                    Console.Write($"Se quitará el segundo factor de {correoMfa}. Escribe SI para confirmar: ");
                    if (Console.ReadLine()?.Trim() != "SI")
                    {
                        Console.Error.WriteLine("Operación cancelada.");
                        return 1;
                    }

                    await CuentasDueno.RestablecerSegundoFactorAsync(userManager, correoMfa);
                    logger.LogWarning("Consola: se restableció el segundo factor del Dueño {Correo}.", correoMfa);
                    Console.WriteLine($"Segundo factor restablecido para {correoMfa}.");
                    Console.WriteLine("Al siguiente inicio de sesión deberá volver a activarlo.");
                    return 0;

                default:
                    Console.Error.WriteLine(
                        "Uso: crear-dueno <correo> <nombre>  |  desactivar-dueno <correo>  |  restablecer-mfa-dueno <correo>");
                    return 1;
            }
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    // Lee sin mostrar los caracteres en pantalla.
    private static string LeerClave(string mensaje)
    {
        Console.Write(mensaje);
        var clave = new StringBuilder();

        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (clave.Length > 0)
                {
                    clave.Length--;
                }
            }
            else if (!char.IsControl(tecla.KeyChar))
            {
                clave.Append(tecla.KeyChar);
            }
        }

        Console.WriteLine();
        return clave.ToString();
    }
}
