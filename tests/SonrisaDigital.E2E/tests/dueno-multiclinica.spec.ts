import { expect, test, type Page } from '@playwright/test';

// Credenciales temporales del Dueño que crea el script de arranque.
const correoDueno = process.env.SONRISADIGITAL_E2E_DUENO_EMAIL;
const claveDueno = process.env.SONRISADIGITAL_E2E_DUENO_PASSWORD;

test.describe('Dueño y selector de clínica', () => {
  test.skip(
    !correoDueno || !claveDueno,
    'Defina SONRISADIGITAL_E2E_DUENO_EMAIL y SONRISADIGITAL_E2E_DUENO_PASSWORD para ejecutar esta prueba.'
  );

  test('elige una clínica, abre los módulos y conserva la selección al recargar', async ({ page }) => {
    await iniciarSesion(page, correoDueno!, claveDueno!);

    const selector = page.locator('#clinica-activa');
    await expect(selector).toBeVisible();
    await expect(selector.locator('option', { hasText: 'Clinica Demo' })).toHaveCount(1);

    await selector.selectOption({ label: 'Clinica Demo' });
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole('heading', { name: 'Inicio', exact: true })).toBeVisible();

    // Los módulos deben cargar sus datos al entrar, sin pulsar Buscar.
    await irA(page, '/roles');
    await expect(page.getByRole('heading', { name: 'Roles y permisos', level: 1 })).toBeVisible();
    await expect(page.locator('.role-list-item').filter({ hasText: 'Administrador' })).toBeVisible();
    await expect(page.getByText('No fue posible cargar los roles y permisos.')).toHaveCount(0);

    await irA(page, '/clinicas');
    await expect(page.getByRole('heading', { name: 'Clínicas', exact: true })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Clinica Demo' })).toBeVisible();

    // Acerca De debe leer la clínica elegida y permitir edición al Dueño.
    const eslogan = `Eslogan E2E ${Date.now()}`;
    await irA(page, '/acercaDe');
    await expect(page.getByRole('heading', { name: 'Información de la Empresa' })).toBeVisible();
    await expect(page.locator('input.form-control-readonly').first()).toHaveValue('Clinica Demo');
    await expect(page.getByRole('button', { name: 'Editar' })).toBeVisible();

    // El Dueño puede guardar la identidad de la clínica elegida.
    await page.getByRole('button', { name: 'Editar' }).click();
    await page.locator('#acerca-eslogan').fill(eslogan);
    await page.getByRole('button', { name: 'Guardar cambios' }).click();
    await expect(page.getByText('La información de la clínica se guardó correctamente.')).toBeVisible();

    // Al recargar la página completa se conserva la clínica de esta pestaña.
    await page.reload();
    await expect(page.locator('#clinica-activa')).not.toHaveValue('');
    await expect(page.locator('input.form-control-readonly').first()).toHaveValue('Clinica Demo');
    await expect(page.locator('input.form-control-readonly').nth(2)).toHaveValue(eslogan);
  });
});

async function iniciarSesion(page: Page, correo: string, clave: string): Promise<void> {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible();
  await page.locator('#email').fill(correo);
  await page.locator('#password').fill(clave);
  await page.getByRole('button', { name: 'Ingresar' }).click();
  await expect(page).toHaveURL(/\/$/);
  await esperarBlazor(page);
}

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
