import { expect, test, type Browser, type Page } from '@playwright/test';

// Credenciales del administrador temporal que crea el script de arranque.
const correoAdmin = process.env.SONRISADIGITAL_E2E_EMAIL;
const claveAdmin = process.env.SONRISADIGITAL_E2E_PASSWORD;

// Cantidad de pacientes que crea la prueba. Con más de 10 se puede comprobar la paginación.
const cantidadPacientes = 11;
const sufijo = Date.now().toString().slice(-8);
const prefijoNombre = `E2E Busq ${sufijo}`;
const pacientesCreados: number[] = [];

test.describe.configure({ mode: 'serial' });

test.describe('Búsquedas clínicas', () => {
  test.skip(
    !correoAdmin || !claveAdmin,
    'Defina SONRISADIGITAL_E2E_EMAIL y SONRISADIGITAL_E2E_PASSWORD para ejecutar las pruebas E2E.'
  );

  test.beforeAll(async ({ browser }) => {
    test.setTimeout(300_000);
    const page = await nuevaPagina(browser);
    await iniciarSesion(page, correoAdmin!, claveAdmin!);

    for (let indice = 1; indice <= cantidadPacientes; indice++) {
      pacientesCreados.push(await crearPaciente(page, indice));
    }

    await page.context().close();
  });

  test('historia clínica muestra la lista, pagina y no la muestra al abrir un paciente por enlace', async ({ page }) => {
    await iniciarSesion(page, correoAdmin!, claveAdmin!);
    await irA(page, '/historia-clinica');

    // La lista aparece al entrar al módulo, sin pulsar Buscar.
    await expect(page.locator('.resultados-resumen')).toBeVisible();

    await buscar(page, prefijoNombre);
    await expect(page.locator('.resultados-resumen')).toContainText(`${cantidadPacientes} pacientes encontrados`);
    await expect(page.locator('.resultados-tabla tbody tr')).toHaveCount(10);

    await page.getByRole('button', { name: 'Página siguiente' }).click();
    await expect(page.locator('.resultados-tabla tbody tr')).toHaveCount(cantidadPacientes - 10);

    // Abrir un paciente desde su perfil no debe dejar la lista de búsqueda visible.
    await irA(page, `/historia-clinica/${pacientesCreados[0]}`);
    await expect(page.locator('.resultados-panel')).toHaveCount(0);
    await expect(page.getByText(`${prefijoNombre} 01`).first()).toBeVisible();
  });

  test('odontograma muestra la lista desde el inicio y pagina los resultados', async ({ page }) => {
    await iniciarSesion(page, correoAdmin!, claveAdmin!);
    await irA(page, '/odontograma');

    await expect(page.locator('.resultados-resumen')).toBeVisible();

    await buscar(page, prefijoNombre);
    await expect(page.locator('.resultados-resumen')).toContainText(`${cantidadPacientes} pacientes encontrados`);
    await expect(page.locator('.resultados-tabla tbody tr')).toHaveCount(10);

    await page.getByRole('button', { name: 'Página siguiente' }).click();
    await expect(page.locator('.resultados-tabla tbody tr')).toHaveCount(cantidadPacientes - 10);
  });

  test('el formulario de citas filtra pacientes con el buscador', async ({ page }) => {
    await iniciarSesion(page, correoAdmin!, claveAdmin!);
    await irA(page, '/cita/nuevo');

    await page.getByLabel('Buscar paciente').fill(prefijoNombre);

    // Opción vacía "-- Seleccionar paciente --" más las coincidencias.
    await expect(page.locator('#patient-select option')).toHaveCount(cantidadPacientes + 1);
  });
});

async function nuevaPagina(browser: Browser): Promise<Page> {
  const contexto = await browser.newContext({ locale: 'es-DO', timezoneId: 'America/Santo_Domingo' });
  return contexto.newPage();
}

// Crea un paciente con el formulario real y devuelve su id, leído de la URL de su ficha.
async function crearPaciente(page: Page, indice: number): Promise<number> {
  const numero = String(indice).padStart(2, '0');
  await irA(page, '/pacientes/nuevo');
  await page.locator('#first-name').fill(`${prefijoNombre} ${numero}`);
  await page.locator('#last-name').fill('Prueba');
  await page.locator('#identification').fill(`BUS-${sufijo}-${numero}`);
  await page.locator('#birth-date').fill('1990-01-15');
  await page.locator('#sex').selectOption('Femenino');
  await page.locator('#tipo-sangre').selectOption('O+');
  await page.locator('#phone').fill('809-555-0300');
  await page.locator('#email').fill(`busq.${sufijo}.${numero}@example.com`);
  await page.locator('#address').fill('Calle E2E 1');
  await page.getByRole('button', { name: 'Guardar paciente' }).click();

  await expect(page).toHaveURL(/\/pacientes\/\d+$/);
  const coincidencia = page.url().match(/\/pacientes\/(\d+)$/);
  return Number(coincidencia![1]);
}

async function buscar(page: Page, termino: string): Promise<void> {
  await page.getByPlaceholder('Escribe el nombre o apellido del paciente').fill(termino);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
}

async function iniciarSesion(page: Page, correo: string, clave: string): Promise<void> {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible();
  await page.locator('#email').fill(correo);
  await page.locator('#password').fill(clave);
  await page.getByRole('button', { name: 'Ingresar' }).click();
  await expect(page).toHaveURL(/\/$/);
  await esperarBlazor(page);
}

// Navega a una página interactiva y espera a que Blazor haya conectado el circuito.
async function irA(page: Page, ruta: string): Promise<void> {
  await page.goto(ruta);
  await esperarBlazor(page);
}

async function esperarBlazor(page: Page): Promise<void> {
  await page.waitForFunction(
    () => Boolean((window as any).Blazor?.reconnect),
    undefined,
    { timeout: 20_000 }
  );
  await page.waitForTimeout(300);
}
