import { createHmac } from 'node:crypto';
import { expect, test, type Page } from '@playwright/test';

// Esta prueba corre contra una instancia con Seguridad:MfaDuenoObligatorio=true y una base
// nueva. La cuenta del Dueño empieza sin segundo factor, así que la prueba lo activa y luego
// vuelve a entrar con el código de la app. Solo puede ejecutarse una vez por base.
const correoDueno = process.env.SONRISADIGITAL_E2E_MFA_DUENO_EMAIL;
const claveDueno = process.env.SONRISADIGITAL_E2E_MFA_DUENO_PASSWORD;

test.describe.configure({ retries: 0 });

test.describe('Segundo factor obligatorio del Dueño', () => {
  test.skip(
    !correoDueno || !claveDueno,
    'Defina SONRISADIGITAL_E2E_MFA_DUENO_EMAIL y SONRISADIGITAL_E2E_MFA_DUENO_PASSWORD para ejecutar esta prueba.'
  );

  test('activa el segundo factor, bloquea la navegación hasta hacerlo y vuelve a entrar con el código', async ({ page }) => {
    await iniciarSesion(page, correoDueno!, claveDueno!);
    await expect(page).toHaveURL(/\/seguridad\/mfa$/);

    // Sin segundo factor, ninguna pantalla del sistema es accesible: ni por HTTP ni dentro del circuito.
    await page.goto('/pacientes');
    await expect(page).toHaveURL(/\/seguridad\/mfa$/);

    await esperarBlazor(page);
    await page.locator('a[href="/pacientes"]').click();
    await page.waitForTimeout(1500);
    await expect(page).toHaveURL(/\/seguridad\/mfa$/);
    await expect(page.getByRole('heading', { name: 'Pacientes', level: 1 })).toHaveCount(0);

    // La clave manual debe aparecer en la pantalla: sin ella no hay forma de configurar la app.
    const claveMostrada = (await page.locator('#mfa-clave').innerText()).replace(/\s+/g, '');
    expect(claveMostrada).toMatch(/^[A-Z2-7]{16,}$/);

    // Un código incorrecto se rechaza y la página lo indica.
    await page.locator('#mfa-codigo').fill('000000');
    await page.getByRole('button', { name: 'Verificar' }).click();
    await expect(page.getByText('El código no es correcto.')).toBeVisible();

    // Con el código correcto se activa y se muestran los códigos de recuperación.
    await esperarBlazor(page);
    const pasoActivacion = pasoActual();
    await page.locator('#mfa-codigo').fill(codigoTotp(claveMostrada, pasoActivacion));
    await page.getByRole('button', { name: 'Verificar' }).click();
    await expect(page.getByRole('heading', { name: 'Segundo factor activo' })).toBeVisible();
    await expect(page.locator('li code')).toHaveCount(10);

    // La sesión se cerró al activar: el siguiente ingreso pide el segundo paso.
    await iniciarSesion(page, correoDueno!, claveDueno!, false);
    await expect(page).toHaveURL(/\/login\/dos-pasos/);

    // Un código ya usado en la misma ventana de 30 s no se acepta de nuevo: se espera la siguiente.
    await esperarPasoNuevo(pasoActivacion);
    await page.locator('#codigo').fill(codigoTotp(claveMostrada));
    await page.getByRole('button', { name: 'Verificar' }).click();
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole('heading', { name: 'Inicio', exact: true })).toBeVisible();
  });
});

async function iniciarSesion(page: Page, correo: string, clave: string, esperarInicio = true): Promise<void> {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible();
  await page.locator('#email').fill(correo);
  await page.locator('#password').fill(clave);
  await page.getByRole('button', { name: 'Ingresar' }).click();
  if (esperarInicio) {
    await expect(page).toHaveURL(/\/seguridad\/mfa$/);
  }
}

async function esperarBlazor(page: Page): Promise<void> {
  await page.waitForFunction(
    () => Boolean((window as any).Blazor?.reconnect),
    undefined,
    { timeout: 20_000 }
  );
  await page.waitForTimeout(300);
}

async function esperarPasoNuevo(pasoUsado: number): Promise<void> {
  const limite = Date.now() + 35_000;
  while (pasoActual() === pasoUsado && Date.now() < limite) {
    await new Promise((resolver) => setTimeout(resolver, 1000));
  }
}

function pasoActual(): number {
  return Math.floor(Date.now() / 30_000);
}

function codigoTotp(claveBase32: string, paso = pasoActual()): string {
  const contador = Buffer.alloc(8);
  contador.writeUInt32BE(paso, 4);
  const hash = createHmac('sha1', base32(claveBase32)).update(contador).digest();
  const desplazamiento = hash[hash.length - 1] & 0x0f;
  const numero =
    ((hash[desplazamiento] & 0x7f) << 24) |
    (hash[desplazamiento + 1] << 16) |
    (hash[desplazamiento + 2] << 8) |
    hash[desplazamiento + 3];
  return String(numero % 1_000_000).padStart(6, '0');
}

function base32(texto: string): Buffer {
  const alfabeto = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = '';
  for (const caracter of texto.toUpperCase()) {
    bits += alfabeto.indexOf(caracter).toString(2).padStart(5, '0');
  }

  const bytes: number[] = [];
  for (let i = 0; i + 8 <= bits.length; i += 8) {
    bytes.push(parseInt(bits.slice(i, i + 8), 2));
  }
  return Buffer.from(bytes);
}
