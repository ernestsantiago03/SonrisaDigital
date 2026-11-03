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
                    Console.WriteLine($"Dueño creado: {correo} (Id {id}).");
                    Console.WriteLine("Al primer inicio de sesión deberá activar el segundo factor.");
                    return 0;

                case ["desactivar-dueno", var correoBaja]:
                    await CuentasDueno.DesactivarAsync(userManager, correoBaja);
                    Console.WriteLine($"Dueño desactivado: {correoBaja}.");
                    return 0;

                default:
                    Console.Error.WriteLine("Uso: crear-dueno <correo> <nombre>  |  desactivar-dueno <correo>");
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
